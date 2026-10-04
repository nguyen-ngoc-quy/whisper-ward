using System;
using UnityEngine;

namespace WhisperWard.Core.Environment
{
    /// <summary>
    /// Represents the occupancy state of an interactive hide spot.
    /// </summary>
    public enum HideSpotState
    {
        Empty = 0,
        Occupied = 1
    }

    /// <summary>
    /// Interactive hide spot component (lockers, vents, closets, crawlspaces).
    /// Implements GDD #5 Player Movement &amp; Hide:
    /// - Sanctuary Invariant: Unwitnessed entry provides complete immunity from vision and capture.
    /// - Witnessed Trap Invariant: Entry under active Chase triggers guard approach to guard_hold and 1.5s dwell capture.
    /// - Pure Pivot: Locks translation (Delta p == 0) while permitting 0 dB rotation.
    /// - Boundary Hysteresis: Suppresses trigger oscillation flapping within 0.06s.
    /// </summary>
    /// <example>
    /// <code>
    /// var hideSpot = gameObject.AddComponent&lt;HideSpot&gt;();
    /// hideSpot.Configure("locker_01", interiorPos, frontAnchor, Vector3.forward);
    /// bool entered = hideSpot.TryEnter(playerTransform, isWitnessed: false);
    /// </code>
    /// </example>
    public sealed class HideSpot : MonoBehaviour
    {
        [Header("Identification & Geometry")]
        [SerializeField] private string _spotId = "spot_01";
        [SerializeField] private Vector3 _interiorPosition = Vector3.zero;
        [SerializeField] private Vector3 _frontAnchor = Vector3.forward;
        [SerializeField] private Vector3 _holdVector = Vector3.forward;
        [SerializeField] private float _guardRadius = 0.40f;

        [Header("Dwell & Verification")]
        [Tooltip("Verification dwell duration at spot-front before capture is committed (GDD t_spotfront_verify = 1.5s).")]
        [SerializeField] private float _dwellVerifyTime = 1.50f;

        [Tooltip("Maximum allowed standoff distance between guard hold and interior position (<= 5.10m).")]
        [SerializeField] private float _maxStandoffDistance = 5.10f;

        [Header("State")]
        [SerializeField] private HideSpotState _state = HideSpotState.Empty;
        [SerializeField] private bool _isWitnessedEntry = false;

        private Transform _currentOccupant;
        private float _dwellTimer = 0f;
        private float _lastTransitionTime = -1f;
        private const float HysteresisWindow = 0.06f; // AC14: 60ms boundary hysteresis

        /// <summary>
        /// Gets the unique identifier for this hide spot.
        /// </summary>
        public string SpotId => _spotId;

        /// <summary>
        /// Gets the authoritative reference position occupied by the hidden player capsule.
        /// </summary>
        public Vector3 InteriorPosition => _interiorPosition;

        /// <summary>
        /// Gets the front entrance anchor position.
        /// </summary>
        public Vector3 FrontAnchor => _frontAnchor;

        /// <summary>
        /// Gets the normalized outward viewing normal vector from the aperture.
        /// </summary>
        public Vector3 HoldVector => _holdVector;

        /// <summary>
        /// Evaluates the guard hold stance coordinate: spot_front_anchor + hold_vector * r_guard.
        /// </summary>
        public Vector3 GuardHoldPosition => _frontAnchor + _holdVector * _guardRadius;

        /// <summary>
        /// Gets the current occupancy state.
        /// </summary>
        public HideSpotState State => _state;

        /// <summary>
        /// Indicates whether entry was witnessed by a chasing guard.
        /// If false, this spot functions as a certified Sanctuary.
        /// </summary>
        public bool IsWitnessedEntry => _isWitnessedEntry;

        /// <summary>
        /// Gets the transform of the current occupant, or null if empty.
        /// </summary>
        public Transform CurrentOccupant => _currentOccupant;

        /// <summary>
        /// Gets the current elapsed dwell verification time during witnessed investigation.
        /// </summary>
        public float DwellTimer => _dwellTimer;

        /// <summary>
        /// Gets the target verification dwell time (default 1.5s).
        /// </summary>
        public float DwellVerifyTime => _dwellVerifyTime;

        /// <summary>
        /// Event fired when a player enters the hide spot.
        /// </summary>
        public event Action<HideSpot, Transform> OnOccupied;

        /// <summary>
        /// Event fired when the player exits the hide spot.
        /// </summary>
        public event Action<HideSpot, Transform> OnVacated;

        /// <summary>
        /// Event fired when a witnessed dwell timer completes, triggering capture.
        /// </summary>
        public event Action<HideSpot, Transform, Transform> OnWitnessedCaptureTriggered;

