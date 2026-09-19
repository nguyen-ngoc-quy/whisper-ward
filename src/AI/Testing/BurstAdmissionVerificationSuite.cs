using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>Deterministic admission tests for the pre-spend Burst contract.</summary>
    public sealed class BurstAdmissionVerificationSuite
    {
        private sealed class Query : IBurstPhysicsQuery,
            IBurstPhysicsQueryWithOverlapStatus, IBurstPickupQueryWithOrigin
        {
            public int OverlapCalls;
            public int PrepareCalls;
            public ClosestContactResult Contact;
            public BurstOverlapResult InitialOverlap = BurstOverlapResult.Clear;

            public void PrepareBatch() { PrepareCalls++; }

            public BurstOverlapResult GetInitialOverlap(Vector3 center,
                float radius)
            {
                OverlapCalls++;
                return InitialOverlap;
            }

            public ClosestContactResult Sweep(Vector3 currentCenter,
                Vector3 nextCenter, float radius)
            {
                return Contact;
            }

            public PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
                Vector3 pickupAnchor, float reachRadius, float originOffset)
            {
                return PickupReachResult.Reachable;
            }
        }

        private sealed class UntypedQuery : IBurstPhysicsQuery
        {
            public void PrepareBatch() { }

            public ClosestContactResult Sweep(Vector3 currentCenter,
                Vector3 nextCenter, float radius)
            {
                return default(ClosestContactResult);
            }
        }

        [Test]
        public void WalkLabel_IsRejectedBeforeOverlapHandleOrSpend()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-admission", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-admission", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Walk, BurstStanceLabel.Standing, 0f);

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.ThrowWhileMovingRejected,
                service.LastRejectionCode);
            Assert.AreEqual("burst-throw-locomotion-not-stationary",
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(0, query.PrepareCalls);

            service.Dispose();
        }

        [Test]
        public void LargeFiniteDelta_ClampsBeforeNarrowingAndRetainsFiniteRemainder()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-large-delta", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(new BurstThrowRequest(
                    "burst-large-delta", 1, "test", Vector3.zero,
                    Vector3.forward, Vector3.zero, 0f,
                    BurstLocomotionLabel.Stationary,
                    BurstStanceLabel.Standing, 0f), out snapshot));

            BurstStepBatchResult result = service.Advance(1, float.MaxValue);

            Assert.AreEqual(configuration.MaxBacklogTicks, result.TickCount);
            Assert.IsTrue(result.BacklogClamped);
            Assert.AreEqual(int.MaxValue, result.DiscardedTickCount);
            Assert.IsFalse(result.HasTerminal);
            Assert.IsTrue(service.CurrentAccumulatorSeconds >= 0f);
            Assert.IsTrue(service.CurrentAccumulatorSeconds
                < configuration.FixedSubstepSeconds);
            service.Dispose();
        }

        [Test]
        public void PositiveResolvedPlanarSpeed_IsRejectedBeforeOverlap()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-speed", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            service.TryPickup("pickup-01", Vector3.zero,
                new Vector3(0.5f, 0f, 0f));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-speed", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Standing, 0.01f);

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.ThrowWhileMovingRejected,
                service.LastRejectionCode);
            Assert.AreEqual("burst-throw-planar-speed-positive",
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(BurstFlightState.Carried, service.State);

            service.Dispose();
        }

        [Test]
        public void RunLabel_IsRejectedBeforeOverlapHandleOrSpend()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-run", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-run", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Run, BurstStanceLabel.Standing, 0f);

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.ThrowWhileMovingRejected,
                service.LastRejectionCode);
            Assert.AreEqual("burst-throw-locomotion-not-stationary",
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(0, query.PrepareCalls);
            Assert.AreEqual(0, bus.PendingEnvelopeCount);

            service.Dispose();
        }

        [Test]
        public void UnsupportedStance_IsRejectedBeforeOverlapHandleOrSpend()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-stance", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-stance", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, (BurstStanceLabel)99, 0f);

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.ThrowWhileMovingRejected,
                service.LastRejectionCode);
            Assert.AreEqual("burst-throw-unsupported-stance",
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(0, query.PrepareCalls);

            service.Dispose();
        }

        [Test]
        public void CrouchedThrow_RequiresSameTickStandResolution()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-crouch-stand", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowSnapshot snapshot;
            BurstThrowRequest blockedStand = new BurstThrowRequest(
                "burst-crouch-stand", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Crouched,
                0f);

            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(blockedStand, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.BurstStandClearanceBlocked,
                service.LastRejectionCode);
            Assert.AreEqual("burst-stand-clearance-blocked",
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0, query.OverlapCalls);

            BurstThrowRequest resolvedStand = new BurstThrowRequest(
                "burst-crouch-stand", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Crouched,
                0f, default(Vector3), 0f, "canonical", true);
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(resolvedStand, out snapshot));
            Assert.AreEqual(BurstStanceLabel.Standing, snapshot.StanceLabel);
            Assert.AreEqual(1, query.OverlapCalls);

            service.Dispose();
        }

        [Test]
        public void ZeroCameraAim_UsesResolvedFacingFallback()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-facing-fallback", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            service.TryPickup("pickup-01", Vector3.zero,
                new Vector3(0.5f, 0f, 0f));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-facing-fallback", 1, "test", Vector3.zero,
                Vector3.zero, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Standing,
                0f, default(Vector3), 90f);
            BurstThrowSnapshot snapshot;

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(request, out snapshot));
            Vector3 expectedVelocity = configuration.ResolveInitialVelocity(
                Vector3.right);
            Assert.That(snapshot.ResolvedVelocity.x,
                Is.EqualTo(expectedVelocity.x).Within(0.0001f));
            Assert.That(snapshot.ResolvedVelocity.z,
                Is.EqualTo(expectedVelocity.z).Within(0.0001f));

            service.Dispose();
        }

        [Test]
        public void IncompleteInitialOverlap_IsRejectedBeforeHandleOrSpend()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-incomplete-overlap", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query
            {
                InitialOverlap = BurstOverlapResult.Incomplete
            };
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-incomplete-overlap", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f);

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(NoiseRejectionCodes.InitialOverlapQueryIncomplete,
                service.LastRejectionCode);
            Assert.AreEqual(NoiseRejectionCodes.InitialOverlapQueryIncomplete,
                service.LastRejectionDiagnosticCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(1, query.OverlapCalls);
            Assert.AreEqual(1, query.PrepareCalls);

            service.Dispose();
        }

        [Test]
        public void UntypedOverlapAdapter_IsRejectedFailClosedBeforeSpend()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-untyped-overlap", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            UntypedQuery query = new UntypedQuery();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Rejected,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            Assert.AreEqual("burst-pickup-origin-seam-required",
                service.LastRejectionCode);
            Assert.AreEqual(BurstFlightState.Placed, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);

            service.Dispose();
        }

        [Test]
        public void AcceptedSnapshot_UsesConfigurationVelocity_AndCanonicalIdentity()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-snapshot", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);
            Vector3 callerVelocityOverride = new Vector3(2f, 3f, 4f);
            Vector3 expectedVelocity = configuration.ResolveInitialVelocity(Vector3.forward);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-snapshot", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Standing,
                0f, callerVelocityOverride, 22.5f, "geometry-v1");

            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(request, out snapshot));
            Assert.AreEqual(expectedVelocity, snapshot.ResolvedVelocity);
            Assert.AreNotEqual(callerVelocityOverride, snapshot.ResolvedVelocity);
            Assert.AreEqual("throw:burst-snapshot:1:1",
                snapshot.ThrowSnapshotId);
            Assert.AreEqual("pickup-01", snapshot.SourcePickupId);
            Assert.AreEqual("geometry-v1", snapshot.GeometryVariantId);
            Assert.AreEqual(snapshot.LaunchPosition, service.CurrentPosition);
            Assert.AreEqual(expectedVelocity, service.CurrentVelocity);

            service.Dispose();
        }

        [Test]
        public void DeathCancellation_RetainsTerminalPublicationTime_WithoutNoisePublication()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-death", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(new BurstThrowRequest(
                    "burst-death", 1, "test", Vector3.zero,
                    Vector3.forward, Vector3.zero, 0f), out snapshot));

            clock.Advance(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds * 3f);
            Assert.IsTrue(service.CancelForDeath());

            BurstTerminalRecord terminal;
            Assert.IsTrue(service.TryGetLastTerminal(out terminal));
            Assert.AreEqual(BurstTerminalEvent.DeathCancelled,
                terminal.TerminalEvent);
            Assert.AreEqual(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds * 3f,
                terminal.TerminalPublicationTime.Value, 0.0001f);
            Assert.IsFalse(terminal.TPublish.HasValue);
            Assert.AreEqual(BurstFactPublicationState.NotApplicable,
                terminal.FactPublicationState);
            Assert.AreEqual(0, bus.PendingEnvelopeCount);

            service.Dispose();
        }

        [Test]
        public void NonIntegralTimeout_IsRejectedBeforeRuntimeCreation()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new BurstRuntimeConfiguration(
                    10f, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    3.001f, 0.05f, 0.001f, 8, 10.5f, 1.5f));
        }

        [Test]
        public void OverflowingTimeout_IsRejectedBeforeRuntimeCreation()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new BurstRuntimeConfiguration(
                    float.MaxValue, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    float.MaxValue, 0.05f, 0.001f, 8, 10.5f, 1.5f));
        }

        [Test]
        public void LockedPhysicsValues_RejectRegistryDrift()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new BurstRuntimeConfiguration(
                    10f, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    3f, 0.5f, 0.001f, 8, 10.5f, 1.5f));
            Assert.Throws<System.ArgumentException>(() =>
                new BurstRuntimeConfiguration(
                    10f, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    3f, 0.05f, 0.5f, 8, 10.5f, 1.5f));
            Assert.Throws<System.ArgumentException>(() =>
                new BurstRuntimeConfiguration(
                    10f, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    3f, 0.05f, 0.001f, 1, 10.5f, 1.5f));
        }

        [Test]
        public void NonFiniteDirection_IsRejectedByVelocityResolver()
        {
            BurstRuntimeConfiguration configuration =
                new BurstRuntimeConfiguration(
                    10f, 38f, 1.5f, 9.81f,
                    BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                    3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);

            Assert.Throws<System.ArgumentException>(() =>
                configuration.ResolveInitialVelocity(
                    new Vector3(float.NaN, 0f, 1f)));
        }

        [Test]
        public void TimeoutPublicationTime_UsesFlightStartTickAcrossClockCallbacks()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-timing", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query();
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            for (int i = 0; i < 12; i++)
                clock.Advance(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(new BurstThrowRequest(
                    "burst-timing", 1, "test", Vector3.zero,
                    Vector3.forward, Vector3.zero, 0f), out snapshot));

            SubscriptionToken token = clock.Subscribe((tick, delta) => { service.Advance(tick, delta); });
            for (int i = 0; i < 360; i++)
                clock.Advance(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds);
            clock.Unsubscribe(token);

            Assert.IsTrue(service.LastBatch.HasTerminal);
            Assert.AreEqual(BurstTerminalEvent.Timeout,
                service.LastBatch.Terminal.TerminalEvent);
            Assert.AreEqual(360, service.LastBatch.Terminal.TerminalTickIndex);
            Assert.That(service.LastBatch.Terminal.TerminalPublicationTime,
                Is.EqualTo(3.1f).Within(0.0001f));
            Assert.That(service.LastBatch.Terminal.TPublish.Value,
                Is.EqualTo(3.1f).Within(0.0001f));
            Assert.AreEqual(snapshot.SourceTimestamp,
                service.LastBatch.Terminal.SourceTimestamp);
            Assert.AreEqual(snapshot.ResolvedVelocity,
                service.LastBatch.Terminal.ResolvedVelocity);
            Assert.AreEqual("Timeout",
                service.LastBatch.Terminal.TerminalCause);
            Assert.AreEqual(BurstFlightState.Consumed,
                service.LastBatch.Terminal.BurstStateAfter);
            Assert.That(clock.CurrentTime, Is.EqualTo(3.1f).Within(0.0001f));

            service.Dispose();
        }

        [Test]
        public void ContactTerminal_PreservesSurfaceContact_AndPublishesPushedCenter()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-contact", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query
            {
                Contact = ClosestContactResult.FromSurfaceContact(
                    new Vector3(2f, 1f, 3f), Vector3.up, 0.001f)
            };
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowRequest request = new BurstThrowRequest(
                "burst-contact", 1, "test", Vector3.zero,
                Vector3.forward, Vector3.zero, 0f);
            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(request, out snapshot));

            BurstStepBatchResult batch = service.Advance(1,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds);

            Assert.IsTrue(batch.HasTerminal);
            Assert.AreEqual(BurstTerminalEvent.Contact,
                batch.Terminal.TerminalEvent);
            Assert.AreEqual(new Vector3(2f, 1f, 3f),
                batch.Terminal.SurfaceContactPosition.Value);
            Assert.That(batch.Terminal.TerminalPosition.x,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(batch.Terminal.TerminalPosition.y,
                Is.EqualTo(1.051f).Within(0.0001f));
            Assert.That(batch.Terminal.TerminalPosition.z,
                Is.EqualTo(3f).Within(0.0001f));
            Assert.AreEqual(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                batch.Terminal.TerminalPublicationTime);
            Assert.AreEqual(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                batch.Terminal.TPublish.Value);
            Assert.AreEqual(snapshot.SourceTimestamp,
                batch.Terminal.SourceTimestamp);
            Assert.AreEqual(snapshot.ResolvedVelocity,
                batch.Terminal.ResolvedVelocity);
            Assert.AreEqual(1, batch.Terminal.WinningTickIndex);
            Assert.AreEqual("Collision", batch.Terminal.TerminalCause);
            Assert.AreEqual(BurstFlightState.Consumed,
                batch.Terminal.BurstStateAfter);
            Assert.AreEqual(1, batch.Terminal.TerminalTickIndex);
            Assert.AreNotEqual(batch.Terminal.SurfaceContactPosition.Value,
                batch.Terminal.TerminalPosition);

            service.Dispose();
        }

        [Test]
        public void MalformedContact_NonFiniteSurface_FailsClosedWithoutNoise()
        {
            BurstTerminalRecord terminal = AdvanceFlightWithContact(
                ClosestContactResult.FromSurfaceContact(
                    new Vector3(float.NaN, 1f, 3f), Vector3.up, 0.001f));

            Assert.AreEqual(BurstTerminalEvent.BoundaryCancelled,
                terminal.TerminalEvent);
            Assert.AreEqual(1, terminal.TerminalTickIndex);
            Assert.AreEqual(BurstFactPublicationState.NotApplicable,
                terminal.FactPublicationState);
            Assert.IsFalse(terminal.FactId.HasValue);
            Assert.IsFalse(terminal.SurfaceContactPosition.HasValue);
            Assert.IsFalse(terminal.TerminalPublicationTime.HasValue);
            Assert.IsFalse(terminal.TPublish.HasValue);
            Assert.AreEqual(BurstFlightState.Consumed,
                terminal.BurstStateAfter);
        }

        [Test]
        public void MalformedContact_ZeroNormal_FailsClosedWithoutNoise()
        {
            BurstTerminalRecord terminal = AdvanceFlightWithContact(
                ClosestContactResult.FromSurfaceContact(
                    new Vector3(2f, 1f, 3f), Vector3.zero, 0.001f));

            Assert.AreEqual(BurstTerminalEvent.BoundaryCancelled,
                terminal.TerminalEvent);
            Assert.AreEqual(1, terminal.TerminalTickIndex);
            Assert.AreEqual(BurstFactPublicationState.NotApplicable,
                terminal.FactPublicationState);
            Assert.IsFalse(terminal.FactId.HasValue);
            Assert.IsFalse(terminal.SurfaceContactPosition.HasValue);
            Assert.IsFalse(terminal.TerminalPublicationTime.HasValue);
            Assert.IsFalse(terminal.TPublish.HasValue);
            Assert.AreEqual(BurstFlightState.Consumed,
                terminal.BurstStateAfter);
        }

        [Test]
        public void MalformedContact_NegativeDistance_FailsClosedWithoutNoise()
        {
            BurstTerminalRecord terminal = AdvanceFlightWithContact(
                ClosestContactResult.FromSurfaceContact(
                    new Vector3(2f, 1f, 3f), Vector3.up, -0.5f));

            Assert.AreEqual(BurstTerminalEvent.BoundaryCancelled,
                terminal.TerminalEvent);
            Assert.AreEqual(1, terminal.TerminalTickIndex);
            Assert.AreEqual(BurstFactPublicationState.NotApplicable,
                terminal.FactPublicationState);
            Assert.IsFalse(terminal.FactId.HasValue);
            Assert.IsFalse(terminal.SurfaceContactPosition.HasValue);
            Assert.IsFalse(terminal.TerminalPublicationTime.HasValue);
            Assert.IsFalse(terminal.TPublish.HasValue);
            Assert.AreEqual(BurstFlightState.Consumed,
                terminal.BurstStateAfter);
        }

        /// <summary>
        /// Runs one full throw flight whose injected query reports the given
        /// contact on every sweep, and returns the terminal that resolves it.
        /// </summary>
        private static BurstTerminalRecord AdvanceFlightWithContact(
            ClosestContactResult contact)
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("burst-malformed-contact", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            VirtualTickClockService clock = new VirtualTickClockService();
            Query query = new Query { Contact = contact };
            BurstRuntimeConfiguration configuration = new BurstRuntimeConfiguration(
                10f, 38f, 1.5f, 9.81f,
                BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds,
                3f, 0.05f, 0.001f, 8, 10.5f, 1.5f);
            BurstSimulationService service = new BurstSimulationService(
                configuration, query, emitter, clock);

            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryPickup("pickup-01", Vector3.zero,
                    new Vector3(0.5f, 0f, 0f)));
            BurstThrowSnapshot snapshot;
            Assert.AreEqual(BurstThrowAdmission.Accepted,
                service.TryThrow(new BurstThrowRequest(
                    "burst-malformed-contact", 1, "test", Vector3.zero,
                    Vector3.forward, Vector3.zero, 0f), out snapshot));

            // Advance through the subscribed clock so every callback tick is
            // monotonic against the injected canonical clock.
            SubscriptionToken token = clock.Subscribe((tick, delta) => { service.Advance(tick, delta); });
            BurstStepBatchResult terminalBatch = default(BurstStepBatchResult);
            for (int i = 0; i < 360; i++)
            {
                clock.Advance(BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds);
                if (service.LastBatch.HasTerminal)
                {
                    terminalBatch = service.LastBatch;
                    break;
                }
            }
            clock.Unsubscribe(token);

            Assert.IsTrue(terminalBatch.HasTerminal);
            BurstTerminalRecord terminal = terminalBatch.Terminal;
            service.Dispose();
            return terminal;
        }
    }
}
