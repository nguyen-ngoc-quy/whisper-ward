using UnityEngine;
using WhisperWard.Core.Contracts;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Runtime driver bridging Unity input devices and camera orientation into PlayerThirdPersonController.
    /// Supports Unity New Input System with automatic fallback, zero garbage allocation per frame,
    /// and visual debug Gizmos for planar facing and velocity vectors.
    /// Satisfies Story SCENE-02 and ADR-0005 / ADR-0006.
    /// </summary>
    [RequireComponent(typeof(PlayerThirdPersonController))]
    public sealed class PlayerRuntimeDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerThirdPersonController _controller;

        [SerializeField]
        private Camera _referenceCamera;

        [SerializeField]
        private Transform _visualTransform;

        [Header("Debug Visualization")]
        [SerializeField]
        private bool _drawHeadingGizmos = true;

        [SerializeField]
        private Color _facingColor = Color.green;

        [SerializeField]
        private Color _velocityColor = Color.yellow;

        private Vector2 _moveInput;
        private bool _isSprintHeld;
        private bool _crouchTriggeredThisFrame;

        /// <summary>
        /// Gets the active third-person controller driven by this component.
        /// </summary>
        public PlayerThirdPersonController Controller => _controller;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<PlayerThirdPersonController>();
            }

            if (_referenceCamera == null)
            {
                _referenceCamera = Camera.main;
            }
        }

        private void Update()
        {
            PollInput();

            // Synchronize camera-relative planar basis
            if (_referenceCamera != null)
            {
                Transform camTransform = _referenceCamera.transform;
                _controller.SetPlanarBasisFromCamera(camTransform.forward, camTransform.up);
            }

            // Build input packet and tick controller
            PlayerInputPacket packet = new PlayerInputPacket(
                _moveInput,
                _isSprintHeld,
                _crouchTriggeredThisFrame,
                isControlSevered: false
            );

            _controller.Tick(packet, Time.deltaTime);

            // Synchronize visual mesh geometry to dynamic capsule dimensions
            if (_visualTransform != null && _controller.Fsm != null)
            {
                float height = _controller.Fsm.CurrentCapsuleHeight;
                _visualTransform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                _visualTransform.localScale = new Vector3(0.6f, height * 0.5f, 0.6f);
            }

            // Reset edge triggers after tick
            _crouchTriggeredThisFrame = false;
        }

        /// <summary>
        /// Explicit setup for tests or runtime bootstrapping.
        /// </summary>
        public void Configure(PlayerThirdPersonController controller, Camera referenceCamera, Transform visualTransform = null)
        {
            _controller = controller;
            _referenceCamera = referenceCamera;
            _visualTransform = visualTransform;
        }

        /// <summary>
        /// Injects artificial input for headless NUnit test execution.
        /// </summary>
        public void InjectInput(Vector2 moveInput, bool sprintHeld, bool crouchTriggered)
        {
            _moveInput = moveInput;
            _isSprintHeld = sprintHeld;
            _crouchTriggeredThisFrame = crouchTriggered;
        }

        /// <summary>
        /// Reads keyboard/gamepad inputs with New Input System support and legacy fallback.
        /// Zero managed allocations.
        /// </summary>
        private void PollInput()
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System polling
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            float x = 0f;
            float y = 0f;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

                _isSprintHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

                if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame)
                {
                    _crouchTriggeredThisFrame = true;
                }
            }

            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    x += stick.x;
                    y += stick.y;
                }

                if (gamepad.leftTrigger.isPressed || gamepad.rightShoulder.isPressed)
                {
                    _isSprintHeld = true;
                }

                if (gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame)
                {
                    _crouchTriggeredThisFrame = true;
                }
            }

            _moveInput = new Vector2(x, y);
#else
            // Legacy input fallback
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(x, y);
            _isSprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
            {
                _crouchTriggeredThisFrame = true;
            }
#endif
        }

        private void OnDrawGizmos()
        {
            if (!_drawHeadingGizmos) return;

            Vector3 pos = transform.position;

            if (_controller != null && _controller.Fsm != null)
            {
                // Draw Facing direction (Green)
                Gizmos.color = _facingColor;
                Vector3 facing = _controller.Fsm.CurrentFacing;
                if (facing.sqrMagnitude > 0.01f)
                {
                    Gizmos.DrawRay(pos + Vector3.up * 0.9f, facing.normalized * 1.5f);
                }

                // Draw Current Velocity vector (Yellow)
                Gizmos.color = _velocityColor;
                Vector3 velocity = _controller.Fsm.CurrentVelocity;
                if (velocity.sqrMagnitude > 0.01f)
                {
                    Gizmos.DrawRay(pos + Vector3.up * 0.5f, velocity);
                }
            }
        }
    }
}
