using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Deterministic boundary tests for hearing timing and geometry. These tests
    /// use an injected default PhysicsScene and therefore require a Unity runner.
    /// </summary>
    public sealed class PerceptionHearingVerificationSuite
    {
        [Test]
        public void PhysicsProfile_RejectsUnresolvedForbiddenLayer()
        {
            PhysicsQueryProfile profile;
            string errorCode;
            bool created = PhysicsQueryProfile.TryCreate(
                new[] { "Default" },
                new[] { "PlayerNoiseMissingLayer" },
                Physics.defaultPhysicsScene,
                out profile, out errorCode);

            Assert.IsFalse(created);
            Assert.IsNull(profile);
            Assert.AreEqual("E20_FORBIDDEN_LAYER_MISSING:PlayerNoiseMissingLayer",
                errorCode);
        }

        [Test]
        public void PhysicsProfile_UsesRegisteredOverlapBufferCapacity()
        {
            PhysicsQueryProfile profile;
            string errorCode;
            Assert.IsTrue(PhysicsQueryProfile.TryCreate(
                new[] { "Default" }, null, Physics.defaultPhysicsScene,
                out profile, out errorCode), errorCode);

            Assert.AreEqual(NoiseRuntimeConfiguration
                .RegisteredInitialOverlapBufferCapacity,
                profile.OverlapBufferCapacity);
        }

        [Test]
        public void PhysicsProfile_RejectsUnregisteredOverlapBufferCapacity()
        {
            PhysicsQueryProfile profile;
            string errorCode;
            Assert.IsFalse(PhysicsQueryProfile.TryCreate(
                new[] { "Default" }, null, Physics.defaultPhysicsScene,
                NoiseRuntimeConfiguration
                    .RegisteredMaxInitialOverlapBufferCapacity + 1,
                out profile, out errorCode));
            Assert.AreEqual("E20_OVERLAP_BUFFER_CAPACITY_INVALID",
                errorCode);
            Assert.IsNull(profile);

            Assert.IsFalse(PhysicsQueryProfile.TryCreate(
                new[] { "Default" }, null, Physics.defaultPhysicsScene,
                NoiseRuntimeConfiguration
                    .RegisteredMinInitialOverlapBufferCapacity - 1,
                out profile, out errorCode));
            Assert.AreEqual("E20_OVERLAP_BUFFER_CAPACITY_INVALID",
                errorCode);
            Assert.IsNull(profile);

            Assert.IsFalse(PhysicsQueryProfile.TryCreate(
                new[] { "Default" }, null, Physics.defaultPhysicsScene,
                0,
                out profile, out errorCode));
            Assert.AreEqual("E20_OVERLAP_BUFFER_CAPACITY_INVALID",
                errorCode);
            Assert.IsNull(profile);
        }

        [Test]
        public void Hearing_NotBeforePublish_RetainsFactUntilExactBoundary()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                fixture.Provider.Snapshots.Add(fixture.Guard(0f));
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0.4f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);

                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                Assert.AreEqual(1, fixture.Service.PendingFactCount,
                    "A fact must not be evaluated before t_publish");
                Assert.AreEqual(0, fixture.Relays.Count);

                // Exact boundary equality is eligible. The relay is queued for
                // the FSM phase and delivered only by its explicit phase drain.
                fixture.AdvanceTo(0.4f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                Assert.AreEqual(1, fixture.Bus.PendingEnvelopeCount);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);

                Assert.AreEqual(1, fixture.Relays.Count);
                Assert.AreEqual(1UL, fixture.Relays[0].FactId);
                Assert.AreEqual(0.4f, fixture.Relays[0].TPublish,
                    0.000001f);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_DeadlineMiss_WithNoGuardSnapshot_RejectsFact()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);

                // No active guard at the first boundary leaves equality at the
                // deadline valid; the next boundary proves terminal expiry.
                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                Assert.AreEqual(1, fixture.Service.PendingFactCount);

                fixture.AdvanceTo(0.4f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);

                Assert.AreEqual(0, fixture.Service.PendingFactCount);
                Assert.AreEqual(1, fixture.Service.RejectedFactCount);
                Assert.AreEqual("perception-hearing-deadline-missed",
                    fixture.Service.LastRejectionCode);
                Assert.AreEqual(0, fixture.Relays.Count);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_GuardSelectionCap_DefersUnselectedGuardsWithoutDroppingThem()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    fixture.Provider.Snapshots.Add(new GuardHearingSnapshot(
                        "guard-" + i.ToString("D2"), Vector3.zero,
                        Vector3.zero, 1f));
                }
                // MVP evaluates one guard per boundary. The second guard stays
                // in ordered deferred work: the first eligible boundary is 0.4
                // and the inclusive deadline is 0.6, so it is evaluated at the
                // second boundary rather than deadline-missed.
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0.4f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);

                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(0, fixture.Relays.Count,
                    "A fact must not be evaluated before t_publish");

                fixture.AdvanceTo(0.4f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(1, fixture.Relays.Count,
                    "The MVP guard cap limits one boundary without dropping the tail");

                // The pending fact owns the original stable snapshot; a later
                // provider capture must not replace its deferred guard tail.
                fixture.Provider.Snapshots.Clear();
                fixture.Provider.Snapshots.Add(new GuardHearingSnapshot(
                    "guard-replaced", Vector3.zero, Vector3.zero, 1f));

                fixture.AdvanceTo(0.6f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(2, fixture.Relays.Count,
                    "The unselected guard must remain ordered deferred work");
                Assert.AreEqual("guard-01", fixture.Relays[1].GuardEid,
                    "Deferred work must use the original stable snapshot");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_VerticalHardCutoff_ProducesNoRelay()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                fixture.Provider.Snapshots.Add(fixture.Guard(4f));
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);

                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick,
                    0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);

                Assert.AreEqual(0, fixture.Relays.Count,
                    "The inclusive hard cutoff excludes deltaY >= 4.0 m");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void GuardSnapshot_DerivedEyeOverflow_IsRejectedAtCapture()
        {
            Assert.Throws<ArgumentException>(() => new GuardHearingSnapshot(
                "guard-01", new Vector3(float.MaxValue, 0f, 0f),
                new Vector3(float.MaxValue, 0f, 0f), 1f));
        }

        [Test]
        public void GuardSnapshot_ResidualOutsideRegisteredRange_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GuardHearingSnapshot("guard-01", Vector3.zero,
                    Vector3.zero, -0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GuardHearingSnapshot("guard-01", Vector3.zero,
                    Vector3.zero, 1.001f));
        }

        [Test]
        public void GuardResidualProvider_CarriesAuthoritativeValueIntoSnapshot()
        {
            IGuardResidualSnapshotProvider provider =
                new FixedResidualProvider(0.75f);
            float residualR;
            string diagnosticCode;

            Assert.IsTrue(provider.TryGetResidual("guard-01", out residualR,
                out diagnosticCode));
            Assert.AreEqual(0.75f, residualR, 0.000001f);
            Assert.IsEmpty(diagnosticCode);

            GuardHearingSnapshot snapshot = new GuardHearingSnapshot(
                "guard-01", Vector3.zero, Vector3.zero, residualR);
            Assert.AreEqual(0.75f, snapshot.ResidualR, 0.000001f);
        }

        [Test]
        public void ConfigurationBackedHearing_RejectsUnregisteredVerticalCutoff()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                NoiseRuntimeConfiguration configuration =
                    new NoiseRuntimeConfiguration(NoiseResponseProfile.MVP,
                        NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                        NoiseRuntimeConfiguration.RegisteredMvpMaxHearingGuards,
                        8, 30, 512, 128, 512, 4, 1, 3,
                        0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                        NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                        NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters);

                Assert.Throws<ArgumentException>(() =>
                    new PerceptionHearingService(fixture.Bus, fixture.Clock,
                        fixture.Profile, fixture.Provider, 3.9f, configuration));

                PerceptionHearingService configured =
                    new PerceptionHearingService(fixture.Bus, fixture.Clock,
                        fixture.Profile, fixture.Provider, configuration);
                configured.Unbind();
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_ConsumptionOutcome_MatchesAndDeduplicates()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                fixture.Provider.Snapshots.Add(fixture.Guard(0f));
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);
                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick, 0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(1, fixture.Relays.Count);

                NoiseHeardRelay relay = fixture.Relays[0];
                var matching = new NoiseConsumptionOutcome("hearing-test", 1,
                    relay.FactId, relay.GuardEid, relay.EntryId,
                    RelayConsumption.Ignored, 0.2f);
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(matching).Admission);
                fixture.Bus.Drain(EventBusPhase.Presentation);
                Assert.AreEqual(string.Empty, fixture.Service.LastRejectionCode);

                Assert.AreEqual(EventAdmission.Duplicate,
                    fixture.Bus.Publish(matching).Admission);
                fixture.Bus.Drain(EventBusPhase.Presentation);
                Assert.AreEqual(string.Empty, fixture.Service.LastRejectionCode);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_ConsumptionOutcome_EntryMismatchRejects()
        {
            HearingFixture fixture = CreateFixture();
            try
            {
                fixture.Provider.Snapshots.Add(fixture.Guard(0f));
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);
                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick, 0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(1, fixture.Relays.Count);

                NoiseHeardRelay relay = fixture.Relays[0];
                var mismatch = new NoiseConsumptionOutcome("hearing-test", 1,
                    relay.FactId, relay.GuardEid, "wrong-entry",
                    RelayConsumption.Consumed, 0.2f);
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(mismatch).Admission);
                fixture.Bus.Drain(EventBusPhase.Presentation);
                Assert.AreEqual("perception-hearing-outcome-mismatch",
                    fixture.Service.LastRejectionCode);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Hearing_ConsumptionOutcomeMismatch_PreservesDetailedDiagnosticEnvelope()
        {
            RecordingDiagnosticSink diagnostics = new RecordingDiagnosticSink();
            HearingFixture fixture = CreateFixture(diagnostics);
            try
            {
                fixture.Provider.Snapshots.Add(fixture.Guard(0f));
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(fixture.BurstFact(1, 0f)).Admission);
                fixture.Bus.Drain(EventBusPhase.Hearing);
                fixture.AdvanceTo(0.2f);
                fixture.Service.ProcessHearingTick(fixture.Clock.CurrentTick, 0.2f);
                fixture.Bus.Drain(EventBusPhase.FsmDecision);
                Assert.AreEqual(1, fixture.Relays.Count);

                NoiseHeardRelay relay = fixture.Relays[0];
                var mismatch = new NoiseConsumptionOutcome("hearing-test", 1,
                    relay.FactId, relay.GuardEid, "wrong-entry",
                    RelayConsumption.Consumed, 0.2f);
                Assert.AreEqual(EventAdmission.Accepted,
                    fixture.Bus.Publish(mismatch).Admission);
                fixture.Bus.Drain(EventBusPhase.Presentation);

                Assert.AreEqual("perception-hearing-outcome-mismatch",
                    diagnostics.Code);
                Assert.AreEqual(1, diagnostics.DetailedCount);
                Assert.IsNotNull(diagnostics.Envelope);
                Assert.AreSame(mismatch, diagnostics.Envelope.Payload);
                Assert.AreEqual(mismatch.SessionId, diagnostics.Envelope.SessionId);
                Assert.AreEqual(mismatch.AttemptEpoch,
                    diagnostics.Envelope.AttemptEpoch);
                Assert.AreEqual(mismatch.Timestamp, diagnostics.Envelope.Timestamp,
                    0.000001f);
                Assert.AreEqual(mismatch.Publisher,
                    diagnostics.Envelope.Publisher);
                Assert.AreEqual(mismatch.Identity, diagnostics.Envelope.Identity);
                Assert.AreEqual(EventBusPhase.Presentation,
                    diagnostics.Envelope.Phase);
                Assert.AreEqual(0, diagnostics.QueueDepth);
                Assert.AreEqual(fixture.Service.RelayQueueCapacity,
                    diagnostics.QueueCapacity);
                Assert.AreEqual("relay-outcome", diagnostics.QueueName);
                Assert.AreEqual(relay.GuardEid + "|" + relay.FactId,
                    diagnostics.OrderingKey);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void GuardResidualProvider_MissingValueFailsClosedWithDiagnostic()
        {
            IGuardResidualSnapshotProvider provider =
                new MissingResidualProvider();
            float residualR;
            string diagnosticCode;

            Assert.IsFalse(provider.TryGetResidual("guard-01", out residualR,
                out diagnosticCode));
            Assert.AreEqual("residual-snapshot-unavailable", diagnosticCode);
        }

        [Test]
        public void NoiseRelay_ResidualOutsideRegisteredRange_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NoiseHeardRelay("hearing-range", 1, 1UL, "entry-01",
                    "guard-01", "movement", "test", Vector3.zero,
                    0f, 0f, 1.001f));
        }

        [Test]
        public void NoisePublished_NonFiniteRadiusAndOrigin_AreRejectedBeforeHearing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoisePublished(
                "hearing-malformed", 1, 1UL, "flight:1", "burst",
                "BurstSimulation", Vector3.zero, float.NaN, 0f,
                (int)NoiseSourceKind.Burst, Vector3.zero, "burst-terminal"));
            Assert.Throws<ArgumentException>(() => new NoisePublished(
                "hearing-malformed", 1, 2UL, "flight:2", "burst",
                "BurstSimulation", Vector3.zero, 1f, 0f,
                (int)NoiseSourceKind.Burst,
                new Vector3(float.NaN, 0f, 0f), "burst-terminal"));
        }

        private static HearingFixture CreateFixture(
            IEventDiagnosticSink diagnosticSink = null)
        {
            PhysicsQueryProfile profile;
            string errorCode;
            Assert.IsTrue(PhysicsQueryProfile.TryCreate(
                new[] { "Default" }, null, Physics.defaultPhysicsScene,
                out profile, out errorCode), errorCode);
            return new HearingFixture(profile, diagnosticSink);
        }

        private static NoiseRuntimeConfiguration CreateConfiguration()
        {
            return new NoiseRuntimeConfiguration(NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                NoiseRuntimeConfiguration.RegisteredMvpMaxHearingGuards,
                8, 30, 512, 3, 0.005f, 0.000001f, 0.25f, 4f, 2f,
                1f, 0.5f);
        }

        private sealed class HearingFixture : IDisposable
        {
            public readonly SessionEventBus Bus;
            public readonly VirtualTickClockService Clock;
            public readonly SnapshotProvider Provider;
            public readonly PhysicsQueryProfile Profile;
            public readonly PerceptionHearingService Service;
            public readonly List<NoiseHeardRelay> Relays =
                new List<NoiseHeardRelay>();
            private readonly PhysicsQueryProfile _profile;
            private readonly SubscriptionToken _relayToken;

            public HearingFixture(PhysicsQueryProfile profile,
                IEventDiagnosticSink diagnosticSink = null)
            {
                _profile = profile;
                Profile = profile;
                Bus = new SessionEventBus();
                Bus.BeginSession("hearing-test", 1);
                Clock = new VirtualTickClockService();
                Provider = new SnapshotProvider();
                Service = new PerceptionHearingService(Bus, Clock, _profile,
                    Provider, CreateConfiguration(), diagnosticSink);
                _relayToken = Bus.Subscribe<NoiseHeardRelay>(
                    relay =>
                    {
                        if (relay != null)
                            Relays.Add(relay);
                    });
                Service.BindWithoutClock();
            }

            public GuardHearingSnapshot Guard(float feetY)
            {
                return new GuardHearingSnapshot("guard-01",
                    new Vector3(0f, feetY, 0f), Vector3.zero, 1f);
            }

            public NoisePublished BurstFact(ulong factId, float tPublish)
            {
                return new NoisePublished("hearing-test", 1, factId,
                    "flight:" + factId.ToString(), "burst", "BurstSimulation",
                    Vector3.zero, 1f, 0f, (int)NoiseSourceKind.Burst,
                    Vector3.zero, "burst-terminal", Vector3.zero, "released",
                    string.Empty, 0f, 0f, tPublish, factId, string.Empty,
                    "published");
            }

            public void AdvanceTo(float virtualTime)
            {
                // Advance to an absolute virtual time: each Advance call is
                // relative, so the tick count must come from the remaining
                // delta, not from the target itself.
                float delta = virtualTime - Clock.CurrentTime;
                if (delta <= 0f) return;
                int ticks = (int)Math.Round(delta / Clock.TickInterval);
                for (int i = 0; i < ticks; i++)
                {
                    Clock.Advance(BurstRuntimeConfiguration
                        .CanonicalFixedSubstepSeconds);
                }
            }

            public void Dispose()
            {
                Service.Unbind();
                Bus.Unsubscribe(_relayToken);
            }
        }

        private sealed class FixedResidualProvider : IGuardResidualSnapshotProvider
        {
            private readonly float _residualR;

            public FixedResidualProvider(float residualR)
            {
                _residualR = residualR;
            }

            public bool TryGetResidual(string guardEid, out float residualR,
                out string diagnosticCode)
            {
                residualR = _residualR;
                diagnosticCode = string.Empty;
                return true;
            }
        }

        private sealed class MissingResidualProvider : IGuardResidualSnapshotProvider
        {
            public bool TryGetResidual(string guardEid, out float residualR,
                out string diagnosticCode)
            {
                residualR = float.NaN;
                diagnosticCode = "residual-snapshot-unavailable";
                return false;
            }
        }

        private sealed class RecordingDiagnosticSink : IEventDiagnosticDetailSink
        {
            public string Code;
            public EventEnvelope Envelope;
            public int RetryCount;
            public int QueueDepth;
            public int QueueCapacity;
            public string QueueName;
            public string OrderingKey;
            public int DetailedCount;

            public void Record(string code, EventEnvelope envelope,
                int retryCount, int queueDepth, int queueCapacity)
            {
                Code = code;
                Envelope = envelope;
                RetryCount = retryCount;
                QueueDepth = queueDepth;
                QueueCapacity = queueCapacity;
            }

            public void RecordDetailed(string code, EventEnvelope envelope,
                int retryCount, int queueDepth, int queueCapacity,
                string queueName, string orderingKey)
            {
                Record(code, envelope, retryCount, queueDepth, queueCapacity);
                QueueName = queueName;
                OrderingKey = orderingKey;
                DetailedCount++;
            }
        }

        private sealed class SnapshotProvider : IGuardHearingSnapshotProvider
        {
            public readonly List<GuardHearingSnapshot> Snapshots =
                new List<GuardHearingSnapshot>();

            public IList<GuardHearingSnapshot> CaptureActiveGuardSnapshots()
            {
                return new List<GuardHearingSnapshot>(Snapshots);
            }
        }
    }
}
