namespace WhisperWard.AI.Core
{
    /// <summary>Stable public rejection families for Player Noise contracts.</summary>
    public static class NoiseRejectionCodes
    {
        /// <summary>Throw input was rejected because the player was not stationary.</summary>
        public const string ThrowWhileMovingRejected = "THROW_WHILE_MOVING_REJECTED";

        /// <summary>Throw input was rejected because crouched stand-up was blocked by overhead clearance.</summary>
        public const string BurstStandClearanceBlocked = "BURST_STAND_CLEARANCE_BLOCKED";

        /// <summary>Launch origin was occupied by E20 geometry.</summary>
        public const string InitialOverlapRejected = "INITIAL_OVERLAP_REJECTED";

        /// <summary>Initial-overlap enumeration could not be proven complete.</summary>
        public const string InitialOverlapQueryIncomplete =
            "INITIAL_OVERLAP_QUERY_INCOMPLETE";
    }
}
