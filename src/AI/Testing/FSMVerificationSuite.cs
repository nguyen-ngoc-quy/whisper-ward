using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    public class FSMVerificationSuite
    {
        private const string SessionId = "fsm-test-session";
        private const long AttemptEpoch = 1;
        private const float TickInterval = 0.5f;

        private FsmVerificationHarness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new FsmVerificationHarness(TickInterval);
        }

        [TearDown]
        public void TearDown()
        {
            if (_harness != null)
            {
                _harness.Dispose();
                _harness = null;
            }
        }

        [Test]
        public void FSM_Has_Only_Three_States()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);

            var states = new HashSet<object>
            {
                fsm.GetPatrolState(),
                fsm.GetInvestigateState(),
                fsm.GetChaseState()
            };

            Assert.AreEqual(3, states.Count);
        }

        [Test]
        public void FSM_ReanchorBudget_Uses_Registered_Difficulty_Formula()
        {
            float starterBudget = _harness.ComputeCanonicalReanchorBudget(4f, 1f,
                0.5f, 0.4f, 1f, 2f);
            float harderBudget = _harness.ComputeCanonicalReanchorBudget(4f, 1.6f,
                0.5f, 0.4f, 1f, 2f);

            Assert.AreEqual(6.8f, starterBudget, 0.0001f);
            Assert.AreEqual(9.68f, harderBudget, 0.0001f);
        }

        [Test]
        public void FSM_Investigate_Liveness_Open_And_Close_AreObserved_On_RealBus()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId, AttemptEpoch, 1,
                "entry-01", fsm.GuardEid, new Vector3(2f, 0f, 3f), 1.25f, 0.4f);

            Assert.AreEqual(EventAdmission.Accepted, _harness.Publish(relay).Admission);

            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.LivenessFacts.Count);

            LivenessFact open = _harness.LivenessFacts[0];
            Assert.AreEqual(SessionId, open.SessionId);
            Assert.AreEqual(AttemptEpoch, open.AttemptEpoch);
            Assert.AreEqual("entry-01", open.EntryId);
            Assert.AreEqual("open", open.Operation);
            Assert.AreEqual("Investigate", open.Tier);
            Assert.AreEqual("investigate-commit", open.Cause);
            Assert.AreEqual(1.25f, open.SourceTimestamp, 0.0001f);
            Assert.AreEqual("GuardAISystem", open.Publisher);

            fsm.TransitionTo(fsm.GetPatrolState());
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, _harness.LivenessFacts.Count);
            LivenessFact close = _harness.LivenessFacts[1];
            Assert.AreEqual("close", close.Operation);
            Assert.AreEqual("Investigate", close.Tier);
            Assert.AreEqual("investigate-resolution", close.Cause);
            Assert.AreEqual("entry-01", close.EntryId);
            Assert.AreEqual("GuardAISystem", close.Publisher);
        }

        [Test]
        public void FSM_Chase_Liveness_Open_And_Close_AreObserved_On_RealBus()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            fsm.GetChaseState().Init("entry-chase", "threshold", new Vector3(4f, 0f, 1f));

            fsm.TransitionTo(fsm.GetChaseState());
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            LivenessFact open = _harness.LivenessFacts[0];
            Assert.AreEqual("open", open.Operation);
            Assert.AreEqual("Chase", open.Tier);
            Assert.AreEqual("Chase-entry", open.Cause);
            Assert.AreEqual("entry-chase", open.EntryId);

            fsm.TransitionTo(fsm.GetPatrolState());
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, _harness.LivenessFacts.Count);
            LivenessFact close = _harness.LivenessFacts[1];
            Assert.AreEqual("close", close.Operation);
            Assert.AreEqual("Chase", close.Tier);
            Assert.AreEqual("Chase-end", close.Cause);
            Assert.AreEqual("entry-chase", close.EntryId);
        }

        [Test]
        public void FSM_Investigate_To_Chase_Uses_InPlace_Liveness_Promotion()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, new Vector3(2f, 0f, 2f), 0.2f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            _harness.Publish(new ChaseReached
            {
                EntryId = "entry-01",
                Position = new Vector3(4f, 0f, 3f)
            }, SessionId, AttemptEpoch, "chase-reached-01");
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, _harness.LivenessFacts.Count);
            Assert.AreEqual("open", _harness.LivenessFacts[0].Operation);
            Assert.AreEqual("promote", _harness.LivenessFacts[1].Operation);
            Assert.AreEqual("entry-01", _harness.LivenessFacts[0].EntryId);
            Assert.AreEqual("entry-01", _harness.LivenessFacts[1].EntryId);
            Assert.AreEqual(1, CountRecords<InvestigateCommit>(_harness.Decisions));
            Assert.AreEqual(fsm.GetChaseState(), fsm.CurrentState);
        }

        [Test]
        public void FSM_Ignored_Relay_Emits_One_Suppressed_Receipt()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, new Vector3(1f, 0f, 1f), 0.1f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 2, 2f, new Vector3(1.2f, 0f, 1.1f), 0.2f);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 3, 3f, new Vector3(1.3f, 0f, 1.2f), 0.3f);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 4, 4f, new Vector3(1.4f, 0f, 1.3f), 0.4f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.SuppressedReceipts.Count);
            Assert.AreEqual(4UL, _harness.SuppressedReceipts[0].FactId);
            Assert.AreEqual("fsm-suppressed",
                _harness.SuppressedReceipts[0].SuppressionReason);
            Assert.IsFalse(_harness.SuppressedReceipts[0].VisibleToPlayer);
            Assert.IsFalse(_harness.SuppressedReceipts[0].MicroTellEmitted);
        }

        [Test]
        public void FSM_Reanchor_Keeps_Live_Entry_And_Does_Not_Publish_Duplicate_Commit()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);

            Assert.AreEqual(EventAdmission.Accepted, _harness.Publish(
                _harness.CreateNoiseRelay(SessionId, AttemptEpoch, 1, "entry-01",
                    fsm.GuardEid, new Vector3(2f, 0f, 2f), 1f, 0.2f)).Admission);

            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            Assert.AreEqual(EventAdmission.Accepted, _harness.Publish(
                _harness.CreateNoiseRelay(SessionId, AttemptEpoch, 2, "entry-01",
                    fsm.GuardEid, new Vector3(2.4f, 0f, 2.2f), 2f, 0.6f)).Admission);

            _harness.Advance(TickInterval);

            Assert.AreEqual(1, CountRecords<InvestigateCommit>(_harness.Decisions));
            Assert.AreEqual(2, CountRelayOutcomes(RelayConsumption.Consumed));

            FsmVerificationHarness.InvestigateDiagnostics diagnostic =
                _harness.GetInvestigateDiagnostics(fsm);
            Assert.AreEqual("entry-01", diagnostic.EntryId);
            Assert.AreEqual(2, diagnostic.CorroborationCount);
            Assert.AreEqual(6f, diagnostic.GiveupTimer, 0.0001f);
            Assert.IsFalse(diagnostic.SearchArmed);
            Assert.IsTrue(diagnostic.IsMovingToTarget);
            Assert.AreEqual(new Vector3(2.4f, 0f, 2.2f), diagnostic.TargetPosition);
        }

        [Test]
        public void FSM_Corroboration_Saturates_At_Three_And_Fourth_Qualifying_Relay_Is_Ignored()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, new Vector3(1f, 0f, 1f), 0.1f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            PublishNoiseRelay(fsm, 2, 2f, new Vector3(1.2f, 0f, 1.1f), 0.2f);
            _harness.Advance(TickInterval);

            PublishNoiseRelay(fsm, 3, 3f, new Vector3(1.3f, 0f, 1.2f), 0.3f);
            _harness.Advance(TickInterval);

            PublishNoiseRelay(fsm, 4, 4f, new Vector3(1.4f, 0f, 1.3f), 0.4f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, CountRecords<InvestigateCommit>(_harness.Decisions));
            Assert.AreEqual(3, CountRelayOutcomes(RelayConsumption.Consumed));
            Assert.AreEqual(1, CountRelayOutcomes(RelayConsumption.Ignored));
            Assert.AreEqual(3, _harness.GetInvestigateDiagnostics(fsm).CorroborationCount);
        }

        [Test]
        public void FSM_EpochBarrier_Diagnostic_Clears_Old_Work_Without_Claiming_Publication_Evidence()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, new Vector3(0f, 0f, 0f), 0.2f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            _harness.ClearCapturedEvents();

            fsm.ResetForBoundary(SessionId, AttemptEpoch + 1);
            Assert.AreEqual(1, _harness.Bus.PendingEnvelopeCount);
            Assert.AreEqual("entry-01", fsm.currentEntryId);

            _harness.BeginEpoch(SessionId, AttemptEpoch + 1);
            Assert.AreEqual(0, _harness.Bus.PendingEnvelopeCount);
            Assert.IsNull(fsm.currentEntryId);
            Assert.AreEqual(fsm.GetPatrolState(), fsm.CurrentState);
            Assert.AreEqual(0, _harness.LivenessFacts.Count);
        }

        [Test]
        public void FSM_PhaseCoordinator_Drains_Canonical_Order_And_Pause_Preserves_It()
        {
            var order = new List<string>();
            _harness.BeginSession(SessionId, AttemptEpoch);

            _harness.Bus.Subscribe<PhaseProbeEvent>(probe =>
            {
                order.Add("drain:" + probe.PhaseLabel);
            });

            _harness.Coordinator.RegisterIngressSource((tick, delta) =>
            {
                order.Add("participant:GameplayIngress");
                _harness.Publish(PhaseProbeEvent.Create(SessionId, AttemptEpoch,
                    "hearing-event", EventBusPhase.Hearing, _harness.Clock.CurrentTime));
            });
            _harness.Coordinator.RegisterHearingParticipant((tick, delta) =>
            {
                order.Add("participant:Hearing");
                _harness.Publish(PhaseProbeEvent.Create(SessionId, AttemptEpoch,
                    "fsm-event", EventBusPhase.FsmDecision, _harness.Clock.CurrentTime));
            });
            _harness.Coordinator.RegisterFsmParticipant((tick, delta) =>
            {
                order.Add("participant:FsmDecision");
                _harness.Publish(PhaseProbeEvent.Create(SessionId, AttemptEpoch,
                    "presentation-event", EventBusPhase.Presentation,
                    _harness.Clock.CurrentTime));
            });

            _harness.Publish(PhaseProbeEvent.Create(SessionId, AttemptEpoch,
                "ingress-event", EventBusPhase.GameplayIngress, 0f));

            _harness.Clock.SetPaused(true);
            _harness.Advance(2f);
            Assert.AreEqual(0, order.Count);
            Assert.AreEqual(0, _harness.Clock.CurrentTick);

            _harness.Clock.SetPaused(false);
            _harness.Advance(TickInterval);

            CollectionAssert.AreEqual(new[]
            {
                "drain:GameplayIngress",
                "participant:GameplayIngress",
                "drain:Hearing",
                "participant:Hearing",
                "drain:FsmDecision",
                "participant:FsmDecision",
                "drain:Presentation"
            }, order);
        }

        private void PublishNoiseRelay(GuardFSM fsm, ulong factId, float sourceTimestamp,
            Vector3 position, float residual)
        {
            Assert.AreEqual(EventAdmission.Accepted, _harness.Publish(
                _harness.CreateNoiseRelay(SessionId, _harness.Bus.AttemptEpoch, factId,
                    "entry-01", fsm.GuardEid, position, sourceTimestamp, residual)).Admission);
        }

        private int CountRelayOutcomes(RelayConsumption consumption)
        {
            int count = 0;
            for (int i = 0; i < _harness.RelayOutcomes.Count; i++)
            {
                if (_harness.RelayOutcomes[i].Consumption == consumption)
                    count++;
            }

            return count;
        }

        private static int CountRecords<T>(IReadOnlyList<DecisionRecord> records)
            where T : DecisionRecord
        {
            int count = 0;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] is T)
                    count++;
            }

            return count;
        }

        private sealed class PhaseProbeEvent : IEvent, IEventPhase
        {
            private PhaseProbeEvent(string sessionId, long attemptEpoch, string identity,
                EventBusPhase phase, float timestamp)
            {
                SessionId = sessionId;
                AttemptEpoch = attemptEpoch;
                Identity = identity;
                Phase = phase;
                Timestamp = timestamp;
            }

            public string SessionId { get; }
            public long AttemptEpoch { get; }
            public float Timestamp { get; }
            public string Publisher { get { return "FSMVerificationSuite"; } }
            public string Identity { get; }
            public EventBusPhase Phase { get; }
            public string PhaseLabel { get { return Phase.ToString(); } }

            public static PhaseProbeEvent Create(string sessionId, long attemptEpoch,
                string identity, EventBusPhase phase, float timestamp)
            {
                return new PhaseProbeEvent(sessionId, attemptEpoch, identity, phase,
                    timestamp);
            }
        }
    }
}
