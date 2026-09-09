using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>Deterministic admission tests for the pre-spend Burst contract.</summary>
    public sealed class BurstAdmissionVerificationSuite
    {
        private sealed class Query : IBurstPhysicsQuery
        {
            public int OverlapCalls;
            public int PrepareCalls;
            public ClosestContactResult Contact;

            public void PrepareBatch() { PrepareCalls++; }

            public bool HasInitialOverlap(Vector3 center, float radius)
            {
                OverlapCalls++;
                return false;
            }

            public ClosestContactResult Sweep(Vector3 currentCenter,
                Vector3 nextCenter, float radius)
            {
                return Contact;
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
            Assert.AreEqual("burst-throw-locomotion-not-stationary",
                service.LastRejectionCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(0, query.PrepareCalls);

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
            Assert.AreEqual("burst-throw-planar-speed-positive",
                service.LastRejectionCode);
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
            Assert.AreEqual("burst-throw-locomotion-not-stationary",
                service.LastRejectionCode);
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
            Assert.AreEqual("burst-throw-unsupported-stance",
                service.LastRejectionCode);
            Assert.AreEqual(BurstFlightState.Carried, service.State);
            Assert.AreEqual(0UL, service.CurrentFlightHandleId);
            Assert.AreEqual(0, query.OverlapCalls);
            Assert.AreEqual(0, query.PrepareCalls);

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
            Assert.AreEqual(new Vector3(2f, 1.051f, 3f),
                batch.Terminal.TerminalPosition);
            Assert.AreNotEqual(batch.Terminal.SurfaceContactPosition.Value,
                batch.Terminal.TerminalPosition);

            service.Dispose();
        }
    }
}
