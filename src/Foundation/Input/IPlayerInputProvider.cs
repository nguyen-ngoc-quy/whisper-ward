using UnityEngine;

namespace WhisperWard.Foundation.Input
{
    /// <summary>
    /// Decoupled interface providing zero-allocation gameplay input access.
    /// Conforms to ADR-0005.
    /// </summary>
    public interface IPlayerInputProvider
    {
        bool IsPlayerMapActive { get; }
        bool IsUIMapActive { get; }

        Vector2 MoveInput { get; }
        Vector2 LookDelta { get; }
        bool IsSprintHeld { get; }
        bool CrouchTriggered { get; }
        bool ThrowTriggered { get; }
        bool InteractTriggered { get; }
        bool PauseTriggered { get; }

        void SwitchToPlayerMap();
        void SwitchToUIMap();
        void FlushInputState();
    }
}
