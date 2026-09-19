using System.Collections.Generic;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Captures all published DecisionRecords for assertion in H.0 test suite.
    /// Implements the exact field equality requirement from the GDD.
    /// </summary>
    public class DecisionTap : MonoBehaviour
    {
        private readonly List<DecisionRecord> _capturedRecords = new List<DecisionRecord>();
        private IEventBus _eventBus;
        private SubscriptionToken _decisionToken;
        private bool _isBound;

        /// <summary>
        /// Injects the session-scoped bus used by this test adapter.
        /// </summary>
        public void Configure(IEventBus eventBus)
        {
            eventBus = eventBus ?? throw new System.ArgumentNullException(nameof(eventBus));
            bool wasBound = _isBound;
            if (wasBound) UnbindRuntime();
            _eventBus = eventBus;
            if (wasBound) BindRuntime(_eventBus);
        }

        private void OnEnable()
        {
            BindRuntime(_eventBus ?? EventBus.Default);
        }

        private void Start()
        {
            BindRuntime(_eventBus ?? EventBus.Default);
        }

        private void OnDisable()
        {
            UnbindRuntime();
        }

        private void OnDestroy()
        {
            UnbindRuntime();
        }

        private void BindRuntime(IEventBus eventBus)
        {
            if (_isBound || eventBus == null) return;
            _eventBus = eventBus;
            _decisionToken = _eventBus.Subscribe<DecisionRecord>(OnDecisionRecord);
            _isBound = true;
        }

        private void UnbindRuntime()
        {
            if (!_isBound) return;
            if (_eventBus != null)
                _eventBus.Unsubscribe(_decisionToken);
            _isBound = false;
        }

        private void OnDecisionRecord(DecisionRecord record)
        {
            if (record == null) return;
            _capturedRecords.Add(record);
            Debug.Log($"[DecisionTap] Captured: {record.GetType().Name} at {record.Timestamp:0.###}s");
        }

        /// <summary>
        /// Returns a snapshot of captured records.
        /// </summary>
        public List<DecisionRecord> GetRecords()
        {
            return new List<DecisionRecord>(_capturedRecords);
        }

        /// <summary>
        /// Clears all captured records.
        /// </summary>
        public void Clear()
        {
            _capturedRecords.Clear();
        }

        /// <summary>
        /// Asserts that the exact sequence of records was published.
        /// </summary>
        public bool VerifyExactSequence(params System.Type[] expectedTypes)
        {
            if (expectedTypes == null) return false;
            if (_capturedRecords.Count != expectedTypes.Length)
            {
                Debug.LogError($"[DecisionTap] Count mismatch: expected {expectedTypes.Length}, got {_capturedRecords.Count}");
                return false;
            }

            for (int i = 0; i < expectedTypes.Length; i++)
            {
                if (_capturedRecords[i].GetType() != expectedTypes[i])
                {
                    Debug.LogError($"[DecisionTap] Type mismatch at index {i}: expected {expectedTypes[i].Name}, got {_capturedRecords[i].GetType().Name}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Asserts specific field values on the last record of a given type.
        /// </summary>
        public bool VerifyLastRecord<T>(System.Action<T> assertions) where T : DecisionRecord
        {
            if (assertions == null) return false;
            for (int i = _capturedRecords.Count - 1; i >= 0; i--)
            {
                if (_capturedRecords[i] is T record)
                {
                    try
                    {
                        assertions(record);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[DecisionTap] Assertion failed: {ex.Message}");
                        return false;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the count of a specific decision record type.
        /// </summary>
        public int GetCount<T>() where T : DecisionRecord
        {
            int count = 0;
            foreach (var r in _capturedRecords)
            {
                if (r is T) count++;
            }
            return count;
        }
    }
}
