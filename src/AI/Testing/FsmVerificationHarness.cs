using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.FSM;
using WhisperWard.AI.FSM.States;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Shared deterministic FSM test context backed by the real session bus,
    /// virtual clock, and phase coordinator.
    /// </summary>
    public sealed class FsmVerificationHarness : IDisposable
    {
        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private readonly List<DecisionRecord> _decisions = new List<DecisionRecord>();
        private readonly List<LivenessFact> _livenessFacts = new List<LivenessFact>();
        private readonly List<NoiseConsumptionOutcome> _relayOutcomes =
            new List<NoiseConsumptionOutcome>();
        private readonly List<NoiseSuppressedReceipt> _suppressedReceipts =
            new List<NoiseSuppressedReceipt>();
        private readonly SubscriptionToken _decisionToken;
        private readonly SubscriptionToken _livenessToken;
        private readonly SubscriptionToken _relayOutcomeToken;
        private readonly SubscriptionToken _suppressedReceiptToken;
        private bool _disposed;

        public FsmVerificationHarness(float tickInterval = 0.5f)
        {
            Bus = new SessionEventBus();
            // The shared clock is always canonical 1/120 s. The fixture's
            // tickInterval parameter remains the requested boundary duration
            // for existing callers; Advance splits that duration below.
            Clock = new VirtualTickClockService();
            Coordinator = new SessionPhaseCoordinator(Bus, Clock);

            _decisionToken = Bus.Subscribe<DecisionRecord>(record =>
            {
                if (record != null)
                    _decisions.Add(record);
            });
            _livenessToken = Bus.Subscribe<LivenessFact>(fact =>
            {
                if (fact != null)
                    _livenessFacts.Add(fact);
            });
            _relayOutcomeToken = Bus.Subscribe<NoiseConsumptionOutcome>(outcome =>
            {
                if (outcome != null)
                    _relayOutcomes.Add(outcome);
            });
            _suppressedReceiptToken = Bus.Subscribe<NoiseSuppressedReceipt>(receipt =>
            {
                if (receipt != null)
                    _suppressedReceipts.Add(receipt);
            });

            Coordinator.Bind();
        }

        public SessionEventBus Bus { get; }
        public VirtualTickClockService Clock { get; }
        public SessionPhaseCoordinator Coordinator { get; }

        public IReadOnlyList<DecisionRecord> Decisions { get { return _decisions; } }
        public IReadOnlyList<LivenessFact> LivenessFacts { get { return _livenessFacts; } }
        public IReadOnlyList<NoiseConsumptionOutcome> RelayOutcomes
        {
            get { return _relayOutcomes; }
        }

        public IReadOnlyList<NoiseSuppressedReceipt> SuppressedReceipts
        {
            get { return _suppressedReceipts; }
        }

        public IEventBus SessionEventBus { get { return Bus; } }
        public IVirtualTickClock VirtualTickClockService { get { return Clock; } }
        public SessionPhaseCoordinator PhaseCoordinator { get { return Coordinator; } }

        public GuardFSM CreateFsm(string sessionId, long attemptEpoch)
        {
            EnsureEnvelope(sessionId, attemptEpoch);

            GameObject gameObject = new GameObject("FSM-" + sessionId + "-" + attemptEpoch);
            _ownedObjects.Add(gameObject);

            GuardFSM fsm = gameObject.AddComponent<GuardFSM>();
            fsm.ConfigureWithPhaseCoordinator(Bus, Clock, sessionId, attemptEpoch,
                Coordinator);
            // EditMode does not guarantee MonoBehaviour.Awake ordering for a
            // freshly-created fixture. Ensure relay-driven tests begin in the
            // production default state without changing runtime behavior.
            if (fsm.CurrentState == null)
                fsm.TransitionTo(fsm.GetPatrolState());
            return fsm;
        }

        public void Advance(float gameplaySeconds)
        {
            if (float.IsNaN(gameplaySeconds) || float.IsInfinity(gameplaySeconds)
                || gameplaySeconds <= 0f)
            {
                Clock.Advance(gameplaySeconds);
                return;
            }

            // The production clock clamps one render-frame call to the
            // registry-backed max_frame_delta_s. Test boundaries may be much
            // larger (the FSM fixture uses 0.5 s), so drive them as the same
            // bounded sequence rather than accidentally dropping the whole
            // request to one sub-frame.
            float remaining = gameplaySeconds;
            float maxFrameDelta =
                NoiseRuntimeConfiguration.RegisteredMaxFrameDeltaSeconds;
            while (remaining > 0f)
            {
                float frameDelta = Math.Min(remaining, maxFrameDelta);
                Clock.Advance(frameDelta);
                remaining -= frameDelta;
            }
        }

        public void ClearCapturedEvents()
        {
            _decisions.Clear();
            _livenessFacts.Clear();
            _relayOutcomes.Clear();
            _suppressedReceipts.Clear();
        }

        public void BeginSession(string sessionId, long attemptEpoch)
        {
            Bus.BeginSession(sessionId, attemptEpoch);
        }

        public void BeginEpoch(string sessionId, long attemptEpoch)
        {
            Bus.BeginEpoch(sessionId, attemptEpoch);
        }

        public EventPublishResult Publish(IEvent evt)
        {
            return Bus.Publish(evt);
        }

        public EventPublishResult Publish(SensingFact fact, string sessionId,
            long attemptEpoch, string identity, float? timestamp = null)
        {
            if (fact == null)
                throw new ArgumentNullException(nameof(fact));

            fact.Timestamp = timestamp ?? Clock.CurrentTime;
            fact.SetEnvelope(sessionId, attemptEpoch, identity);
            return Bus.Publish(fact);
        }

        public EventPublishResult PublishFact(SensingFact fact, string sessionId,
            long attemptEpoch, string identity, float? timestamp = null)
        {
            return Publish(fact, sessionId, attemptEpoch, identity, timestamp);
        }

        public NoiseHeardRelay CreateNoiseRelay(string sessionId, long attemptEpoch,
            ulong factId, string entryId, string guardEid, Vector3 position,
            float sourceTimestamp, float residualAtHearing)
        {
            return new NoiseHeardRelay(sessionId, attemptEpoch, factId, entryId,
                guardEid, "movement", "test-harness", position, 1.5f, 0,
                "test-harness", position, sourceTimestamp, Clock.CurrentTime,
                residualAtHearing);
        }

        public float ComputeCanonicalReanchorBudget(float giveupBaseSeconds,
            float difficultyScalar, float thoroughnessCoefficient,
            float residualAtHearing, float residualMax, float reanchorExtendSeconds)
        {
            if (giveupBaseSeconds <= 0f || difficultyScalar < 0f
                || thoroughnessCoefficient < 0f || residualMax <= 0f
                || reanchorExtendSeconds < 0f)
                throw new ArgumentOutOfRangeException();
            return giveupBaseSeconds * difficultyScalar
                * (1f + thoroughnessCoefficient
                    * (residualAtHearing / residualMax))
                + reanchorExtendSeconds;
        }

        public InvestigateDiagnostics GetInvestigateDiagnostics(GuardFSM fsm)
        {
            InvestigateState state = fsm == null ? null : fsm.CurrentState as InvestigateState;
            Assert.IsNotNull(state, "FSM is not currently in InvestigateState.");

            return new InvestigateDiagnostics
            {
                EntryId = ReadPrivateField<string>(state, "_entryId"),
                CorroborationCount = ReadPrivateField<int>(state, "_corroborationCount"),
                GiveupTimer = ReadPrivateField<float>(state, "_giveupTimer"),
                SearchArmed = ReadPrivateField<bool>(state, "_searchArmed"),
                IsMovingToTarget = ReadPrivateField<bool>(state, "_isMovingToTarget"),
                TargetPosition = ReadPrivateField<Vector3>(state, "_targetPosition"),
                DifficultyScalar = ReadPrivateField<float>(state, "_difficultyScalar")
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Coordinator.Unbind();
            Bus.Unsubscribe(_decisionToken);
            Bus.Unsubscribe(_livenessToken);
            Bus.Unsubscribe(_relayOutcomeToken);
            Bus.Unsubscribe(_suppressedReceiptToken);

            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
            }

            _ownedObjects.Clear();
            _disposed = true;
        }

        private void EnsureEnvelope(string sessionId, long attemptEpoch)
        {
            if (!string.Equals(Bus.SessionId, sessionId, StringComparison.Ordinal))
            {
                Bus.BeginSession(sessionId, attemptEpoch);
                return;
            }

            if (attemptEpoch == Bus.AttemptEpoch)
                return;

            if (attemptEpoch > Bus.AttemptEpoch)
            {
                Bus.BeginEpoch(sessionId, attemptEpoch);
                return;
            }

            Bus.BeginSession(sessionId, attemptEpoch);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field: " + fieldName);
            return (T)field.GetValue(target);
        }

        public sealed class InvestigateDiagnostics
        {
            public string EntryId;
            public int CorroborationCount;
            public float GiveupTimer;
            public bool SearchArmed;
            public bool IsMovingToTarget;
            public Vector3 TargetPosition;
            public float DifficultyScalar;
        }
    }
}
