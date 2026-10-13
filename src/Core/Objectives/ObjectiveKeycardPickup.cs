using System;
using UnityEngine;

namespace WhisperWard.Core.Objectives
{
    /// <summary>
    /// Interactive world-space objective item (e.g. Keycard / Data Drive) that
    /// the player can collect to progress the mission lifecycle.
    /// </summary>
    public class ObjectiveKeycardPickup : MonoBehaviour
    {
        [Header("Objective Configuration")]
        [SerializeField] private string _objectiveId = "Keycard_SecurityLevel1";
        [SerializeField] private float _interactionRadius = 2.00f;

        [Header("Object References")]
        [SerializeField] private GameObject _visualModel;
        [SerializeField] private Collider _interactionCollider;

        private bool _isAcquired;

        /// <summary>Global notification dispatched when any objective item is acquired.</summary>
        public static event Action<string> OnAnyObjectiveAcquired;

        /// <summary>Instance notification dispatched when this specific item is acquired.</summary>
        public event Action<string> OnObjectiveAcquired;

        /// <summary>Gets the unique objective identifier for this pickup.</summary>
        public string ObjectiveId => _objectiveId;

        /// <summary>Gets the maximum allowed interaction distance.</summary>
        public float InteractionRadius => _interactionRadius;

        /// <summary>Gets whether this objective item has been acquired.</summary>
        public bool IsAcquired => _isAcquired;

        /// <summary>Gets the configured visual model GameObject.</summary>
        public GameObject VisualModel => _visualModel;

        /// <summary>Gets the configured interaction collider.</summary>
        public Collider InteractionCollider => _interactionCollider;

        private void Awake()
        {
            if (_visualModel == null && transform.childCount > 0)
            {
                _visualModel = transform.GetChild(0).gameObject;
            }

            if (_interactionCollider == null)
            {
                _interactionCollider = GetComponent<Collider>();
            }
        }

        /// <summary>
        /// Attempts to collect this objective item on behalf of an interacting GameObject.
        /// </summary>
        /// <param name="interactor">The actor attempting pickup (typically the player).</param>
        /// <param name="bypassDistanceCheck">True if triggered by physical collision/trigger.</param>
        /// <returns>True if pickup was successfully completed; false if invalid or already taken.</returns>
        public bool TryPickup(GameObject interactor, bool bypassDistanceCheck = false)
        {
            if (_isAcquired || interactor == null)
            {
                return false;
            }

            if (!bypassDistanceCheck)
            {
                float sqrDist = (interactor.transform.position - transform.position).sqrMagnitude;
                float maxAllowedSqr = (_interactionRadius + 0.05f) * (_interactionRadius + 0.05f);
                if (sqrDist > maxAllowedSqr)
                {
                    return false;
                }
            }

            _isAcquired = true;

            if (_visualModel != null)
            {
                _visualModel.SetActive(false);
            }

            if (_interactionCollider != null)
            {
                _interactionCollider.enabled = false;
            }

            OnObjectiveAcquired?.Invoke(_objectiveId);
            OnAnyObjectiveAcquired?.Invoke(_objectiveId);

            return true;
        }

        /// <summary>
        /// Resets the pickup state, re-enabling visuals and collision for a new mission attempt.
        /// </summary>
        public void ResetPickup()
        {
            _isAcquired = false;

            if (_visualModel != null)
            {
                _visualModel.SetActive(true);
            }

            if (_interactionCollider != null)
            {
                _interactionCollider.enabled = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isAcquired || other == null)
            {
                return;
            }

            // Check if collider belongs to the Player
            if (other.CompareTag("Player") || other.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TryPickup(other.gameObject, bypassDistanceCheck: true);
            }
        }
    }
}
