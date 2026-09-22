using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Pure C# Deterministic Locomotion State Machine implementing ADR-0006, GDD #11, and control-manifest.md.
    /// Manages kinematic velocity slewing, camera-relative planar basis transformation,
    /// rear-damped facing orientation, feet-anchored continuous capsule scaling, stand headroom probe gating,
    /// and HideSpot containment states. Zero-allocation in all hot paths (0 B GC).
    /// </summary>
    public sealed class PlayerLocomotionFSM
    {
        private readonly PlayerConfig _config;

        private LocomotionState _currentState;
        private MovementStance _currentStance;
        private bool _isCrouchLatched;
        private bool _isStandBlocked;
        private bool _isInHideSpot;
        private bool _isGrounded;

        private float _effectiveSpeed;
        private float _targetSpeed;
        private Vector3 _moveDirection;
        private Vector3 _currentFacing;
        private Vector3 _currentPosition;
        private Vector3 _currentVelocity;

        private AnimState _currentAnimState;
        private float _speedRatio;
        private float _gamma;

        private int _transitionCountThisTick;
        private int _totalTransitionCount;

        private IStandHeadroomProbe _headroomProbe;
        private bool _isControlSevered;
        private bool _hasCapturedBaseline;
        private PlayerLocomotionSnapshot _frozenSnapshot;
        private Vector3 _interiorAnchor;
        private Vector3 _portalForward = Vector3.forward;

        /// <summary>
        /// Active locomotion state.
        /// </summary>
        public LocomotionState CurrentState => _currentState;

        /// <summary>
        /// Active movement stance.
        /// </summary>
        public MovementStance CurrentStance => _currentStance;

        /// <summary>
        /// Active discrete animation state variant.
        /// </summary>
        public AnimState CurrentAnimState => _currentAnimState;

        /// <summary>
        /// Speed ratio relative to active variant cruise speed [0, 1].
        /// </summary>
        public float SpeedRatio => _speedRatio;

        /// <summary>
        /// Continuous crouch posture blend weight [0, 1] (0 = crouched, 1 = standing).
        /// </summary>
        public float Gamma => _gamma;

        /// <summary>
        /// Effective horizontal speed magnitude in m/s.
        /// </summary>
        public float EffectiveSpeed => _effectiveSpeed;

        /// <summary>
        /// Target horizontal speed magnitude in m/s toward which the slew accelerates.
        /// </summary>
        public float TargetSpeed => _targetSpeed;

        /// <summary>
        /// Normalized 3D planar move direction vector in world coordinates.
        /// </summary>
        public Vector3 MoveDirection => _moveDirection;

        /// <summary>
        /// Normalized horizontal facing vector of the character model.
        /// </summary>
        public Vector3 CurrentFacing => _currentFacing;

        /// <summary>
        /// Current world position of the player capsule base.
        /// </summary>
        public Vector3 CurrentPosition => _currentPosition;

        /// <summary>
        /// Current 3D linear velocity vector.
        /// </summary>
        public Vector3 CurrentVelocity => _currentVelocity;

        /// <summary>
        /// True if the crouch latch is actively toggled on.
        /// </summary>
        public bool IsCrouchLatched => _isCrouchLatched;

        /// <summary>
        /// True if an overhead ceiling obstacle prevents standing upright.
        /// </summary>
        public bool IsStandBlocked => _isStandBlocked;

        /// <summary>
        /// True if character is grounded.
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// True if character is currently contained inside a HideSpot volume.
        /// </summary>
        public bool IsInHideSpot => _isInHideSpot;

        /// <summary>
        /// Outward normal vector from the hide spot opening when contained.
        /// </summary>
        public Vector3 PortalForward => _portalForward;

        /// <summary>
        /// True if player input control is severed (e.g. during capture sequence or cutscene).
        /// </summary>
        public bool IsControlSevered => _isControlSevered;

        /// <summary>
        /// Gets the injected stand headroom query probe.
        /// </summary>
        public IStandHeadroomProbe HeadroomProbe => _headroomProbe;

        /// <summary>
        /// Current continuous vertical height of the capsule based on gamma blend.
        /// Satisfies AC-P13 and ADR-0006.
        /// </summary>
        public float CurrentCapsuleHeight => _config.CrouchCapsuleHeight + (_config.StandCapsuleHeight - _config.CrouchCapsuleHeight) * _gamma;

        /// <summary>
        /// Current capsule center local coordinate co-moved with height to maintain feet ground anchoring.
        /// Satisfies AC-P13: center.y = CurrentCapsuleHeight / 2.
        /// </summary>
        public Vector3 CapsuleCenter => new Vector3(0f, CurrentCapsuleHeight * 0.5f, 0f);

        /// <summary>
        /// Number of state transitions resolved in the most recent simulation tick.
        /// </summary>
        public int TransitionCountThisTick => _transitionCountThisTick;

        /// <summary>
        /// Total number of state transitions resolved over the lifetime of this state machine.
        /// </summary>
        public int TotalTransitionCount => _totalTransitionCount;

        /// <summary>
        /// Configuration parameters driving this FSM.
        /// </summary>
        public PlayerConfig Config => _config;

        /// <summary>
        /// Initializes a new locomotion state machine with the specified configuration.
        /// </summary>
        /// <param name="config">Tunings and kinematic invariants.</param>
        public PlayerLocomotionFSM(PlayerConfig config = null)
        {
            _config = config ?? PlayerConfig.CreateDefault();
            _currentState = LocomotionState.Idle;
            _currentStance = MovementStance.Standing;
            _isCrouchLatched = false;
            _isStandBlocked = false;
            _isInHideSpot = false;
            _isGrounded = true;
            _effectiveSpeed = 0f;
            _targetSpeed = 0f;
            _moveDirection = Vector3.zero;
            _currentFacing = Vector3.forward;
            _currentPosition = Vector3.zero;
            _currentVelocity = Vector3.zero;
            _currentAnimState = AnimState.IdleStanding;
            _speedRatio = 0f;
            _gamma = 1.0f;
            _transitionCountThisTick = 0;
            _totalTransitionCount = 0;
            _isControlSevered = false;
            _hasCapturedBaseline = false;
            _interiorAnchor = Vector3.zero;
            _portalForward = Vector3.forward;
        }

        /// <summary>
        /// Injects an external stand headroom query probe for headless testing and decoupled physics queries.
        /// Satisfies GDD #11 Section H.0.13.
        /// </summary>
        /// <param name="probe">Headroom query implementation.</param>
        public void SetHeadroomProbe(IStandHeadroomProbe probe)
        {
            _headroomProbe = probe;
        }

        /// <summary>
        /// Suspends player input processing, freezing the pre-tick baseline snapshot.
        /// Satisfies AC-P15.
        /// </summary>
        public void SeverControl()
        {
            _isControlSevered = true;
        }

        /// <summary>
        /// Restores active player input processing.
        /// </summary>
        public void RestoreControl()
        {
            _isControlSevered = false;
            _hasCapturedBaseline = false;
        }

        /// <summary>
        /// Direct setter for gamma blend weight (used for mid-blend boundary verification in testing).
        /// </summary>
        /// <param name="gamma">Blend value in [0, 1].</param>
        public void OverrideGamma(float gamma)
        {
            _gamma = Mathf.Clamp01(gamma);
            _currentStance = (_gamma >= 0.5f) ? MovementStance.Standing : MovementStance.Crouched;
        }

        /// <summary>
        /// Advances the locomotion simulation deterministically by one tick.
        /// Fully non-allocating (0 B GC).
        /// </summary>
        /// <param name="input">Input packet for this tick.</param>
        /// <param name="planarBasisForward">Authoritative horizontal forward basis vector.</param>
        /// <param name="planarBasisRight">Authoritative horizontal right basis vector.</param>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Tick(
            PlayerInputPacket input,
            Vector3 planarBasisForward,
            Vector3 planarBasisRight,
            float deltaTime)
        {
            _transitionCountThisTick = 0;

            // AC-P15: Capture / Sever Ingress - Freeze pre-tick baseline snapshot across all ticks
            bool controlSevered = input.IsControlSevered || _isControlSevered;
            if (controlSevered)
            {
                if (!_hasCapturedBaseline)
                {
                    _frozenSnapshot = GenerateSnapshot();
                    _hasCapturedBaseline = true;
                }
                return;
            }
            _hasCapturedBaseline = false;

            // TR-FEAT-015 / TR-FEAT-016: HideSpot Containment & Pure-Pivot Translational Lock
            if (_isInHideSpot)
            {
                _currentPosition = _interiorAnchor;
                _effectiveSpeed = 0f;
                _targetSpeed = 0f;
                _currentVelocity = Vector3.zero;
                _moveDirection = Vector3.zero;

                // Pure pivot yaw viewing clamp: [-60 deg, +60 deg] relative to _portalForward
                Vector2 rawLookMove = input.MoveAxes;
                if (rawLookMove.sqrMagnitude > (_config.MinInputThreshold * _config.MinInputThreshold))
                {
                    Vector2 normalizedLook = rawLookMove.normalized;
                    Vector3 desiredHeading = (planarBasisRight * normalizedLook.x + planarBasisForward * normalizedLook.y);
                    desiredHeading.y = 0f;
                    if (desiredHeading.sqrMagnitude > 1e-6f)
                    {
                        desiredHeading.Normalize();
                        float signedAngle = Vector3.SignedAngle(_portalForward, desiredHeading, Vector3.up);
                        float clampedAngle = Mathf.Clamp(signedAngle, -_config.HideSpotYawClampDegrees, _config.HideSpotYawClampDegrees);
                        Vector3 clampedHeading = Quaternion.AngleAxis(clampedAngle, Vector3.up) * _portalForward;

                        float maxAngleDelta = _config.TurnRate * deltaTime;
                        float currentAngle = Vector3.SignedAngle(_currentFacing, clampedHeading, Vector3.up);
                        float step = Mathf.Min(maxAngleDelta, Mathf.Abs(currentAngle)) * Mathf.Sign(currentAngle);
                        Vector3 newFacing = Quaternion.AngleAxis(step, Vector3.up) * _currentFacing;
                        newFacing.y = 0f;
                        if (newFacing.sqrMagnitude > 1e-6f)
                        {
                            _currentFacing = newFacing.normalized;
                        }
                    }
                }

                UpdateAnimationFeed(false, 0f);
                return;
            }

            // 1. Process Stance Toggle & Headroom Constraints (AC-P12, AC-P13, GDD #11 F4)
            if (input.CrouchToggleEdge)
            {
                _isCrouchLatched = !_isCrouchLatched;
            }

            bool targetStanding = !_isCrouchLatched;

            if (targetStanding)
            {
                if (_gamma < 1.0f)
                {
                    if (_headroomProbe != null)
                    {
                        bool clear = _headroomProbe.QueryStandClearance(_currentPosition, out _);
                        _isStandBlocked = !clear;
                    }

                    if (_isStandBlocked)
                    {
                        // Ceiling obstacle blocks standing: gamma freezes! (AC-P12)
                    }
                    else
                    {
                        // Clearance granted: gamma blends monotonically toward 1.0 (F4: delta_t / accel_time)
                        float deltaGamma = deltaTime / _config.AccelTime;
                        _gamma = Mathf.Min(1.0f, _gamma + deltaGamma);
                    }
                }
                else
                {
                    _isStandBlocked = false;
                }
            }
            else
            {
                // Crouching is unconditionally allowed
                _isStandBlocked = false;
                float deltaGamma = deltaTime / _config.AccelTime;
                _gamma = Mathf.Max(0.0f, _gamma - deltaGamma);
            }

            // AC-P13: Binary stance flip strictly at gamma = 0.5
            _currentStance = (_gamma >= 0.5f) ? MovementStance.Standing : MovementStance.Crouched;

            // 2. Diagonal Normalization & Direction Resolution (AC-P1, AC-P23)
            Vector2 rawMove = input.MoveAxes;
            float sqrMagnitude = rawMove.sqrMagnitude;
            bool hasMovementInput = sqrMagnitude > (_config.MinInputThreshold * _config.MinInputThreshold);

            if (hasMovementInput)
            {
                Vector2 normalizedMove = rawMove.normalized;
                _moveDirection = (planarBasisRight * normalizedMove.x + planarBasisForward * normalizedMove.y);
                float dirSqrMag = _moveDirection.sqrMagnitude;
                if (dirSqrMag > 1e-8f)
                {
                    _moveDirection.Normalize();
                }
                else
                {
                    _moveDirection = Vector3.zero;
                    hasMovementInput = false;
                }
            }
            else
            {
                // AC-P23: Zero-vector NaN Guard — do not compute undefined dot product
                _moveDirection = Vector3.zero;
            }

            // 3. State Resolution with Latch & Stand Blocked Priority (AC-P8, AC-P12, AC-P20)
            LocomotionState previousState = _currentState;
            LocomotionState nextState;

            if (!hasMovementInput)
            {
                nextState = LocomotionState.Idle;
            }
            else if (_isCrouchLatched || _isStandBlocked || _currentStance == MovementStance.Crouched)
            {
                // Crouch latch OR stand blocked strictly clamps speed to Crouch speed!
                nextState = LocomotionState.Crouch;
            }
            else if (input.IsRunHeld)
            {
                nextState = LocomotionState.Run;
            }
            else
            {
                nextState = LocomotionState.Walk;
            }

            if (nextState != previousState)
            {
                _currentState = nextState;
                _transitionCountThisTick = 1; // AC-P20: exactly 1 transition event per tick
                _totalTransitionCount++;
            }

            // 4. Target Speed Evaluation & Backpedal Penalty (AC-P1, AC-P2)
            float baseSpeed = 0f;
            switch (_currentState)
            {
                case LocomotionState.Idle:
                case LocomotionState.InHideSpot:
                    baseSpeed = 0f;
                    break;
                case LocomotionState.Crouch:
                    baseSpeed = _config.CrouchSpeed;
                    break;
                case LocomotionState.Walk:
                    baseSpeed = _config.WalkSpeed;
                    break;
                case LocomotionState.Run:
                    baseSpeed = _config.RunSpeed;
                    break;
            }

            float backpedalFactor = 1.0f;
            float dot = 0f;
            if (hasMovementInput && _moveDirection != Vector3.zero)
            {
                dot = Vector3.Dot(_moveDirection, _currentFacing);
                if (dot < _config.BackpedalThreshold)
                {
                    backpedalFactor = _config.BackpedalFactor;
                }
            }

            _targetSpeed = baseSpeed * backpedalFactor;

            // 5. Deterministic Linear Slew (AC-P5, AC-P6, AC-P7)
            float maxDeltaSpeed = _config.MaxAcceleration * deltaTime;
            _effectiveSpeed = Mathf.MoveTowards(_effectiveSpeed, _targetSpeed, maxDeltaSpeed);

            // AC-P17: Structural Speed Ceiling
            if (_effectiveSpeed > _config.RunSpeed + 1e-4f)
            {
                _effectiveSpeed = _config.RunSpeed;
            }

            // 6. Update 3D Linear Velocity Vector
            if (_moveDirection != Vector3.zero && _effectiveSpeed > 1e-5f)
            {
                _currentVelocity = _moveDirection * _effectiveSpeed;
            }
            else
            {
                _currentVelocity = Vector3.zero;
            }

            // 7. Update Facing Orientation with Proportional Rear Damping (AC-P3, AC-P4, AC-P25)
            if (hasMovementInput && _moveDirection != Vector3.zero)
            {
                float w = _config.ComputeRearDampWeight(dot);
                float maxAngleDelta = _config.TurnRate * w * deltaTime;

                float signedAngle = Vector3.SignedAngle(_currentFacing, _moveDirection, Vector3.up);
                float angleDelta = Mathf.Min(maxAngleDelta, Mathf.Abs(signedAngle)) * Mathf.Sign(signedAngle);
                Quaternion rot = Quaternion.AngleAxis(angleDelta, Vector3.up);
                Vector3 newFacing = (rot * _currentFacing);
                newFacing.y = 0f;
                if (newFacing.sqrMagnitude > 1e-6f)
                {
                    _currentFacing = newFacing.normalized;
                }
            }
            // AC-P25: When !hasMovementInput, _currentFacing remains bit-identical (zero autonomous rotation).

            // 8. Update Animation Feed (AC-P24)
            UpdateAnimationFeed(hasMovementInput, dot);
        }

        private void ApplyDeceleration(float deltaTime)
        {
            _targetSpeed = 0f;
            float maxDeltaSpeed = _config.MaxAcceleration * deltaTime;
            _effectiveSpeed = Mathf.MoveTowards(_effectiveSpeed, 0f, maxDeltaSpeed);
            _currentVelocity = Vector3.zero;

            if (_currentState != LocomotionState.InHideSpot && _currentState != LocomotionState.Idle)
            {
                if (_effectiveSpeed <= 1e-5f)
                {
                    _currentState = LocomotionState.Idle;
                    _transitionCountThisTick = 1;
                    _totalTransitionCount++;
                }
            }

            UpdateAnimationFeed(false, 0f);
        }

        private void UpdateAnimationFeed(bool hasMovementInput, float dot)
        {
            if (!hasMovementInput || _currentState == LocomotionState.Idle)
            {
                _currentAnimState = (_currentStance == MovementStance.Crouched)
                    ? AnimState.IdleCrouched
                    : AnimState.IdleStanding;
                _speedRatio = 0.0f;
                return;
            }

            if (_currentState == LocomotionState.InHideSpot)
            {
                _currentAnimState = AnimState.IdleCrouched;
                _speedRatio = 0.0f;
                return;
            }

            bool isBackward = dot < _config.BackpedalThreshold;
            float cruiseSpeed;

            switch (_currentState)
            {
                case LocomotionState.Crouch:
                    _currentAnimState = isBackward ? AnimState.CrouchBack : AnimState.CrouchFwd;
                    cruiseSpeed = isBackward ? _config.CrouchSpeed * _config.BackpedalFactor : _config.CrouchSpeed;
                    break;

                case LocomotionState.Walk:
                    _currentAnimState = isBackward ? AnimState.WalkBack : AnimState.WalkFwd;
                    cruiseSpeed = isBackward ? _config.WalkSpeed * _config.BackpedalFactor : _config.WalkSpeed;
                    break;

                case LocomotionState.Run:
                    _currentAnimState = isBackward ? AnimState.RunBack : AnimState.RunFwd;
                    cruiseSpeed = isBackward ? _config.RunSpeed * _config.BackpedalFactor : _config.RunSpeed;
                    break;

                default:
                    _currentAnimState = AnimState.IdleStanding;
                    cruiseSpeed = 0f;
                    break;
            }

            _speedRatio = cruiseSpeed > 0f ? Mathf.Clamp01(_effectiveSpeed / cruiseSpeed) : 0f;
        }

        /// <summary>
        /// Teleports the state machine to a specific position and facing orientation, resetting velocity.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _currentPosition = position;
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f)
            {
                _currentFacing = forward.normalized;
            }
            _effectiveSpeed = 0f;
            _targetSpeed = 0f;
            _currentVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
            _currentState = LocomotionState.Idle;
        }

        /// <summary>
        /// Authoritative respawn reset restoring standing posture, spawn facing, and clear states.
        /// Satisfies AC-P16 and C8.
        /// </summary>
        /// <param name="spawnPosition">Target spawn coordinates.</param>
        /// <param name="spawnRotation">Target spawn orientation.</param>
        public void Respawn(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            _currentPosition = spawnPosition;
            Vector3 forward = spawnRotation * Vector3.forward;
            forward.y = 0f;
            _currentFacing = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            _currentState = LocomotionState.Idle;
            _currentStance = MovementStance.Standing;
            _isCrouchLatched = false;
            _isStandBlocked = false;
            _isInHideSpot = false;
            _isControlSevered = false;
            _hasCapturedBaseline = false;
            _effectiveSpeed = 0f;
            _targetSpeed = 0f;
            _currentVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
            _gamma = 1.0f;
            _currentAnimState = AnimState.IdleStanding;
            _speedRatio = 0f;
            _transitionCountThisTick = 0; // zero spurious transition events emitted
        }

        /// <summary>
        /// Enters designated HideSpot volume with pure-pivot translational lock.
        /// Satisfies TR-FEAT-015 and TR-FEAT-016.
        /// </summary>
        public void EnterHideSpot(Vector3 interiorAnchor, Vector3 portalForward)
        {
            _isInHideSpot = true;
            _interiorAnchor = interiorAnchor;
            _currentPosition = interiorAnchor;
            portalForward.y = 0f;
            _portalForward = portalForward.sqrMagnitude > 1e-6f ? portalForward.normalized : Vector3.forward;
            _currentFacing = _portalForward;
            _currentState = LocomotionState.InHideSpot;
            _effectiveSpeed = 0f;
            _targetSpeed = 0f;
            _currentVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
            _isCrouchLatched = true;
            _currentStance = MovementStance.Crouched;
            _gamma = 0.0f;
            _currentAnimState = AnimState.IdleCrouched;
            _speedRatio = 0.0f;
        }

        /// <summary>
        /// Exits designated HideSpot volume to specified exit coordinates.
        /// Satisfies TR-FEAT-017.
        /// </summary>
        public void ExitHideSpot(Vector3 exitPosition)
        {
            _isInHideSpot = false;
            _currentPosition = exitPosition;
            _currentState = LocomotionState.Idle;
            _effectiveSpeed = 0f;
            _targetSpeed = 0f;
            _currentVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
        }

        /// <summary>
        /// Updates the authoritative character facing direction.
        /// </summary>
        public void SetFacing(Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-6f)
            {
                _currentFacing = facing.normalized;
            }
        }

        /// <summary>
        /// Sets ceiling headroom obstruction status.
        /// </summary>
        public void SetStandBlocked(bool isBlocked)
        {
            _isStandBlocked = isBlocked;
            if (_isStandBlocked)
            {
                _currentStance = MovementStance.Crouched;
            }
        }

        /// <summary>
        /// Sets floor grounding status.
        /// </summary>
        public void SetGrounded(bool isGrounded)
        {
            _isGrounded = isGrounded;
        }

        /// <summary>
        /// Updates current world position tracking.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            _currentPosition = position;
        }

        /// <summary>
        /// Direct setter for effective speed (used in testing specific acceleration mid-points).
        /// </summary>
        public void OverrideEffectiveSpeed(float speed)
        {
            _effectiveSpeed = Mathf.Clamp(speed, 0f, _config.RunSpeed);
        }

        /// <summary>
        /// Calculates camera-relative horizontal planar basis vectors with pitch degeneracy fallback.
        /// Fully non-allocating static utility adhering to AC-P19 and ADR-0006.
        /// </summary>
        /// <param name="camForward">Raw camera forward vector.</param>
        /// <param name="camUp">Raw camera up vector.</param>
        /// <param name="planarForward">Output normalized horizontal forward basis vector.</param>
        /// <param name="planarRight">Output normalized horizontal right basis vector.</param>
        public static void CalculatePlanarBasis(
            Vector3 camForward,
            Vector3 camUp,
            out Vector3 planarForward,
            out Vector3 planarRight)
        {
            Vector3 proj = Vector3.ProjectOnPlane(camForward, Vector3.up);
            if (proj.sqrMagnitude >= 1e-8f)
            {
                planarForward = proj.normalized;
            }
            else
            {
                Vector3 upProj = Vector3.ProjectOnPlane(camUp, Vector3.up);
                if (upProj.sqrMagnitude >= 1e-8f)
                {
                    planarForward = upProj.normalized;
                }
                else
                {
                    planarForward = Vector3.forward;
                }
            }

            planarRight = Vector3.Cross(Vector3.up, planarForward).normalized;
        }

        /// <summary>
        /// Generates an immutable zero-allocation animation feed snapshot.
        /// </summary>
        public PlayerAnimationFeed GenerateAnimationFeed()
        {
            return new PlayerAnimationFeed(_currentAnimState, _speedRatio, _gamma);
        }

        /// <summary>
        /// Generates an immutable zero-allocation snapshot of current locomotion parameters.
        /// </summary>
        public PlayerLocomotionSnapshot GenerateSnapshot()
        {
            if (_isControlSevered && _hasCapturedBaseline)
            {
                return _frozenSnapshot;
            }

            float capsuleHeight = _config.CrouchCapsuleHeight + (_config.StandCapsuleHeight - _config.CrouchCapsuleHeight) * _gamma;

            return new PlayerLocomotionSnapshot(
                _currentPosition,
                _currentVelocity,
                _currentFacing,
                _currentState,
                _currentStance,
                _effectiveSpeed,
                capsuleHeight,
                _isStandBlocked,
                _isGrounded);
        }
    }
}
