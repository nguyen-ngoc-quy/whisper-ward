using System;
using UnityEngine;

namespace WhisperWard.Core.Environment
{
    /// <summary>
    /// Physical barrier gate that blocks passage until unlocked with a valid Keycard.
    /// </summary>
    public class LockedSecurityGate : MonoBehaviour
    {
        [Header("Security Configuration")]
        [SerializeField] private string _requiredObjectiveId = "Keycard_SecurityLevel1";
        [SerializeField] private float _interactionDistance = 2.50f;

        [Header("Object References")]
        [SerializeField] private Collider _barrierCollider;
        [SerializeField] private GameObject _gateDoorVisual;

        private bool _isUnlocked;
        private bool _isOpen;

        /// <summary>Global notification dispatched when any gate unlocks.</summary>
        public static event Action OnAnyGateUnlocked;

        /// <summary>Dispatched when this gate unlocks and opens.</summary>
        public event Action OnGateUnlocked;

        /// <summary>Dispatched when an unlock attempt is rejected.</summary>
        public event Action OnGateUnlockFailed;

        /// <summary>Gets the required objective identifier to unlock this gate.</summary>
        public string RequiredObjectiveId => _requiredObjectiveId;

        /// <summary>Gets the maximum allowed interaction distance.</summary>
        public float InteractionDistance => _interactionDistance;

        /// <summary>Gets whether the gate has been unlocked.</summary>
        public bool IsUnlocked => _isUnlocked;

        /// <summary>Gets whether the gate is currently open and passable.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Gets the blocking barrier collider.</summary>
        public Collider BarrierCollider => _barrierCollider;

        /// <summary>Gets the door visual representation.</summary>
        public GameObject GateDoorVisual => _gateDoorVisual;

        private void Awake()
        {
            if (_barrierCollider == null)
            {
                _barrierCollider = GetComponent<Collider>();
            }

            if (_gateDoorVisual == null && transform.childCount > 0)
            {
                _gateDoorVisual = transform.GetChild(0).gameObject;
            }
        }

        /// <summary>
        /// Attempts to unlock and open the gate using the player's keycard state.
        /// </summary>
        /// <param name="hasKeycard">True if the player currently holds the required keycard.</param>
        /// <param name="interactor">The GameObject attempting the unlock.</param>
        /// <param name="bypassDistanceCheck">True if called directly without spatial validation.</param>
        /// <returns>True if unlocked and opened; false if rejected.</returns>
        public bool TryUnlockAndOpen(bool hasKeycard, GameObject interactor = null, bool bypassDistanceCheck = false)
        {
            if (_isOpen)
            {
                return true; // Idempotent
            }

            if (interactor != null && !bypassDistanceCheck)
            {
                float sqrDist = (interactor.transform.position - transform.position).sqrMagnitude;
                float maxAllowedSqr = (_interactionDistance + 0.05f) * (_interactionDistance + 0.05f);
                if (sqrDist > maxAllowedSqr)
                {
                    OnGateUnlockFailed?.Invoke();
                    return false;
                }
            }

            if (!hasKeycard)
            {
                OnGateUnlockFailed?.Invoke();
                return false;
            }

            _isUnlocked = true;
            _isOpen = true;

            if (_barrierCollider != null)
            {
                _barrierCollider.enabled = false;
            }

            if (_gateDoorVisual != null)
            {
                _gateDoorVisual.SetActive(false); // Simulates door opening by clearing passage
            }

            OnGateUnlocked?.Invoke();
            OnAnyGateUnlocked?.Invoke();

            return true;
        }

        /// <summary>
        /// Resets the gate to locked and closed state for a new mission attempt.
        /// </summary>
        public void ResetGate()
        {
            _isUnlocked = false;
            _isOpen = false;

            if (_barrierCollider != null)
            {
                _barrierCollider.enabled = true;
            }

            if (_gateDoorVisual != null)
            {
                _gateDoorVisual.SetActive(true);
            }
        }
    }
}
