using UnityEngine;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.FSM;

namespace WhisperWard.AI.FSM.States
{
    public class InvestigateState : GuardStateBase
    {
        public override string StateName => "Investigate";

        private Vector3 _targetPosition;
        private string _entryId;
        private string _cause;

        // Tuning Knobs (consumed from registry/entities.yaml)
        private const float T_GIVEUP_BASE = 4.0f;
        private const float S_DIFF = 1.0f;
        private const float K_THOROUGH = 0.50f;
        private const float R_MAX = 1.0f;
        private const float T_SWEEP_BASE = 1.0f;

        private float _giveupTimer;
        private bool _isMovingToTarget = true;
        private float _thoroughness;
        private int _currentSweep = 0;
        private int _totalSweeps = 3;
        private float _sweepTimer;
        private float _lastPlayerPositionY;

        // For tracking player visibility
        private int _resightCount = 0;
        private float _resightAccumulator = 0f;

        // For look-around sweeps
        private List<Vector3> _scanTargetPositions = new List<Vector3>();
        private int _currentScan = 0;

        public void Init(string entryId, Vector3 position, string cause)
        {
            _entryId = entryId;
            _targetPosition = position;
            _cause = cause;

            // Calculate thoroughness tau based on current R
            // In a real implementation, R would come from perception
            float currentR = 0.5f; // Placeholder - would come from sensing facts
            _thoroughness = 1 + K_THOROUGH * (currentR / R_MAX);

            // Scale sweeps with tau
            _totalSweeps = Mathf.RoundToInt(3 * _thoroughness);
            _giveupTimer = T_GIVEUP_BASE * S_DIFF * _thoroughness;

            Debug.Log($"[InvestigateState] Initialized for {_cause} with give-up timer: {_giveupTimer}s");
        }

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log($"[InvestigateState] Entering state for {_cause}");

            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                nav.SetInvestigateSpeed();
                nav.MoveTo(_targetPosition);
            }

            // Initialize sweep positions (approximating scan arcs)
            // In a full implementation, these would be derived from scanCount and scanArcSpan
            _scanTargetPositions.Clear();
            for (int i = 0; i < 3; i++)
            {
                // Generating future scan positions around the target
                float angleOffset = (360f / _totalSweeps) * i;
                _scanTargetPositions.Add(CalculateOffsetPosition(_targetPosition, angleOffset, 2f));
            }
            _currentScan = 0;

            // Reset counters
            _currentSweep = 0;
            _resightCount = 0;
            _resightAccumulator = 0f;
        }

        private Vector3 CalculateOffsetPosition(Vector3 center, float angleDegrees, float radius)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angleRadians), 0, Mathf.Sin(angleRadians)) * radius;
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            // Handle movement progress
            if (_isMovingToTarget &&
                fsm.TryGetComponent<GuardNavigator>(out var nav) &&
                nav.HasReachedDestination())
            {
                _isMovingToTarget = false;
                Debug.Log("[InvestigateState] Reached target position");
                StartSweep(fsm);
            }

            // Handle sweep execution
            if (!_isMovingToTarget)
            {
                _sweepTimer -= delta;
                if (_sweepTimer <= 0)
                {
                    _currentSweep++;
                    if (_currentSweep >= _totalSweeps)
                    {
                        Resolution(fsm, "fruitless");
                    }
                    else
                    {
                        _sweepTimer = T_SWEEP_BASE * _thoroughness;
                        PerformScan(fsm);
                    }
                }
            }

            // Handle give-up clock (pauses during sustained LOS)
            bool hasLOS = fsm.GetComponent<PerceptionDriver>()?.HasLOS() ?? false;

            if (_giveupTimer > 0 && !hasLOS)
            {
                _giveupTimer -= delta;
                if (_giveupTimer <= 0)
                {
                    Resolution(fsm, "giveup");
                }
            }
            else if (hasLOS)
            {
                // Reset timer when LOS is regained
                _giveupTimer = T_GIVEUP_BASE * S_DIFF * _thoroughness;
            }

            // Handle re-sight tracking
            TrackReSight(fsm, delta);
        }

        private void PerformScan(GuardFSM fsm)
        {
            if (_currentScan >= _scanTargetPositions.Count) return;

            var scanTarget = _scanTargetPositions[_currentScan];
            Debug.Log($"[InvestigateState] Performing scan #{_currentScan + 1}/{_totalSweeps}");

            // In a full implementation, this would trigger scan animation/behavior
            // For now, we'll just simulate the sweep duration
            // This is where you'd add the look-around behavior

            _currentScan++;
        }

        private void TrackReSight(GuardFSM fsm, float delta)
        {
            // In a real implementation, this would query Perception for current player position
            var perceptionDriver = fsm.GetComponent<PerceptionDriver>();
            if (perceptionDriver == null) return;

            Vector3 currentPlayerPos = perceptionDriver.GetPlayerPosition();
            if (currentPlayerPos != Vector3.zero)
            {
                float yDiff = currentPlayerPos.y - _lastPlayerPositionY;
                _lastPlayerPositionY = currentPlayerPos.y;

                _resightAccumulator += delta;
                if (_resightAccumulator >= 1.0f) // 1 second of sustained LOS
                {
                    _resightCount++;
                    Debug.Log($"[InvestigateState] Re-sight count: {_resightCount}");
                    _resightAccumulator = 0;

                    if (_resightCount >= 2)
                    {
                        // Transition to Chase state
                        Debug.Log("[InvestigateState] Promotion to Chase due to re-sight count");
                        // This transition is handled when ChaseReached is processed
                    }
                }
            }
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is ChaseReached chaseEvt)
            {
                Debug.Log($"[InvestigateState] ChaseReached event received: {chaseEvt.Position}");
                var chaseState = fsm.GetChaseState();
                chaseState.Init(chaseEvt.EntryId, "threshold", chaseEvt.Position);
                fsm.TransitionTo(chaseState);
            }
        }

        public override void OnExit(GuardFSM fsm)
        {
            Debug.Log("[InvestigateState] Exiting state");
            // Cleanup if needed
        }

        private void Resolution(GuardFSM fsm, string cause)
        {
            Debug.Log($"[InvestigateState] Resolution with cause: {cause}");

            var record = new InvestigateResolution
            {
                EntryId = _entryId,
                Cause = cause,
                Position = _targetPosition
            };

            // Use EventBus to publish the resolution record
            EventBus.Publish(record);

            Debug.Log($"{cause} resolution published");
            fsm.TransitionTo(fsm.GetPatrolState());
        }
    }
}