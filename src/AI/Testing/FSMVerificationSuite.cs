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
            Assert.AreEqual(LivenessPositionSources.OpenerDecision,
                open.PositionSource);
            Assert.AreEqual(1.25f, open.SourceTimestamp, 0.0001f);
            Assert.AreEqual("GuardAISystem", open.Publisher);

            fsm.TransitionTo(fsm.GetPatrolState());
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, _harness.LivenessFacts.Count);
            LivenessFact close = _harness.LivenessFacts[1];
            Assert.AreEqual("close", close.Operation);
            Assert.AreEqual("Investigate", close.Tier);
            Assert.AreEqual("investigate-resolution", close.Cause);
            Assert.AreEqual(LivenessPositionSources.TerminalDecision,
                close.PositionSource);
            Assert.AreEqual("entry-01", close.EntryId);
            Assert.AreEqual("GuardAISystem", close.Publisher);
        }

        [Test]
        public void FSM_DirectChaseOpener_PreservesZeroTPublishAndEntryIdentity()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            fsm.GetChaseState().Init("entry-chase-zero", "threshold",
                new Vector3(4f, 0f, 1f), false, null, 0f);

            fsm.TransitionTo(fsm.GetChaseState());
            _harness.Advance(TickInterval);

            Assert.AreEqual("entry-chase-zero", fsm.currentEntryId);
            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual("open", _harness.LivenessFacts[0].Operation);
            Assert.AreEqual("entry-chase-zero", _harness.LivenessFacts[0].EntryId);
            Assert.AreEqual(0f, _harness.LivenessFacts[0].SourceTimestamp,
                0.0001f);
        }

        [Test]
        public void FSM_ZeroTPublish_IsRetainedAsAuthoritativeLivenessTimestamp()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                AttemptEpoch, 1, "entry-zero", fsm.GuardEid,
                new Vector3(2f, 0f, 3f), 0f, 0.4f);

            Assert.AreEqual(EventAdmission.Accepted,
                _harness.Publish(relay).Admission);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual(0f, _harness.LivenessFacts[0].SourceTimestamp,
                0.0001f);
        }

        [Test]
        public void FSM_NonNoiseEpisode_AcceptsQualifyingNoiseCorroboration()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            _harness.Publish(new CapReached
            {
                EntryId = "entry-cap",
                Counter = 2,
                Position = new Vector3(1f, 0f, 1f)
            }, SessionId, AttemptEpoch, "cap-reached-01", 0f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            PublishNoiseRelay(fsm, 1, 1f,
                new Vector3(1.2f, 0f, 1.1f), 0.2f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1,
                _harness.GetInvestigateDiagnostics(fsm).CorroborationCount);
            Assert.AreEqual(1, CountRecords<NoiseReanchor>(_harness.Decisions));
            Assert.AreEqual(1, CountRelayOutcomes(RelayConsumption.Consumed));
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
            Assert.AreEqual(LivenessPositionSources.OpenerDecision,
                open.PositionSource);
            Assert.AreEqual("entry-chase", open.EntryId);

            fsm.TransitionTo(fsm.GetPatrolState());
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, _harness.LivenessFacts.Count);
            LivenessFact close = _harness.LivenessFacts[1];
            Assert.AreEqual("close", close.Operation);
            Assert.AreEqual("Chase", close.Tier);
            Assert.AreEqual("Chase-end", close.Cause);
            Assert.AreEqual(LivenessPositionSources.TerminalDecision,
                close.PositionSource);
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
        public void FSM_HideEntryPromotion_IsInPlace_AndPublishesPromotionBeforeChaseEntry()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, new Vector3(2f, 0f, 2f), 0.2f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            _harness.ClearCapturedEvents();

            var publicationOrder = new List<string>();
            SubscriptionToken livenessToken = _harness.Bus.Subscribe<LivenessFact>(fact =>
            {
                if (fact.Operation == "promote") publicationOrder.Add("promote");
            });
            SubscriptionToken chaseToken = _harness.Bus.Subscribe<ChaseEntry>(entry =>
            {
                publicationOrder.Add("Chase-entry");
            });

            try
            {
                _harness.Publish(new LOSBreak
                {
                    GuardEid = fsm.GuardEid,
                    IsHideEntry = true,
                    APreBreak = 0.8f,
                    ThresholdApplied = 0.5f,
                    SpotPosition = new Vector3(8f, 0f, 4f),
                    SpotFrontPosition = new Vector3(7f, 0f, 4f),
                    HasSpotFrontPosition = true,
                    EntryId = "different-entry"
                }, SessionId, AttemptEpoch, "hide-entry-promotion", 2f);
                _harness.Advance(TickInterval);
                _harness.Advance(TickInterval);
            }
            finally
            {
                _harness.Bus.Unsubscribe(livenessToken);
                _harness.Bus.Unsubscribe(chaseToken);
            }

            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual("promote", _harness.LivenessFacts[0].Operation);
            Assert.AreEqual(LivenessPositionSources.EpisodePosition,
                _harness.LivenessFacts[0].PositionSource);
            Assert.AreEqual("entry-01", _harness.LivenessFacts[0].EntryId);
            Assert.AreEqual(0, CountRecords<InvestigateResolution>(_harness.Decisions));
            Assert.AreEqual(1, CountRecords<ChaseEntry>(_harness.Decisions));
            Assert.AreEqual("entry-01", ((ChaseEntry)_harness.Decisions[0]).EntryId);
            CollectionAssert.AreEqual(new[] { "promote", "Chase-entry" },
                publicationOrder);
        }

        [Test]
        public void FSM_NonNoiseEpisode_UsesCommitTPublish_ForCapAndExactZero()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            _harness.Advance(1f);
            _harness.Publish(new CapReached
            {
                EntryId = "entry-cap-zero",
                Counter = 3,
                Position = new Vector3(3f, 0f, 1f)
            }, SessionId, AttemptEpoch, "cap-zero", 0f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual(0f, _harness.LivenessFacts[0].SourceTimestamp,
                0.0001f);
            Assert.AreEqual(LivenessPositionSources.OpenerDecision,
                _harness.LivenessFacts[0].PositionSource);

            PublishNoiseRelay(fsm, 2, 0.5f, new Vector3(3.2f, 0f, 1.1f), 0.2f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, CountRecords<NoiseReanchor>(_harness.Decisions));
            NoiseReanchor reanchor = null;
            for (int i = 0; i < _harness.Decisions.Count; i++)
            {
                reanchor = _harness.Decisions[i] as NoiseReanchor;
                if (reanchor != null) break;
            }
            Assert.IsNotNull(reanchor);
            Assert.AreEqual(0f, reanchor.EpisodeAnchorTPublish, 0.0001f);
        }

        [Test]
        public void FSM_NonNoiseHideEntry_UsesCommitTPublish_ForExactZero()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            _harness.Advance(1f);
            _harness.Publish(new LOSBreak
            {
                GuardEid = fsm.GuardEid,
                IsHideEntry = true,
                APreBreak = 0.1f,
                ThresholdApplied = 0.5f,
                SpotPosition = new Vector3(4f, 0f, 2f),
                EntryId = "entry-hide-zero"
            }, SessionId, AttemptEpoch, "hide-zero", 0f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual(0f, _harness.LivenessFacts[0].SourceTimestamp,
                0.0001f);
            Assert.AreEqual(LivenessPositionSources.OpenerDecision,
                _harness.LivenessFacts[0].PositionSource);
        }

        [Test]
        public void FSM_EpochBoundary_RetainsExactlyOneOldGenerationStaleClose()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1f, Vector3.zero, 0.2f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            _harness.ClearCapturedEvents();

            var boundary = new SessionBoundaryService(_harness.Bus);
            boundary.Subscribe((activeSessionId, activeEpoch) =>
                fsm.ResetForBoundary(activeSessionId, activeEpoch));
            boundary.BeginEpoch(AttemptEpoch + 1);

            Assert.AreEqual(1, _harness.Bus.PendingEnvelopeCount);
            _harness.Bus.Drain(EventBusPhase.FsmDecision);
            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            LivenessFact stale = _harness.LivenessFacts[0];
            Assert.AreEqual(SessionId, stale.SessionId);
            Assert.AreEqual(AttemptEpoch, stale.AttemptEpoch);
            Assert.AreEqual("stale", stale.Cause);
            Assert.AreEqual(LivenessPositionSources.LastAuthoritativeEpisodePosition,
                stale.PositionSource);

            fsm.ProcessTick(_harness.Clock.CurrentTick + 1, TickInterval);
            _harness.Bus.Drain(EventBusPhase.FsmDecision);
            Assert.AreEqual(1, _harness.LivenessFacts.Count);
        }

        [Test]
        public void FSM_SuppressedReceipt_CopiesRelayProvenanceWithoutRecomputation()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            PublishNoiseRelay(fsm, 1, 1.25f, Vector3.zero, 0.2f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 2, 2.25f, new Vector3(8f, 0f, 8f), 0.2f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, _harness.SuppressedReceipts.Count);
            NoiseSuppressedReceipt receipt = _harness.SuppressedReceipts[0];
            Assert.AreEqual("movement", receipt.SourceKind);
            Assert.AreEqual("step:2", receipt.SourceEventId);
            Assert.AreEqual(2.25f, receipt.TPublish, 0.0001f);
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
            Assert.AreEqual("out-of-window",
                _harness.SuppressedReceipts[0].SuppressionReason);
            Assert.IsFalse(_harness.SuppressedReceipts[0].VisibleToPlayer);
            Assert.IsFalse(_harness.SuppressedReceipts[0].MicroTellEmitted);
        }

        [Test]
        public void FSM_Chase_IgnoresCurrentRelay_WithStateExclusionReceipt_ExactlyOnce()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            fsm.GetChaseState().Init("entry-chase-suppression", "threshold",
                new Vector3(4f, 0f, 1f));
            int visibilityQueries = 0;
            fsm.ConfigureSuppressionVisibilityPolicy(
                new DelegateSuppressionVisibilityPolicy(position =>
                {
                    visibilityQueries++;
                    return true;
                }));
            fsm.TransitionTo(fsm.GetChaseState());
            _harness.Advance(TickInterval);
            _harness.ClearCapturedEvents();

            NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                AttemptEpoch, 77, "entry-noise", fsm.GuardEid,
                new Vector3(2f, 0f, 2f), 1f, 0.2f);
            Assert.AreEqual(EventAdmission.Accepted, _harness.Publish(relay).Admission);
            _harness.Advance(TickInterval);

            Assert.AreEqual(1, CountRelayOutcomes(RelayConsumption.Ignored));
            Assert.AreEqual(0, visibilityQueries);
            Assert.AreEqual(1, _harness.SuppressedReceipts.Count);
            NoiseSuppressedReceipt receipt = _harness.SuppressedReceipts[0];
            Assert.AreEqual(77UL, receipt.FactId);
            Assert.AreEqual("state-exclusion", receipt.SuppressionReason);
            Assert.IsFalse(receipt.VisibleToPlayer);
            Assert.IsFalse(receipt.MicroTellEmitted);
            Assert.AreEqual("not-applicable", receipt.VisibilityQueryId);
            Assert.AreEqual("not-performed:state-exclusion",
                receipt.VisibilityQueryProvenance);

            Assert.AreEqual(EventAdmission.Duplicate, _harness.Publish(relay).Admission);
            _harness.Advance(TickInterval);
            Assert.AreEqual(1, CountRelayOutcomes(RelayConsumption.Ignored));
            Assert.AreEqual(1, _harness.SuppressedReceipts.Count);
        }

        [Test]
        public void FSM_VisibleSuppression_QueriesOncePerEligibleRelay_AndRateLimitsTell()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            int visibilityQueries = 0;
            fsm.ConfigureSuppressionVisibilityPolicy(
                new DelegateSuppressionVisibilityPolicy(position =>
                {
                    visibilityQueries++;
                    return true;
                }));

            PublishNoiseRelay(fsm, 1, 1f, new Vector3(1f, 0f, 1f), 0.1f);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 2, 2f, new Vector3(1.2f, 0f, 1.1f), 0.2f);
            PublishNoiseRelay(fsm, 3, 3f, new Vector3(1.3f, 0f, 1.2f), 0.3f);
            _harness.Advance(TickInterval);
            PublishNoiseRelay(fsm, 4, 4f, new Vector3(1.4f, 0f, 1.3f), 0.4f);
            PublishNoiseRelay(fsm, 5, 5f, new Vector3(1.5f, 0f, 1.4f), 0.5f);
            _harness.Advance(TickInterval);

            Assert.AreEqual(2, visibilityQueries);
            Assert.AreEqual(2, _harness.SuppressedReceipts.Count);
            Assert.IsTrue(_harness.SuppressedReceipts[0].VisibleToPlayer);
            Assert.IsTrue(_harness.SuppressedReceipts[0].MicroTellEmitted);
            Assert.IsTrue(_harness.SuppressedReceipts[1].VisibleToPlayer);
            Assert.IsFalse(_harness.SuppressedReceipts[1].MicroTellEmitted);
            Assert.AreEqual("GuardAISystem",
                _harness.SuppressedReceipts[0].Publisher);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                _harness.SuppressedReceipts[0].VisibilityQueryId));
            Assert.AreEqual("legacy-adapter",
                _harness.SuppressedReceipts[0].VisibilityQueryProvenance);
        }

        [Test]
        public void FSM_SuppressionReceipt_RetryIsStagedUntilBusAcceptsIt()
        {
            // A capacity-1 bus, deliberately saturated with one pending
            // Presentation envelope, proves the receipt is staged on Retry and
            // republished by the next FSM tick instead of being silently
            // dropped after its identity was marked exactly-once.
            SessionEventBus saturatedBus = new SessionEventBus(capacity: 1,
                maxRetries: 3);
            VirtualTickClockService clock = new VirtualTickClockService();
            var receipts = new List<NoiseSuppressedReceipt>();
            SubscriptionToken receiptToken =
                saturatedBus.Subscribe<NoiseSuppressedReceipt>(
                    receipt =>
                    {
                        if (receipt != null)
                            receipts.Add(receipt);
                    });

            GameObject host = new GameObject("FSM-receipt-retry");
            try
            {
                saturatedBus.BeginSession(SessionId, AttemptEpoch);
                GuardFSM fsm = host.AddComponent<GuardFSM>();
                fsm.Configure(saturatedBus, clock, SessionId, AttemptEpoch);

                // Saturate the queue with one unrelated Presentation envelope
                // so the receipt publication is retried rather than admitted.
                // The saturator has no subscriber, so it drains without
                // dispatching and never reaches the capture list.
                Assert.AreEqual(EventAdmission.Accepted,
                    saturatedBus.Publish(PhaseProbeEvent.Create(SessionId,
                        AttemptEpoch, "saturator", EventBusPhase.Presentation,
                        0f)).Admission);
                Assert.AreEqual(1, saturatedBus.PendingEnvelopeCount);

                NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                    AttemptEpoch, 1, "entry-01", fsm.GuardEid,
                    new Vector3(1f, 0f, 1f), 1f, 0.1f);

                fsm.PublishSuppressedReceipt(relay, "out-of-window", false,
                    false, clock.CurrentTime);
                Assert.AreEqual(0, receipts.Count,
                    "A retried receipt must not reach subscribers yet");
                Assert.IsEmpty(fsm.LastSuppressionReceiptFailureCode,
                    "A staged retry is transport pressure, not a failure");

                // Free the queue; the next FSM tick flushes the staged receipt
                // back onto the bus. A following phase drain delivers it.
                saturatedBus.Drain(EventBusPhase.Presentation);
                Assert.AreEqual(0, saturatedBus.PendingEnvelopeCount);
                fsm.ProcessTick(clock.CurrentTick + 1, TickInterval);
                Assert.AreEqual(1, saturatedBus.PendingEnvelopeCount);
                Assert.AreEqual(0, receipts.Count);
                saturatedBus.Drain(EventBusPhase.Presentation);

                Assert.AreEqual(1, receipts.Count,
                    "The staged receipt must be republished after the retry clears");
                Assert.AreEqual(1UL, receipts[0].FactId);
                Assert.AreEqual("out-of-window", receipts[0].SuppressionReason);
                Assert.AreEqual("GuardAISystem", receipts[0].Publisher);
                Assert.IsEmpty(fsm.LastSuppressionReceiptFailureCode);

                // A second attempt for the same key stays exactly-once even
                // though the first attempt was retried rather than accepted.
                fsm.PublishSuppressedReceipt(relay, "out-of-window", false,
                    false, clock.CurrentTime);
                Assert.AreEqual(1, receipts.Count);
            }
            finally
            {
                saturatedBus.Unsubscribe(receiptToken);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FSM_SuppressionReceipt_RetryExhaustion_RecordsStableFailureCode()
        {
            // The FSM-side exhaustion guard fires only when the bus keeps
            // answering Retry past the FSM's own limit, so this bus is built
            // with a retry budget larger than the FSM's three.
            SessionEventBus patientBus = new SessionEventBus(capacity: 1,
                maxRetries: 8);
            VirtualTickClockService clock = new VirtualTickClockService();
            var receipts = new List<NoiseSuppressedReceipt>();
            SubscriptionToken receiptToken =
                patientBus.Subscribe<NoiseSuppressedReceipt>(
                    receipt =>
                    {
                        if (receipt != null)
                            receipts.Add(receipt);
                    });

            GameObject host = new GameObject("FSM-receipt-exhaustion");
            try
            {
                patientBus.BeginSession(SessionId, AttemptEpoch);
                GuardFSM fsm = host.AddComponent<GuardFSM>();
                fsm.Configure(patientBus, clock, SessionId, AttemptEpoch);

                Assert.AreEqual(EventAdmission.Accepted,
                    patientBus.Publish(PhaseProbeEvent.Create(SessionId,
                        AttemptEpoch, "saturator", EventBusPhase.Presentation,
                        0f)).Admission);

                NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                    AttemptEpoch, 1, "entry-01", fsm.GuardEid,
                    new Vector3(1f, 0f, 1f), 1f, 0.1f);

                fsm.PublishSuppressedReceipt(relay, "out-of-window", false,
                    false, clock.CurrentTime);
                Assert.AreEqual(0, receipts.Count);

                // Initial staging carries the bus retry count (1); each
                // ProcessTick flush increments it until the FSM-side guard
                // exceeds its limit of three and drops the staged receipt.
                fsm.ProcessTick(1, TickInterval); // retry 2
                fsm.ProcessTick(2, TickInterval); // retry 3
                fsm.ProcessTick(3, TickInterval); // retry 4 -> exhausted

                Assert.AreEqual(0, receipts.Count,
                    "An exhausted receipt must never reach subscribers");
                Assert.AreEqual("fsm-suppression-receipt-retry-exhausted",
                    fsm.LastSuppressionReceiptFailureCode);
            }
            finally
            {
                patientBus.Unsubscribe(receiptToken);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FSM_SuppressionReceipt_BoundaryDiscard_RecordsStableFailureCode()
        {
            // A queued boundary transition discards staged receipts with a
            // diagnostic instead of delivering them into the next epoch.
            SessionEventBus bus = new SessionEventBus(capacity: 1);
            VirtualTickClockService clock = new VirtualTickClockService();

            GameObject host = new GameObject("FSM-receipt-boundary");
            try
            {
                bus.BeginSession(SessionId, AttemptEpoch);
                GuardFSM fsm = host.AddComponent<GuardFSM>();
                fsm.Configure(bus, clock, SessionId, AttemptEpoch);

                Assert.AreEqual(EventAdmission.Accepted,
                    bus.Publish(PhaseProbeEvent.Create(SessionId, AttemptEpoch,
                        "saturator", EventBusPhase.Presentation, 0f)).Admission);

                NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                    AttemptEpoch, 1, "entry-01", fsm.GuardEid,
                    new Vector3(1f, 0f, 1f), 1f, 0.1f);

                fsm.PublishSuppressedReceipt(relay, "out-of-window", false,
                    false, clock.CurrentTime);

                fsm.ResetForBoundary(SessionId, AttemptEpoch + 1);
                Assert.AreEqual("fsm-suppression-receipt-boundary-discarded",
                    fsm.LastSuppressionReceiptFailureCode);

                // The new epoch never delivers the discarded receipt: the
                // stale saturator drains away and the next flush publishes
                // nothing even though the queue is free again.
                bus.Drain(EventBusPhase.Presentation);
                Assert.AreEqual(0, bus.PendingEnvelopeCount);
                fsm.ProcessTick(1, TickInterval);
                Assert.AreEqual(0, bus.PendingEnvelopeCount,
                    "A boundary-discarded receipt must not be republished");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FSM_InvestigateCommit_Preserves_Anchor_And_Target_Provenance()
        {
            GuardFSM fsm = _harness.CreateFsm(SessionId, AttemptEpoch);
            NoiseHeardRelay relay = _harness.CreateNoiseRelay(SessionId,
                AttemptEpoch, 1, "entry-01", fsm.GuardEid,
                new Vector3(2f, 0f, 3f), 1.25f, 0.4f);

            Assert.AreEqual(EventAdmission.Accepted,
                _harness.Publish(relay).Admission);
            _harness.Advance(TickInterval);
            _harness.Advance(TickInterval);

            InvestigateCommit commit = null;
            for (int i = 0; i < _harness.Decisions.Count; i++)
            {
                commit = _harness.Decisions[i] as InvestigateCommit;
                if (commit != null) break;
            }

            Assert.IsNotNull(commit);
            Assert.AreEqual(SessionId, commit.EpisodeAnchorSessionId);
            Assert.AreEqual(fsm.GuardEid, commit.EpisodeAnchorGuardEid);
            Assert.AreEqual(1UL, commit.EpisodeAnchorFactId);
            Assert.AreEqual(1.25f, commit.EpisodeAnchorTPublish, 0.0001f);
            Assert.AreEqual(new Vector3(2f, 0f, 3f), commit.Position);
            Assert.AreEqual(0, commit.TargetSampleIndex);
            Assert.AreEqual("candidate", commit.TargetResolution);
            Assert.AreNotEqual(Vector3.zero, commit.TargetPosition);
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
            Assert.AreEqual(1, CountRecords<NoiseReanchor>(_harness.Decisions));
            NoiseReanchor reanchor = null;
            for (int i = 0; i < _harness.Decisions.Count; i++)
            {
                reanchor = _harness.Decisions[i] as NoiseReanchor;
                if (reanchor != null) break;
            }
            Assert.IsNotNull(reanchor);
            Assert.AreEqual(2UL, reanchor.CorroboratingFactId);
            Assert.AreEqual("entry-01", reanchor.EntryId);
            Assert.AreEqual("step:2", reanchor.SourceEventId);
            Assert.AreEqual(2f, reanchor.TPublish, 0.0001f);
            Assert.AreEqual(2, CountRelayOutcomes(RelayConsumption.Consumed));

            FsmVerificationHarness.InvestigateDiagnostics diagnostic =
                _harness.GetInvestigateDiagnostics(fsm);
            Assert.AreEqual("entry-01", diagnostic.EntryId);
            Assert.AreEqual(2, diagnostic.CorroborationCount);
            Assert.AreEqual(7.2f, diagnostic.GiveupTimer, 0.0001f);
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

            Vector3 expectedStalePosition =
                _harness.GetInvestigateDiagnostics(fsm).TargetPosition;
            fsm.ResetForBoundary(SessionId, AttemptEpoch + 1);
            Assert.AreEqual(1, _harness.Bus.PendingEnvelopeCount);
            Assert.IsNull(fsm.currentEntryId);
            // Drain returns callback deliveries, so the liveness fact is also
            // observed by the FSM's sensing subscription. Assert the captured
            // authoritative fact rather than coupling this test to fan-out count.
            _harness.Bus.Drain(EventBusPhase.FsmDecision);
            Assert.AreEqual(1, _harness.LivenessFacts.Count);
            Assert.AreEqual(LivenessPositionSources.LifecycleStaleClose,
                _harness.LivenessFacts[0].PositionSource);
            Assert.AreEqual(expectedStalePosition,
                _harness.LivenessFacts[0].Position);
            _harness.ClearCapturedEvents();

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
            _harness.Clock.Advance(
                VirtualTickClockService.CanonicalTickInterval);

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