        /// <summary>
        /// Configures hide spot geometry and parameters.
        /// Zero allocations.
        /// </summary>
        /// <param name="spotId">Unique identifier string.</param>
        /// <param name="interiorPosition">Authored interior capsule coordinate.</param>
        /// <param name="frontAnchor">Entrance aperture world coordinate.</param>
        /// <param name="holdVector">Aperture outward normal vector.</param>
        /// <param name="dwellVerifyTime">Hold verification duration (default 1.5s).</param>
        /// <param name="guardRadius">Guard navigation radius (default 0.40m).</param>
        public void Configure(
            string spotId,
            Vector3 interiorPosition,
            Vector3 frontAnchor,
            Vector3 holdVector,
            float dwellVerifyTime = 1.50f,
            float guardRadius = 0.40f)
        {
            _spotId = spotId;
            _interiorPosition = interiorPosition;
            _frontAnchor = frontAnchor;
            _holdVector = holdVector.sqrMagnitude > 1e-6f ? holdVector.normalized : Vector3.forward;
            _dwellVerifyTime = Mathf.Max(0.1f, dwellVerifyTime);
            _guardRadius = Mathf.Max(0.1f, guardRadius);
            _dwellTimer = 0f;
            _state = HideSpotState.Empty;
            _isWitnessedEntry = false;
            _currentOccupant = null;
        }

        /// <summary>
        /// Validates GDD Formula D1: Stand-off distance between GuardHoldPosition and InteriorPosition
        /// must be &lt;= 5.10m (strictly within certified catch range 5.50m).
        /// </summary>
        public bool ValidateResolvability(out float planarDistance)
        {
            Vector3 guardHold = GuardHoldPosition;
            float dx = guardHold.x - _interiorPosition.x;
            float dz = guardHold.z - _interiorPosition.z;
            planarDistance = Mathf.Sqrt(dx * dx + dz * dz);
            return planarDistance <= _maxStandoffDistance;
        }

        /// <summary>
        /// Attempts to enter the hide spot.
        /// </summary>
        /// <param name="playerTransform">Transform of the entering player.</param>
        /// <param name="isWitnessed">True if a guard has active line-of-sight during Chase; false for Sanctuary.</param>
        /// <param name="currentTime">Current game time for hysteresis evaluation.</param>
        /// <returns>True if entry succeeded; false if occupied or rejected by hysteresis.</returns>
        public bool TryEnter(Transform playerTransform, bool isWitnessed, float currentTime = 0f)
        {
            if (playerTransform == null) return false;
            if (_state == HideSpotState.Occupied) return false;

            // AC14: Boundary hysteresis check (60ms)
            if (_lastTransitionTime >= 0f && (currentTime - _lastTransitionTime) < HysteresisWindow)
            {
                return false;
            }

            _lastTransitionTime = currentTime;
            _currentOccupant = playerTransform;
            _state = HideSpotState.Occupied;
            _isWitnessedEntry = isWitnessed;
            _dwellTimer = 0f;

            // Move player to interior coordinate (locks translation)
            playerTransform.position = _interiorPosition;

            OnOccupied?.Invoke(this, playerTransform);
            return true;
        }

        /// <summary>
        /// Attempts to exit the hide spot.
        /// </summary>
        /// <param name="exitPosition">Output world coordinate for player to exit to.</param>
        /// <param name="currentTime">Current game time for hysteresis evaluation.</param>
        /// <returns>True if exit succeeded; false if not occupied or rejected by hysteresis.</returns>
        public bool TryExit(out Vector3 exitPosition, float currentTime = 0f)
        {
            exitPosition = _frontAnchor;
            if (_state != HideSpotState.Occupied) return false;

            // AC14: Boundary hysteresis check
            if (_lastTransitionTime >= 0f && (currentTime - _lastTransitionTime) < HysteresisWindow)
            {
                return false;
            }

            _lastTransitionTime = currentTime;
            Transform occupant = _currentOccupant;

            if (occupant != null)
            {
                occupant.position = _frontAnchor;
            }

            _currentOccupant = null;
            _state = HideSpotState.Empty;
            _isWitnessedEntry = false;
            _dwellTimer = 0f;

            OnVacated?.Invoke(this, occupant);
            return true;
        }

        /// <summary>
        /// Advances the verification dwell timer when a guard reaches guard_hold during a witnessed entry.
        /// </summary>
        /// <param name="deltaTime">Simulation time delta.</param>
        /// <param name="guardTransform">Transform of the investigating guard.</param>
        /// <param name="guardArrivedAtHold">True if the guard has arrived at or within 0.5m of GuardHoldPosition.</param>
        /// <returns>True if the dwell completed and capture is executed; otherwise false.</returns>
        public bool TickWitnessedDwell(float deltaTime, Transform guardTransform, bool guardArrivedAtHold)
        {
            if (_state != HideSpotState.Occupied || !_isWitnessedEntry)
            {
                _dwellTimer = 0f;
                return false;
            }

            if (!guardArrivedAtHold)
            {
                return false;
            }

            _dwellTimer += deltaTime;
            if (_dwellTimer >= _dwellVerifyTime)
            {
                OnWitnessedCaptureTriggered?.Invoke(this, _currentOccupant, guardTransform);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Immediately resets the spot state (e.g. on scene reload or respawn).
        /// </summary>
        public void ResetState()
        {
            _state = HideSpotState.Empty;
            _isWitnessedEntry = false;
            _currentOccupant = null;
            _dwellTimer = 0f;
            _lastTransitionTime = -1f;
        }
    }
}
