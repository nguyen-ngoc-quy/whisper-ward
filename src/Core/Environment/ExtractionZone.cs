using System;
using UnityEngine;

namespace WhisperWard.Core.Environment
{
    /// <summary>
    /// Trigger volume representing the mission extraction boundary.
    /// When crossed by the player after completing objectives, signals successful extraction.
    /// </summary>
    public class ExtractionZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private Collider _zoneCollider;

        private bool _isExtracted;

        /// <summary>Global notification dispatched when any extraction zone is triggered.</summary>
        public static event Action OnAnyExtractionCompleted;

        /// <summary>Instance notification dispatched with the player actor that extracted.</summary>
        public event Action<GameObject> OnExtractionCompleted;

        /// <summary>Gets whether the extraction has already been completed in this attempt.</summary>
        public bool IsExtracted => _isExtracted;

        /// <summary>Gets the zone trigger collider.</summary>
        public Collider ZoneCollider => _zoneCollider;

        private void Awake()
        {
            if (_zoneCollider == null)
            {
                _zoneCollider = GetComponent<Collider>();
            }

            if (_zoneCollider != null)
            {
                _zoneCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// Explicitly triggers extraction on behalf of the player actor.
        /// </summary>
        /// <param name="player">The player GameObject entering the extraction volume.</param>
        /// <returns>True if extraction was successfully initiated; false if already extracted or invalid.</returns>
        public bool TriggerExtraction(GameObject player)
        {
            if (_isExtracted || player == null)
            {
                return false;
            }

            _isExtracted = true;

            OnExtractionCompleted?.Invoke(player);
            OnAnyExtractionCompleted?.Invoke();

            return true;
        }

        /// <summary>
        /// Resets the extraction state for a new mission attempt.
        /// </summary>
        public void ResetZone()
        {
            _isExtracted = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isExtracted || other == null)
            {
                return;
            }

            if (other.CompareTag("Player") || other.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TriggerExtraction(other.gameObject);
            }
        }
    }
}
