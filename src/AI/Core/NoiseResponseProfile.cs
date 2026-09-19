namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable response tier selected by the session configuration before the
    /// first playable tick. MVP never silently enables Target coordination.
    /// </summary>
    public enum NoiseResponseProfile
    {
        MVP,
        Target
    }
}
