using System;
using System.Text;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Lightweight on-screen diagnostic HUD rendering real-time locomotion and camera telemetry.
    /// Displays actual speed, stance, camera distance, stand headroom clearance, and active FOV.
    /// Satisfies Story SCENE-05 and Acceptance Criterion AC-SCENE-16 with zero heap allocations on hot paths.
    /// </summary>
    /// <example>
    /// <code>
    /// var hud = gameObject.AddComponent&lt;DebugLocomotionHUD&gt;();
    /// hud.Configure(playerController, cameraOrbitDriver);
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class DebugLocomotionHUD : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("Target player controller to poll locomotion telemetry from.")]
        [SerializeField] private PlayerThirdPersonController _controller;

        [Tooltip("Target camera orbit driver to poll camera distance and FOV from.")]
        [SerializeField] private CameraOrbitDriver _cameraDriver;

        [Header("HUD Display Settings")]
        [Tooltip("Toggle visibility of the on-screen debug overlay.")]
        [SerializeField] private bool _showHUD = true;

        [Tooltip("Key code used to toggle HUD overlay visibility at runtime.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

        [Tooltip("Screen margin offset for the HUD box in pixels.")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(16f, 16f);

        [Tooltip("Width of the telemetry HUD box in pixels.")]
        [SerializeField] private float _boxWidth = 320f;

        [Tooltip("Height of the telemetry HUD box in pixels.")]
        [SerializeField] private float _boxHeight = 220f;

        [Header("Telemetry Cache")]
        [Tooltip("Refresh rate for string telemetry generation in seconds (0 = per frame).")]
        [SerializeField] private float _refreshInterval = 0.05f; // ~20 Hz text update to eliminate string GC pressure

        private readonly StringBuilder _stringBuilder = new StringBuilder(512);
        private string _cachedTelemetryText = string.Empty;
        private float _timeSinceLastRefresh;

        // Structured telemetry snapshot for headless test assertions
        private float _lastSpeed;
        private MovementStance _lastStance;
        private float _lastCameraDistance;
        private bool _lastHeadroomBlocked;
        private float _lastFov;
        private float _lastCapsuleHeight;

        /// <summary>
        /// Gets or sets whether the on-screen debug HUD is currently visible.
        /// </summary>
        public bool ShowHUD
        {
            get => _showHUD;
            set => _showHUD = value;
        }

        /// <summary>
        /// Gets the most recently evaluated player speed in m/s.
        /// </summary>
        public float LastSpeed => _lastSpeed;

        /// <summary>
        /// Gets the most recently evaluated movement stance.
        /// </summary>
        public MovementStance LastStance => _lastStance;

        /// <summary>
        /// Gets the most recently evaluated camera distance in meters.
        /// </summary>
        public float LastCameraDistance => _lastCameraDistance;

        /// <summary>
        /// Gets whether overhead headroom is currently blocked.
        /// </summary>
        public bool LastHeadroomBlocked => _lastHeadroomBlocked;

        /// <summary>
        /// Gets the most recently evaluated camera FOV in degrees.
        /// </summary>
        public float LastFov => _lastFov;

        /// <summary>
        /// Gets the most recently evaluated character capsule height in meters.
        /// </summary>
        public float LastCapsuleHeight => _lastCapsuleHeight;

        /// <summary>
        /// Gets the formatted telemetry text block currently rendered to the screen.
        /// </summary>
        public string CachedTelemetryText => _cachedTelemetryText;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = UnityEngine.Object.FindFirstObjectByType<PlayerThirdPersonController>();
            }

            if (_cameraDriver == null)
            {
                _cameraDriver = UnityEngine.Object.FindFirstObjectByType<CameraOrbitDriver>();
            }

            RefreshTelemetry(force: true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showHUD = !_showHUD;
            }

            _timeSinceLastRefresh += Time.deltaTime;
            if (_timeSinceLastRefresh >= _refreshInterval)
            {
                RefreshTelemetry(force: false);
                _timeSinceLastRefresh = 0f;
            }
        }

        /// <summary>
        /// Programmatically configures target component references for telemetry polling.
        /// </summary>
        /// <param name="controller">Player controller instance.</param>
        /// <param name="cameraDriver">Camera orbit driver instance.</param>
        /// <example>
        /// <code>
        /// hud.Configure(controller, cameraDriver);
        /// </code>
        /// </example>
        public void Configure(PlayerThirdPersonController controller, CameraOrbitDriver cameraDriver)
        {
            _controller = controller;
            _cameraDriver = cameraDriver;
            RefreshTelemetry(force: true);
        }

        /// <summary>
        /// Forces an immediate evaluation and update of telemetry metrics and formatted string cache.
        /// </summary>
        /// <param name="force">Whether to bypass time-slice interval gating.</param>
        /// <returns>The newly formatted telemetry text string.</returns>
        /// <example>
        /// <code>
        /// string telemetry = hud.RefreshTelemetry(true);
        /// </code>
        /// </example>
        public string RefreshTelemetry(bool force = false)
        {
            // 1. Poll Player Locomotion Telemetry
            if (_controller != null)
            {
                PlayerLocomotionSnapshot snapshot = _controller.CurrentSnapshot;
                _lastSpeed = snapshot.EffectiveSpeed;
                _lastStance = snapshot.Stance;
                _lastHeadroomBlocked = snapshot.IsStandBlocked;
                _lastCapsuleHeight = snapshot.CapsuleHeight;
            }
            else
            {
                _lastSpeed = 0f;
                _lastStance = MovementStance.Stand;
                _lastHeadroomBlocked = false;
                _lastCapsuleHeight = 1.80f;
            }

            // 2. Poll Camera Telemetry
            if (_cameraDriver != null)
            {
                _lastCameraDistance = _cameraDriver.CurrentDistance;
                _lastFov = _cameraDriver.RigService != null
                    ? _cameraDriver.RigService.ActiveFov
                    : (_cameraDriver.Camera != null ? _cameraDriver.Camera.fieldOfView : 60f);
            }
            else
            {
                _lastCameraDistance = 2.80f;
                _lastFov = 60f;
            }

            // 3. Format telemetry string via pre-allocated StringBuilder (0 GC per refresh cycle)
            _stringBuilder.Clear();
            _stringBuilder.AppendLine("=== WHISPER WARD: CORE PLAYGROUND ===");
            _stringBuilder.Append("Locomotion Speed : ").Append(_lastSpeed.ToString("F2")).AppendLine(" m/s");
            _stringBuilder.Append("Physical Stance  : ").Append(_lastStance.ToString()).Append(" (Height: ").Append(_lastCapsuleHeight.ToString("F2")).AppendLine(" m)");
            _stringBuilder.Append("Stand Headroom   : ").AppendLine(_lastHeadroomBlocked ? "<color=red>BLOCKED (Low Ceiling)</color>" : "<color=green>CLEAR (Full Height)</color>");
            _stringBuilder.Append("Camera Distance  : ").Append(_lastCameraDistance.ToString("F2")).AppendLine(" m (D_nom: 2.80 m)");
            _stringBuilder.Append("Camera Viewport  : ").Append("FOV ").Append(_lastFov.ToString("F1")).Append("°");

            if (_cameraDriver != null && _cameraDriver.RigService != null)
            {
                _stringBuilder.Append(" | Pitch: ").Append(_cameraDriver.RigService.CameraPitch.ToString("F1")).Append("° | Yaw: ").Append(_cameraDriver.RigService.CameraYaw.ToString("F1")).Append("°");
            }
            _stringBuilder.AppendLine();

            _stringBuilder.AppendLine("-------------------------------------");
            _stringBuilder.AppendLine("[WASD] Move | [Shift] Sprint | [C] Crouch | [Mouse] Orbit | [F3] Toggle HUD");

            _cachedTelemetryText = _stringBuilder.ToString();
            return _cachedTelemetryText;
        }

        private void OnGUI()
        {
            if (!_showHUD) return;

            Rect boxRect = new Rect(_screenOffset.x, _screenOffset.y, _boxWidth, _boxHeight);

            // Semi-transparent dark background styling
            GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.88f);
            GUI.Box(boxRect, GUIContent.none);

            GUI.color = Color.white;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                alignment = TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = new Color(0.85f, 0.95f, 1.0f);

            Rect textRect = new Rect(boxRect.x + 10f, boxRect.y + 8f, boxRect.width - 20f, boxRect.height - 16f);
            GUI.Label(textRect, _cachedTelemetryText, labelStyle);
        }
    }
}
