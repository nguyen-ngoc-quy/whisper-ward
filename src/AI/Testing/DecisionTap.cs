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

        private void Start()
        {
            // Subscribe to all decision records
            EventBus.Subscribe<DecisionRecord>(OnDecisionRecord);
        }

        private void OnDecisionRecord(DecisionRecord record)
        {
            _capturedRecords.Add(record);
            Debug.Log($"[DecisionTap] Captured: {record.GetType().Name} at tick {record.Timestamp / 0.5f}");
        }

        public List<DecisionRecord> GetRecords()
        {
            return new List<DecisionRecord>(_capturedRecords);
        }

        public void Clear()
        {
            _capturedRecords.Clear();
        }

        /// <summary>
        /// Asserts that the exact sequence of records was published.
        /// </summary>
        public bool VerifyExactSequence(params System.Type[] expectedTypes)
        {
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