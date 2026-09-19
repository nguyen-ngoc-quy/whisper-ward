using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Base class for Guard states providing common utilities.
    /// </summary>
    public abstract class GuardStateBase : IGuardState
    {
        public abstract string StateName { get; }

        public virtual void OnEnter(GuardFSM fsm) { }
        public virtual void OnUpdate(GuardFSM fsm, long tick, float delta) { }
        public virtual void OnExit(GuardFSM fsm) { }

        public virtual void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            // Base implementation handles generic event routing if needed
        }

        protected void PublishDecision(GuardFSM fsm, DecisionRecord record)
        {
            fsm?.PublishDecision(record);
        }
    }
}
