using System;
using UnityEngine;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// All events produced by the Perception system that the FSM consumes.
    /// </summary>
    public abstract class SensingFact : IEvent
    {
        public float Timestamp { get; protected set; }
        public string Publisher => "Perception";
    }

    public class LOSGain : SensingFact
    {
        public Vector3 Position { get; set; }
        public float Distance { get; set; }
        public GameObject Guard { get; set; }
    }

    public class LOSBreak : SensingFact
    {
        public float ResidualR { get; set; }
        public string BreakType { get; set; } // "hide", "wall", "segment"
        public string CausalClass { get; set; } // "peek-out", "guard-sweep", etc.
        public float GuardYawDelta { get; set; }
    }

    public class NoiseHeard : SensingFact
    {
        public string Kind { get; set; }
        public Vector3 Position { get; set; }
        public string EntryId { get; set; }
        public bool IsFirstConsumption { get; set; }
    }

    public class ThresholdCrossing : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
    }

    public class ConfirmWindowElapsed : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
    }

    public class CapReached : SensingFact
    {
        public string EntryId { get; set; }
        public int Counter { get; set; }
        public Vector3 Position { get; set; }
    }

    public class Reachability : SensingFact
    {
        public string EntryId { get; set; }
        public bool IsReachable { get; set; }
        public float PathArrivalM { get; set; }
        public float DeltaYM { get; set; }
        public Vector3 Position { get; set; }
    }

    public class ChaseReached : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>
    /// All events produced by the FSM that Perception and other systems consume.
    /// </summary>
    public abstract class DecisionRecord : IEvent
    {
        public float Timestamp { get; protected set; }
        public string Publisher => "GuardFSM";
    }

    public class InvestigateCommit : DecisionRecord
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string Cause { get; set; } // "threshold", "noise", "alert", "cap-forced"
        public Vector3 Position { get; set; }
        public string ThresholdState { get; set; }
        public bool IsHideEntry { get; set; }
    }

    public class ChaseEntry : DecisionRecord
    {
        public string EntryId { get; set; }
        public string Cause { get; set; } // "threshold", "hide-entry"
        public string ThresholdState { get; set; }
        public Vector3 Position { get; set; }
        public bool PairedChaseReached { get; set; }
    }

    public class ChaseEnd : DecisionRecord
    {
        public string EntryId { get; set; }
        public GameObject Guard { get; set; }
    }

    public class InvestigateResolution : DecisionRecord
    {
        public string EntryId { get; set; }
        public string Cause { get; set; } // "fruitless", "sight-committed", "cap-forced", "capture", "redirected", "abandoned"
        public Vector3 Position { get; set; }
    }

    public class Capture : DecisionRecord
    {
        public string EntryId { get; set; }
        public Vector3 Position { get; set; }
        public GameObject Victim { get; set; }
        public bool IsCarveOut { get; set; }
    }
}
