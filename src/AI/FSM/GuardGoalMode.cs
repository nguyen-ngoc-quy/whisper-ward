namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Parallel goal mode for the catch-gate contract.
    /// Not an FSM state, but a behavioral mode.
    /// </summary>
    public enum GuardGoalMode
    {
        LivePursuit,
        HideSpotFront,
        StaleLKP,
        None
    }
}
