using UnityEngine;

namespace WhisperWard.Foundation.Input
{
    /// <summary>
    /// Centralized input service for Whisper Ward.
    /// Strictly manages:
    /// 1. Exclusive action map switching (Player vs UI).
    /// 2. Instant flush of movement/look vectors upon switching to UI or Pause (preventing stuck inputs).
    /// 3. Radial deadzone normalization on analog stick inputs.
    /// 4. 150ms stance and throw request buffering.
    /// 5. Zero GC allocations on polling loops.
    /// Conforms to ADR-0005 and TR-FOUND-008 / TR-FOUND-009.
    /// </summary>
    public sealed class InputSystemService : IPlayerInputProvider
    {
        private bool _isPlayerMapActive = true;
        private bool _isUIMapActive = false;

        private Vector2 _moveInput = Vector2.zero;
        private Vector2 _lookDelta = Vector2.zero;
        private bool _isSprintHeld = false;
        private bool _crouchTriggered = false;
        private bool _throwTriggered = false;
        private bool _interactTriggered = false;
        private bool _pauseTriggered = false;

        private readonly InputBufferService _bufferService = new InputBufferService();

        public bool IsPlayerMapActive => _isPlayerMapActive;
        public bool IsUIMapActive => _isUIMapActive;

        public Vector2 MoveInput => _isPlayerMapActive ? _moveInput : Vector2.zero;
        public Vector2 LookDelta => _isPlayerMapActive ? _lookDelta : Vector2.zero;
        public bool IsSprintHeld => _isPlayerMapActive && _isSprintHeld;
        public bool CrouchTriggered => _isPlayerMapActive && _crouchTriggered;
        public bool ThrowTriggered => _isPlayerMapActive && _throwTriggered;
        public bool InteractTriggered => _isPlayerMapActive && _interactTriggered;
        public bool PauseTriggered => _pauseTriggered;

        public InputBufferService BufferService => _bufferService;

        public void SetRawMoveInput(Vector2 rawStickInput)
        {
            if (!_isPlayerMapActive)
            {
                _moveInput = Vector2.zero;
                return;
            }
            _moveInput = RadialDeadzoneProcessor.Process(rawStickInput);
        }

        public void SetRawLookDelta(Vector2 rawLook)
        {
            if (!_isPlayerMapActive)
            {
                _lookDelta = Vector2.zero;
                return;
            }
            _lookDelta = rawLook;
        }

        public void SetSprintHeld(bool isHeld)
        {
            _isSprintHeld = isHeld;
        }

        public void TriggerCrouch(double timestamp)
        {
            _crouchTriggered = true;
            _bufferService.BufferAction(BufferedActionType.Crouch, timestamp);
        }

        public void TriggerThrow(double timestamp)
        {
            _throwTriggered = true;
            _bufferService.BufferAction(BufferedActionType.Throw, timestamp);
        }

        public void TriggerInteract()
        {
            _interactTriggered = true;
        }

        public void TriggerPause()
        {
            _pauseTriggered = true;
            SwitchToUIMap();
        }

        public void SwitchToPlayerMap()
        {
            _isPlayerMapActive = true;
            _isUIMapActive = false;
        }

        public void SwitchToUIMap()
        {
            _isPlayerMapActive = false;
            _isUIMapActive = true;
            FlushInputState();
        }

        public void FlushInputState()
        {
            _moveInput = Vector2.zero;
            _lookDelta = Vector2.zero;
            _isSprintHeld = false;
            _crouchTriggered = false;
            _throwTriggered = false;
            _interactTriggered = false;
            _pauseTriggered = false;
            _bufferService.Clear();
        }

        public void ResetTransientTriggers()
        {
            _crouchTriggered = false;
            _throwTriggered = false;
            _interactTriggered = false;
            _pauseTriggered = false;
        }
    }
}
