using System;
using UnityEngine;
using WhisperWard.AI.FSM;

namespace WhisperWard.UI.Feedback
{
    /// <summary>
    /// Controls world-space overhead awareness markers and telegraph cues for Guard NPCs.
    /// Manages:
    /// 1. State-to-Marker mapping (Patrol -> None, Investigate -> Question '?', Chase -> Exclamation '!').
    /// 2. Planar billboard alignment facing the main camera.
    /// 3. Telegraph event dispatching for audio stings and VFX.
    /// 4. Zero managed GC allocations per frame.
    /// Conforms to GDD guard-ai-fsm.md (Visual/Audio Requirements lines 584..598) and Story FEEDBACK-01.
    /// </summary>
    public class GuardOverheadMarkerController : MonoBehaviour
    {
        public enum MarkerType
        {
            None,
            Question,
            Exclamation,
            Captured
        }

        [Header("Positioning & Display")]
        [Tooltip("Height offset above guard origin in meters (GDD: 2.20m to clear capsule head).")]
        [SerializeField] private float _heightOffset = 2.20f;

        [Tooltip("Warning Amber color for Investigate state marker (GDD: #FFB800).")]
        [SerializeField] private Color _investigateColor = new Color(1.0f, 0.72f, 0.0f, 1.0f);

        [Tooltip("Alert Red color for Chase state marker (GDD: #FF3333).")]
        [SerializeField] private Color _chaseColor = new Color(1.0f, 0.20f, 0.20f, 1.0f);

        [Tooltip("Captured state color (GDD: #990000).")]
        [SerializeField] private Color _capturedColor = new Color(0.6f, 0.0f, 0.0f, 1.0f);

        [Header("Component References")]
        [SerializeField] private GuardFSMRuntimeController _fsm;
        [SerializeField] private Transform _markerAnchor;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private MarkerType _currentMarkerType = MarkerType.None;
        private Color _currentColor = Color.clear;
        private bool _isInitialized;

        public MarkerType CurrentMarkerType => _currentMarkerType;
        public bool IsVisible => _currentMarkerType != MarkerType.None;
        public Color CurrentColor => _currentColor;
        public float HeightOffset => _heightOffset;

        /// <summary>
        /// Fires when the overhead marker changes type.
        /// </summary>
        public event Action<MarkerType> OnMarkerChanged;

        /// <summary>
        /// Fires when an escalation state transition triggers an audio/visual telegraph.
        /// </summary>
        public event Action<MarkerType, GuardFSMRuntimeController.GuardState> OnTelegraphTriggered;

        private void Awake()
        {
            if (!_isInitialized)
            {
                InitializeComponents();
            }
        }

        private void Start()
        {
            BindFsmEvents();
            if (_fsm != null)
            {
                UpdateMarkerForState(_fsm.CurrentState);
            }
        }

        private void OnDestroy()
        {
            UnbindFsmEvents();
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    UpdateBillboard(cam.transform.position);
                }
            }
        }

        private void InitializeComponents()
        {
            if (_fsm == null)
            {
                _fsm = GetComponentInParent<GuardFSMRuntimeController>();
            }

            if (_markerAnchor == null)
            {
                _markerAnchor = transform;
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Explicit dependency injection and initialization for runtime and testing.
        /// Zero allocations.
        /// </summary>
        public void Configure(
            GuardFSMRuntimeController fsm,
            Transform markerAnchor = null,
            SpriteRenderer spriteRenderer = null,
            float heightOffset = 2.20f)
        {
            UnbindFsmEvents();

            _fsm = fsm;
            _markerAnchor = markerAnchor != null ? markerAnchor : transform;
            _spriteRenderer = spriteRenderer;
            _heightOffset = Mathf.Max(0.5f, heightOffset);

            _isInitialized = true;
            BindFsmEvents();

            if (_fsm != null)
            {
                UpdateMarkerForState(_fsm.CurrentState);
            }
            else
            {
                SetMarkerType(MarkerType.None);
            }
        }

        private void BindFsmEvents()
        {
            if (_fsm != null)
            {
                _fsm.OnStateChanged += HandleFsmStateChanged;
            }
        }

        private void UnbindFsmEvents()
        {
            if (_fsm != null)
            {
                _fsm.OnStateChanged -= HandleFsmStateChanged;
            }
        }

        private void HandleFsmStateChanged(GuardFSMRuntimeController.GuardState previous, GuardFSMRuntimeController.GuardState current)
        {
            UpdateMarkerForState(current);
        }

        /// <summary>
        /// Updates the marker type, color, and telegraph events for a given FSM state.
        /// Zero allocations.
        /// </summary>
        public void UpdateMarkerForState(GuardFSMRuntimeController.GuardState state)
        {
            MarkerType newMarker;
            Color newColor;

            switch (state)
            {
                case GuardFSMRuntimeController.GuardState.Patrol:
                    newMarker = MarkerType.None;
                    newColor = Color.clear;
                    break;

                case GuardFSMRuntimeController.GuardState.Investigate:
                    newMarker = MarkerType.Question;
                    newColor = _investigateColor;
                    break;

                case GuardFSMRuntimeController.GuardState.Chase:
                    newMarker = MarkerType.Exclamation;
                    newColor = _chaseColor;
                    break;

                case GuardFSMRuntimeController.GuardState.Captured:
                    newMarker = MarkerType.Captured;
                    newColor = _capturedColor;
                    break;

                default:
                    newMarker = MarkerType.None;
                    newColor = Color.clear;
                    break;
            }

            SetMarkerType(newMarker, newColor);

            if (newMarker != MarkerType.None)
            {
                OnTelegraphTriggered?.Invoke(newMarker, state);
            }
        }

        private void SetMarkerType(MarkerType type, Color? colorOverride = null)
        {
            _currentMarkerType = type;
            _currentColor = colorOverride.HasValue ? colorOverride.Value : Color.clear;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = (_currentMarkerType != MarkerType.None);
                _spriteRenderer.color = _currentColor;
            }

            OnMarkerChanged?.Invoke(_currentMarkerType);
        }

        /// <summary>
        /// Rotates the marker anchor to face the camera position (planar billboard).
        /// Zero allocations.
        /// </summary>
        public void UpdateBillboard(Vector3 cameraPosition)
        {
            if (_markerAnchor == null) return;

            Vector3 toCamera = cameraPosition - _markerAnchor.position;
            toCamera.y = 0f; // Planar billboard (avoid weird vertical tilt)

            if (toCamera.sqrMagnitude > 0.001f)
            {
                _markerAnchor.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }
    }
}
