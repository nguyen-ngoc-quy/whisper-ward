using System;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Interface for Guard AI FSM States.
    /// Enforces the logic for handling virtual ticks and sensing facts.
    /// </summary>
    public interface IGuardState
    {
        void OnEnter(GuardFSM fsm);
        void OnUpdate(GuardFSM fsm, long tick, float delta);
        void OnHandleEvent(GuardFSM fsm, IEvent evt);
        void OnExit(GuardFSM fsm);
        string StateName { get; }
    }
}
