using System;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Base interface for all events flowing through the Guard AI system.
    /// </summary>
    public interface IEvent
    {
        float Timestamp { get; }
        string Publisher { get; }
    }
}
