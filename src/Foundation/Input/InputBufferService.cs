using System;

namespace WhisperWard.Foundation.Input
{
    public enum BufferedActionType
    {
        None = 0,
        Crouch = 1,
        Sprint = 2,
        Throw = 3
    }

    /// <summary>
    /// Manages zero-allocation buffering for stance and throw requests.
    /// Conforms to ADR-0005 (150ms buffer window, single-consumption semantics).
    /// </summary>
    public sealed class InputBufferService
    {
        public const double DefaultBufferWindow = 0.15; // 150 ms window

        private struct BufferEntry
        {
            public BufferedActionType Action;
            public double Timestamp;
            public bool IsActive;
        }

        private const int Capacity = 8;
        private readonly BufferEntry[] _entries = new BufferEntry[Capacity];

        /// <summary>
        /// Buffers an action request at the given virtual or game timestamp.
        /// Zero heap allocation.
        /// </summary>
        public void BufferAction(BufferedActionType action, double timestamp)
        {
            if (action == BufferedActionType.None) return;

            // Overwrite existing active entry for the same action or find first inactive slot
            int targetIndex = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (_entries[i].IsActive && _entries[i].Action == action)
                {
                    targetIndex = i;
                    break;
                }
                if (!_entries[i].IsActive && targetIndex == -1)
                {
                    targetIndex = i;
                }
            }

            if (targetIndex == -1) targetIndex = 0; // fallback to ring overwrite

            _entries[targetIndex].Action = action;
            _entries[targetIndex].Timestamp = timestamp;
            _entries[targetIndex].IsActive = true;
        }

        /// <summary>
        /// Attempts to consume a buffered action request.
        /// Returns true only if an active unconsumed request exists within the valid time window.
        /// Immediately consumes and invalidates the request so it cannot fire twice.
        /// </summary>
        public bool TryConsumeAction(BufferedActionType action, double currentTime, double maxAge = DefaultBufferWindow)
        {
            if (action == BufferedActionType.None) return false;

            for (int i = 0; i < Capacity; i++)
            {
                if (_entries[i].IsActive && _entries[i].Action == action)
                {
                    double age = currentTime - _entries[i].Timestamp;
                    if (age >= 0.0 && age <= maxAge)
                    {
                        // Valid request consumed
                        _entries[i].IsActive = false;
                        return true;
                    }
                    else
                    {
                        // Expired request: invalidate
                        _entries[i].IsActive = false;
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Clears all buffered input entries.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _entries[i].IsActive = false;
                _entries[i].Action = BufferedActionType.None;
            }
        }
    }
}
