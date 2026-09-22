using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Kinematic Third-Person Player Controller implementing IPlayerController and IStandHeadroomProbe.
    /// Manages Unity CharacterController physics, deterministic linear slewing FSM,
    /// feet-anchored continuous capsule scaling, upward stand headroom query gating,
    /// downward ground-snapping, and zero-allocation snapshot publication.
    /// Governed by ADR-0006, GDD #11, and control-manifest.md.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerThirdPersonController : MonoBehaviour, IPlayerController, IStandHeadroomProbe
    {
        [Header("Configuration")]
        [SerializeField]
        private PlayerConfig _config = new PlayerConfig();

        [Header("Components")]
        [SerializeField]
        private CharacterController _characterController;

        [Header("Physics Layers")]
        [SerializeField]
        private LayerMask _e20LayerMask = 1 << 20;

        private readonly Collider[] _standProbeBuffer = new Collider[16];

        private PlayerLocomotionFSM _fsm;
        private bool _isControlSevered;
        private Vector3 _planarBasisForward = Vector3.forward;
        private Vector3 _planarBasisRight = Vector3.right;

        /// <summary>
        /// Gets the current locomotion state machine driving this controller.
        /// </summary>
        public PlayerLocomotionFSM Fsm => _fsm;

        /// <summary>
        /// Gets the configuration driving this controller.
        /// </summary>
        public PlayerConfig Config => _config;

        /// <summary>
        /// Gets the current immutable locomotion snapshot without managed allocations.
        /// </summary>
        public PlayerLocomotionSnapshot CurrentSnapshot => _fsm != null ? _fsm.GenerateSnapshot() : default;

        /// <summary>
        /// Gets the current animation feed for view-layer driving without managed allocations.
        /// </summary>
        public PlayerAnimationFeed CurrentAnimationFeed => _fsm != null ? _fsm.GenerateAnimationFeed() : default;

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            InitializeController();
        }

        /// <summary>
        /// Explicit initialization method for headless execution or runtime setup.
        /// </summary>
        public void InitializeController()
        {
            _config ??= PlayerConfig.CreateDefault();
            _fsm ??= new PlayerLocomotionFSM(_config);
            _fsm.SetHeadroomProbe(this);

            if (_characterController != null)
            {
                _characterController.skinWidth = _config.SkinWidth;
                _characterController.minMoveDistance = 0.0f;
                _characterController.radius = _config.CapsuleRadius;
                _characterController.height = _config.StandCapsuleHeight;
                _characterController.center = new Vector3(0f, _config.StandCapsuleHeight * 0.5f, 0f);
                _characterController.slopeLimit = 45.0f;
                _characterController.stepOffset = _config.StandStepOffset;
            }

            _fsm.Teleport(transform.position, transform.rotation);
        }

        /// <summary>
        /// Authoritative simulation tick driver.
        /// Deterministic and can be called headlessly in EditMode / PlayMode test suites.
        /// </summary>
        /// <param name="input">Raw player input packet for this tick.</param>
        /// <param name="deltaTime">Simulation time delta in seconds.</param>
        public void Tick(PlayerInputPacket input, float deltaTime)
        {
            if (_fsm == null)
            {
                InitializeController();
            }

            // Sync current position & grounding from Unity component if present
            if (_characterController != null)
            {
                _fsm.SetPosition(transform.position);
                _fsm.SetGrounded(_characterController.isGrounded);
            }

            if (_isControlSevered)
            {
                input = new PlayerInputPacket(input.MoveAxes, input.IsRunHeld, input.CrouchToggleEdge, isControlSevered: true);
            }

            // Advance FSM simulation
            _fsm.Tick(input, _planarBasisForward, _planarBasisRight, deltaTime);

            // Update transform orientation to reflect FSM facing
            if (_fsm.CurrentFacing != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(_fsm.CurrentFacing, Vector3.up);
            }

            // Apply displacement and capsule scaling through CharacterController if present
            if (_characterController != null && _characterController.enabled)
            {
                // AC-P13: Continuous feet-anchored capsule scaling (center.y = height / 2)
                float currentCapHeight = _fsm.CurrentCapsuleHeight;
                _characterController.height = currentCapHeight;
                _characterController.center = new Vector3(0f, currentCapHeight * 0.5f, 0f);

                // Dynamic step offset adjustment based on active stance (0.30m stand, 0.15m crouch)
                _characterController.stepOffset = (_fsm.CurrentStance == MovementStance.Crouched)
                    ? _config.CrouchStepOffset
                    : _config.StandStepOffset;

                Vector3 horizontalDisplacement = _fsm.CurrentVelocity * deltaTime;
                Vector3 verticalSnap = Vector3.down * (-_config.DownwardGroundSnap) * deltaTime; // Ground snap bias
                Vector3 totalDisplacement = horizontalDisplacement + verticalSnap;

                _characterController.Move(totalDisplacement);
                _fsm.SetPosition(transform.position);
            }
            else
            {
                // Headless integration: apply horizontal velocity directly to tracked position
                Vector3 newPos = _fsm.CurrentPosition + _fsm.CurrentVelocity * deltaTime;
                _fsm.SetPosition(newPos);
            }
        }

        /// <summary>
        /// Evaluates whether overhead clearance exists for standing upright at the specified anchor.
        /// Uses Physics.OverlapCapsuleNonAlloc against E20 with lower hemisphere ground insetting.
        /// Satisfies AC-P12, GDD #11 Section H.0.13, and control-manifest.md.
        /// </summary>
        /// <param name="feetPosition">Ground-plane reference coordinates of character feet.</param>
        /// <param name="hitCount">Outputs the number of detected blocking colliders.</param>
        /// <returns>True if clearance is granted (unobstructed); false if blocked.</returns>
        public bool QueryStandClearance(Vector3 feetPosition, out int hitCount)
        {
            float radius = _config.CapsuleRadius;
            Vector3 bottom = feetPosition + Vector3.up * _config.StandProbeBottomOffset;
            Vector3 top = feetPosition + Vector3.up * _config.StandProbeTopOffset;

            Physics.SyncTransforms();
            hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                _standProbeBuffer,
                _e20LayerMask,
                QueryTriggerInteraction.Ignore);

            // Fail-safe buffer saturation check
            if (hitCount >= _standProbeBuffer.Length)
            {
                return false;
            }

            return hitCount == 0;
        }

        /// <summary>
        /// Updates the external planar basis vectors (supplied by CameraRigService).
        /// </summary>
        /// <param name="planarForward">Camera planar forward vector on the XZ plane.</param>
        /// <param name="planarRight">Camera planar right vector on the XZ plane.</param>
        public void SetPlanarBasis(Vector3 planarForward, Vector3 planarRight)
        {
            _planarBasisForward = planarForward;
            _planarBasisRight = planarRight;
        }

        /// <summary>
        /// Updates the planar basis from raw camera forward and up vectors, handling pitch degeneracy.
        /// Fully non-allocating method satisfying AC-P19 and ADR-0006.
        /// </summary>
        /// <param name="camForward">Camera transform forward vector.</param>
        /// <param name="camUp">Camera transform up vector.</param>
        public void SetPlanarBasisFromCamera(Vector3 camForward, Vector3 camUp)
        {
            PlayerLocomotionFSM.CalculatePlanarBasis(camForward, camUp, out _planarBasisForward, out _planarBasisRight);
        }

        /// <summary>
        /// Teleports the character instantaneously to a world transform, resetting velocity.
        /// </summary>
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_characterController != null)
            {
                _characterController.enabled = false;
                transform.position = worldPosition;
                transform.rotation = worldRotation;
                _characterController.enabled = true;
            }
            else
            {
                transform.position = worldPosition;
                transform.rotation = worldRotation;
            }

            _fsm?.Teleport(worldPosition, worldRotation);
        }

        /// <summary>
        /// Authoritative respawn reset restoring standing posture, spawn facing, and clear states.
        /// Satisfies AC-P16 and C8.
        /// </summary>
        /// <param name="worldPosition">Target spawn coordinates.</param>
        /// <param name="worldRotation">Target spawn orientation.</param>
        public void Respawn(Vector3 worldPosition, Quaternion worldRotation)
        {
            _isControlSevered = false;
            Teleport(worldPosition, worldRotation);
            _fsm?.Respawn(worldPosition, worldRotation);
        }

        /// <summary>
        /// Transitions the controller into the InHideSpot containment state.
        /// Locks horizontal translation and aligns with the interior anchor.
        /// Satisfies TR-FEAT-015 and TR-FEAT-016.
        /// </summary>
        /// <param name="interiorAnchor">Interior focal position.</param>
        /// <param name="portalForward">Outward normal vector from the hide spot opening.</param>
        public void EnterHideSpot(Vector3 interiorAnchor, Vector3 portalForward)
        {
            Teleport(interiorAnchor, Quaternion.LookRotation(portalForward, Vector3.up));
            _fsm?.EnterHideSpot(interiorAnchor, portalForward);
        }

        /// <summary>
        /// Exits hide spot containment and places the character at the specified standoff point.
        /// Satisfies TR-FEAT-017.
        /// </summary>
        /// <param name="exitPosition">World position outside the hide spot threshold.</param>
        public void ExitHideSpot(Vector3 exitPosition)
        {
            Teleport(exitPosition, transform.rotation);
            _fsm?.ExitHideSpot(exitPosition);
        }

        /// <summary>
        /// Exits hide spot containment using standardized standoff displacement along portal forward.
        /// Satisfies TR-FEAT-017 (interiorAnchor + portalForward * 1.20m).
        /// </summary>
        public void ExitHideSpotWithStandoff()
        {
            Vector3 exitPos = _fsm != null
                ? _fsm.CurrentPosition + _fsm.PortalForward * _config.HideSpotStandoffDistance
                : transform.position + transform.forward * _config.HideSpotStandoffDistance;
            ExitHideSpot(exitPos);
        }

        /// <summary>
        /// Suspends player input processing (e.g. during cutscenes or capture sequences).
        /// Satisfies AC-P15.
        /// </summary>
        public void SeverControl()
        {
            _isControlSevered = true;
            _fsm?.SeverControl();
        }

        /// <summary>
        /// Restores active player input processing.
        /// </summary>
        public void RestoreControl()
        {
            _isControlSevered = false;
            _fsm?.RestoreControl();
        }
    }
}
