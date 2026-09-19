using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>Contract tests for canonical Player Noise publication fields.</summary>
    public sealed class NoiseContractVerificationSuite
    {
        [Test]
        public void MovementPublication_UsesCanonicalIdentityModeAndPublishTime()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-movement", 2);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            NoisePublished published = null;
            bus.Subscribe<NoisePublished>(fact => published = fact);

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-movement", 2, "step-01",
                    "PlayerController", "Run", "Walk", Vector3.zero,
                    new Vector3(0f, 0f, 1f),
                    1f, 0.25f, 4f, 1.5f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            bus.Drain(EventBusPhase.Hearing);

            Assert.IsNotNull(published);
            Assert.AreEqual("movement", published.Kind);
            Assert.AreEqual("step:step-01", published.SourceEventId);
            Assert.AreEqual("Walk", published.MovementMode);
            Assert.AreEqual(4f, published.ResolvedNominalRadius);
            Assert.AreEqual(1.5f, published.TPublish);
            Assert.IsFalse(published.TerminalPublicationTime.HasValue,
                "Movement has no terminal publication time");
            Assert.AreEqual("published", published.PublicationState);
        }

        [Test]
        public void MovementSubmission_RejectsStaleSessionEnvelope()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-current", 4);
            NoiseEmitter emitter = new NoiseEmitter(bus);

            EventPublishResult result = emitter.SubmitMovement(
                "noise-old", 3, "step-stale", "PlayerController", "Walk",
                "Walk", Vector3.zero, Vector3.forward, 1f, 0f, 4f, 1f);

            Assert.AreEqual(EventAdmission.Rejected, result.Admission);
            Assert.AreEqual("noise-emitter-stale-source-envelope", result.Code);
            Assert.AreEqual(0, emitter.Flush());
        }

        [Test]
        public void EmitterFlush_OrdersNumericNamespacedMovementIds()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-ordering", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            List<string> sourceIds = new List<string>();
            bus.Subscribe<NoisePublished>(fact => sourceIds.Add(fact.SourceEventId));

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-ordering", 1, "10",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-ordering", 1, "9",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(2, emitter.Flush());
            bus.Drain(EventBusPhase.Hearing);

            CollectionAssert.AreEqual(new[] { "step:9", "step:10" }, sourceIds);
        }

        [Test]
        public void EmitterFlush_OrdersEqualTimestampBySourceClassBeforeArrival()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-equal-timestamp", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            List<string> sourceIds = new List<string>();
            bus.Subscribe<NoisePublished>(fact => sourceIds.Add(fact.SourceEventId));

            // Submit Burst first deliberately; rank, not arrival, owns the tie.
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitBurst("noise-equal-timestamp", 1, 1UL,
                    "BurstSimulation", Vector3.zero, 10.5f, 0.5f, 0.5f).Admission);
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-equal-timestamp", 1, "2",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 0.5f).Admission);

            Assert.AreEqual(2, emitter.Flush());
            bus.Drain(EventBusPhase.Hearing);
            CollectionAssert.AreEqual(new[] { "step:2", "flight:1" }, sourceIds);
        }

        [Test]
        public void EventBus_DrainsRelaysBySourceTupleNotEvaluationTime()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-relay-ordering", 1);
            List<string> sourceIds = new List<string>();
            bus.Subscribe<NoiseHeardRelay>(relay =>
                sourceIds.Add(relay.SourceEventId));

            // Relay evaluation boundaries deliberately disagree with source time.
            // The canonical source tuple must win over evaluatedAt in the envelope.
            NoiseHeardRelay laterSource = new NoiseHeardRelay(
                "noise-relay-ordering", 1, 1UL, "entry:1", "guard-01",
                "movement", "PlayerController", Vector3.zero, 4f, 0,
                "movement-feet", Vector3.zero, 0.50f, 0.20f, 0f,
                null, "step:1", null, "Walk", 4f);
            NoiseHeardRelay earlierSource = new NoiseHeardRelay(
                "noise-relay-ordering", 1, 2UL, "entry:2", "guard-01",
                "movement", "PlayerController", Vector3.zero, 4f, 0,
                "movement-feet", Vector3.zero, 0.10f, 0.30f, 0f,
                null, "step:2", null, "Walk", 4f);

            Assert.AreEqual(EventAdmission.Accepted, bus.Publish(laterSource).Admission);
            Assert.AreEqual(EventAdmission.Accepted, bus.Publish(earlierSource).Admission);
            bus.Drain(EventBusPhase.FsmDecision);

            CollectionAssert.AreEqual(new[] { "step:2", "step:1" }, sourceIds);
        }

        [Test]
        public void EmitterFlush_DoesNotApplyMovementOrderToBurstTerminalSources()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-cross-kind-ordering", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            List<string> sourceIds = new List<string>();
            NoisePublished burstPublished = null;
            bus.Subscribe<NoisePublished>(fact =>
            {
                sourceIds.Add(fact.SourceEventId);
                if (fact.SourceEventId == "flight:9")
                    burstPublished = fact;
            });

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-cross-kind-ordering", 1, "10",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 2f).Admission);
            Assert.AreEqual(1, emitter.Flush());

            // Burst source_timestamp is the input-edge timestamp. Its terminal
            // publication may occur later and must not be rejected by Movement
            // source ordering.
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitBurst("noise-cross-kind-ordering", 1, 9UL,
                    "BurstSimulation", Vector3.zero, 10.5f, 1f, 3f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            bus.Drain(EventBusPhase.Hearing);

            CollectionAssert.Contains(sourceIds, "flight:9");
            Assert.IsNotNull(burstPublished);
            Assert.AreEqual("flight:9", burstPublished.SourceEventId);
            Assert.AreEqual(3f, burstPublished.TPublish);
        }

        [Test]
        public void RuntimeConfiguration_UsesRegisteredCorroborationValues()
        {
            NoiseRuntimeConfiguration configuration =
                new NoiseRuntimeConfiguration(NoiseResponseProfile.MVP,
                    NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                    NoiseRuntimeConfiguration.RegisteredMvpMaxHearingGuards,
                    8, 30, 512, 128, 512, 4, 1, 3,
                    0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                    NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                    NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters);

            Assert.AreEqual(6f, configuration.NoiseCorroborateWindowSeconds);
            Assert.AreEqual(1.5f, configuration.NoiseCorroborateRadiusMeters);
            Assert.AreEqual(NoiseRuntimeConfiguration.RegisteredMvpMaxHearingGuards,
                configuration.MaxHearingGuards);
            Assert.AreEqual(8, configuration.MaxHearingFacts);
            Assert.AreEqual(30, configuration.MaxHearingPairs);
            Assert.AreEqual(512, configuration.HearingWorkCapacity);
            Assert.AreEqual(128, configuration.RawFactQueueCapacity);
            Assert.AreEqual(512, configuration.RelayQueueCapacity);
            Assert.AreEqual(4, configuration.MaxDeferredHearingBoundaries);
            Assert.AreEqual(1, configuration.MaxHearingBoundariesPerFrame);
            Assert.AreEqual(3, configuration.MaxRetries);
            Assert.AreEqual(4f, configuration.HearingYHardCutoffMeters);
            Assert.AreEqual(0.3f, configuration.FsmIntervalSeconds);
            Assert.AreEqual(8f, configuration.ChaseGiveupSeconds);
            Assert.AreEqual(30f, configuration.ChaseCapSeconds);
            Assert.AreEqual(2, configuration.ResightCap);
            Assert.AreEqual(1f, configuration.CatchSeconds);
            Assert.AreEqual(5.5f, configuration.CatchRangeMeters);
            Assert.AreEqual(0.4f,
                configuration.NavMeshSampleMaxDistanceMeters);
            Assert.AreEqual(1.5f,
                configuration.InvestigateErrorRadiusMeters);
        }

        [Test]
        public void RuntimeConfiguration_RejectsValuesOutsideRegisteredCapacityRanges()
        {
            string errorCode;
            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                31, 8, 30, 512, 128, 512, 4, 1, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-mvp-guard-cap-not-registered", errorCode);

            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 33, 30, 512, 128, 512, 4, 1, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-fact-cap-invalid", errorCode);

            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 9, 30, 512, 128, 512, 4, 1, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-fact-cap-not-registered", errorCode);

            Assert.IsTrue(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.Target,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                30, 9, 31, 512, 128, 512, 4, 2, 4,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode), errorCode);
        }

        [Test]
        public void GuardComposition_UsesProfileSpecificRetryValidation()
        {
            Assert.IsFalse(GuardAISystem.IsNoiseRetryConfigurationValid(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredMvpRetryAttempts + 1));
            Assert.IsTrue(GuardAISystem.IsNoiseRetryConfigurationValid(
                NoiseResponseProfile.Target,
                NoiseRuntimeConfiguration.RegisteredMinRetryAttempts));
            Assert.IsTrue(GuardAISystem.IsNoiseRetryConfigurationValid(
                NoiseResponseProfile.Target,
                NoiseRuntimeConfiguration.RegisteredMaxRetryAttempts));
            Assert.IsFalse(GuardAISystem.IsNoiseRetryConfigurationValid(
                NoiseResponseProfile.Target,
                NoiseRuntimeConfiguration.RegisteredMinRetryAttempts - 1));
            Assert.IsFalse(GuardAISystem.IsNoiseRetryConfigurationValid(
                NoiseResponseProfile.Target,
                NoiseRuntimeConfiguration.RegisteredMaxRetryAttempts + 1));
        }

        [Test]
        public void Composition_RequiresExplicitCanonicalClockAndNeverFallsBack()
        {
            Assert.IsFalse(GuardAISystem.IsAuthoritativeClockValid(null));

            VirtualTickClockService clock = new VirtualTickClockService();
            Assert.IsTrue(GuardAISystem.IsAuthoritativeClockValid(clock));
        }

        [Test]
        public void Composition_RejectsTargetWithoutAlertPropagation()
        {
            Assert.IsTrue(GuardAISystem.IsNoiseCompositionValid(
                NoiseResponseProfile.MVP, false));
            Assert.IsFalse(GuardAISystem.IsNoiseCompositionValid(
                NoiseResponseProfile.Target, false));
            Assert.IsTrue(GuardAISystem.IsNoiseCompositionValid(
                NoiseResponseProfile.Target, true));
        }

        [Test]
        public void Composition_RejectsNullPhysicsSceneProof()
        {
            GuardAISystem system = new UnityEngine.GameObject(
                "noise-contract-guard-system").AddComponent<GuardAISystem>();
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    system.ConfigurePhysicsScene(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(system.gameObject);
            }
        }

        [Test]
        public void RuntimeConfiguration_RejectsMvpUnlockedPairBoundaryAndRetryCaps()
        {
            string errorCode;
            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 8, 31, 512, 128, 512, 4, 1, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-pair-cap-not-registered", errorCode);

            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 8, 30, 512, 128, 512, 4, 2, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-boundary-frame-cap-not-registered",
                errorCode);

            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 8, 30, 512, 128, 512, 4, 1, 4,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-retry-cap-not-registered", errorCode);
        }

        [Test]
        public void RuntimeConfiguration_RejectsRetryAndQueueValuesOutsideSafeRanges()
        {
            string errorCode;
            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 8, 30, 63, 128, 512, 4, 1, 3,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-work-cap-invalid", errorCode);

            Assert.IsFalse(NoiseRuntimeConfiguration.TryValidate(
                NoiseResponseProfile.MVP,
                NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds,
                1, 8, 30, 512, 128, 512, 4, 1, 0,
                0.005f, 0.000001f, 0.25f, 4f, 2f, 1f, 0.5f,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds,
                NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters,
                out errorCode));
            Assert.AreEqual("noise-config-retry-cap-invalid", errorCode);
        }

        [Test]
        public void TransportDefaults_UseRegisteredEventBusCapacityAndRetryLimit()
        {
            SessionEventBus bus = new SessionEventBus();

            Assert.AreEqual(NoiseRuntimeConfiguration.RegisteredEventBusCapacity,
                bus.Capacity);
            Assert.AreEqual(NoiseRuntimeConfiguration.RegisteredEventBusRetryAttempts,
                bus.RetryLimit);
        }

        [Test]
        public void SourceRecord_ConstructorsCanonicalizeAuthoritativeIds()
        {
            NoiseSourceRecord movement = new NoiseSourceRecord(
                "step-02", NoiseSourceKind.Movement, "movement", "Player",
                "controller-feet", Vector3.zero, 2f, 0.5f);
            Assert.AreEqual("step:step-02", movement.SourceEventId);

            NoiseSourceRecord burst = NoiseSourceRecord.Burst(
                "noise-contract", 1, 8UL, "BurstSimulation", Vector3.zero,
                1f, 0.5f, 1f, "landing-contact");
            Assert.AreEqual("flight:8", burst.SourceEventId);
        }

        [Test]
        public void SourceOrdering_UsesNumericSuffixForCanonicalIdentities()
        {
            Assert.Less(NoiseSourceOrdering.CompareSourceEventIds(
                "step:9", "step:10"), 0);
            Assert.Less(NoiseSourceOrdering.CompareSourceEventIds(
                "flight:9", "flight:10"), 0);
            Assert.Greater(NoiseSourceOrdering.CompareSourceEventIds(
                "step:10", "step:9"), 0);
        }

        [Test]
        public void EventBus_UsesFactIdAsFinalSourceTieBreak()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-fact-tie", 1);
            List<ulong> factIds = new List<ulong>();
            bus.Subscribe<NoisePublished>(fact => factIds.Add(fact.FactId));

            NoisePublished laterFact = new NoisePublished(
                "noise-fact-tie", 1, 2UL, "step:1", "movement",
                "PlayerController", Vector3.zero, 4f, 0.5f, 0,
                Vector3.zero, "controller-feet", Vector3.zero, "Walk",
                "Walk", 1f, 0f, null, null, "Walk", "published");
            NoisePublished earlierFact = new NoisePublished(
                "noise-fact-tie", 1, 1UL, "step:1", "movement",
                "PlayerController", Vector3.zero, 4f, 0.5f, 0,
                Vector3.zero, "controller-feet", Vector3.zero, "Walk",
                "Walk", 1f, 0f, null, null, "Walk", "published");

            Assert.AreEqual(EventAdmission.Accepted, bus.Publish(laterFact).Admission);
            Assert.AreEqual(EventAdmission.Accepted, bus.Publish(earlierFact).Admission);
            bus.Drain(EventBusPhase.Hearing);

            CollectionAssert.AreEqual(new ulong[] { 1UL, 2UL }, factIds);
        }

        [Test]
        public void SourceOrdering_RejectsMalformedTypedIdentities()
        {
            string errorCode;
            Assert.IsFalse(NoiseSourceOrdering.TryValidateSourceEventId(
                "step:01", NoiseSourceKind.Movement, out errorCode));
            Assert.AreEqual("noise-source-id-leading-zero", errorCode);

            Assert.IsFalse(NoiseSourceOrdering.TryValidateSourceEventId(
                "flight:0", NoiseSourceKind.Burst, out errorCode));
            Assert.AreEqual("noise-source-id-zero", errorCode);

            Assert.IsFalse(NoiseSourceOrdering.TryValidateSourceEventId(
                "flight:opaque", NoiseSourceKind.Burst, out errorCode));
            Assert.AreEqual("noise-source-id-flight-not-numeric", errorCode);

            Assert.IsFalse(NoiseSourceOrdering.TryValidateSourceEventId(
                "step:bad:id", NoiseSourceKind.Movement, out errorCode));
            Assert.AreEqual("noise-source-id-namespace-invalid", errorCode);

            Assert.IsTrue(NoiseSourceOrdering.TryValidateSourceEventId(
                "step:step-01", NoiseSourceKind.Movement, out errorCode));
            Assert.IsEmpty(errorCode);
        }

        [Test]
        public void LegacyBurstAdapter_DoesNotFabricateAuthoritativeEnvelope()
        {
#pragma warning disable 618
            NoiseSourceRecord legacy = NoiseSourceRecord.Burst(
                "old-flight", "LegacyFixture", Vector3.zero, 1f, 0.5f,
                "legacy-contact");
#pragma warning restore 618

            Assert.AreEqual(NoiseSourceKind.Legacy, legacy.SourceKind);
            Assert.IsNull(legacy.BurstFlightHandleId);
            Assert.AreEqual(string.Empty, legacy.SessionId);
        }

        [Test]
        public void BurstPublication_UsesHandleIdentityAndTerminalPublishTime()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-burst", 3);
            NoiseEmitter emitter = new NoiseEmitter(bus);
            NoisePublished published = null;
            bus.Subscribe<NoisePublished>(fact => published = fact);

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitBurst("noise-burst", 3, 7UL, "BurstSimulation",
                    new Vector3(1f, 2f, 3f), 1.5f, 0.4f, 2.25f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            bus.Drain(EventBusPhase.Hearing);

            Assert.IsNotNull(published);
            Assert.AreEqual("burst", published.Kind);
            Assert.AreEqual("flight:7", published.SourceEventId);
            Assert.AreEqual(7UL, published.FlightHandleId.Value);
            Assert.AreEqual(published.FactId, published.SourceFactId);
            Assert.AreEqual(0.4f, published.SourceTimestamp);
            Assert.AreEqual(2.25f, published.TerminalPublicationTime);
            Assert.AreEqual(2.25f, published.TPublish);
            Assert.AreEqual("published", published.PublicationState);

            NoiseHeardRelay relay = new NoiseHeardRelay(
                "noise-burst", 3, published.FactId, "entry-01", "guard-01",
                published.Kind, published.Source, published.Position,
                published.Radius, published.SourceEventClassRank,
                published.OriginProvenance, published.AuthoritativeOrigin,
                published.SourceTimestamp, 2.3f, 0.8f,
                published.TerminalPublicationTime, published.SourceEventId,
                published.FlightHandleId, published.MovementMode,
                published.ResolvedNominalRadius, published.PublicationState);
            Assert.AreEqual(published.SourceEventId, relay.SourceEventId);
            Assert.AreEqual(published.SourceTimestamp, relay.SourceTimestamp);
            Assert.AreEqual(published.SourceEventClassRank,
                relay.SourceEventClassRank);
            Assert.AreEqual(published.SourceFactId, relay.SourceFactId);
            Assert.AreEqual(published.FlightHandleId, relay.FlightHandleId);
            Assert.AreEqual(published.TPublish, relay.TPublish);
            Assert.AreEqual(published.ResolvedNominalRadius,
                relay.ResolvedNominalRadius);
        }

        [Test]
        public void EmitterFlush_RetryKeepsSourcePendingUntilQueueFrees()
        {
            // A saturated queue makes the oldest staged source Retry; the
            // source must stay Pending with its ordering cursor parked on it,
            // then publish on the next flush once the queue drains.
            SessionEventBus saturatedBus = new SessionEventBus(capacity: 1);
            saturatedBus.BeginSession("noise-retry", 1);
            NoiseEmitter emitter = new NoiseEmitter(saturatedBus);

            Assert.AreEqual(EventAdmission.Accepted,
                saturatedBus.Publish(PhaseProbeEvent.Create("noise-retry", 1,
                    "saturator", EventBusPhase.Hearing, 0f)).Admission);
            Assert.AreEqual(1, saturatedBus.PendingEnvelopeCount);

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-retry", 1, "step-01",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(0, emitter.Flush(),
                "A full queue must publish nothing");

            NoisePublicationStatus status;
            ulong? factId;
            EventPublishResult result;
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-01", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Pending, status);
            Assert.AreEqual(EventAdmission.Retry, result.Admission);
            Assert.IsNull(factId, "A retried source has no committed fact id yet");

            // A second source must not overtake the blocked head.
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-retry", 1, "step-02",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0.1f, 4f, 1.1f).Admission);

            saturatedBus.Drain(EventBusPhase.Hearing);
            Assert.AreEqual(1, emitter.Flush(),
                "The blocked head must publish once the queue is free");
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-01", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Published, status);
            Assert.IsNotNull(factId);

            // The capacity-one bus still contains step-01. Drain it before
            // flushing step-02; the emitter's source cursor remains parked on
            // step-02 until that queue admission succeeds.
            saturatedBus.Drain(EventBusPhase.Hearing);
            Assert.AreEqual(1, emitter.Flush());
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-02", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Published, status);
            Assert.IsNotNull(factId);
        }

        [Test]
        public void EmitterFlush_QueueOverflowExhaustion_RejectsWithStableCode()
        {
            // With the bus retry budget below the emitter-side persistence, a
            // permanently saturated queue drives the oldest source to a
            // terminal Rejected state with the bus overflow code.
            SessionEventBus overflowBus = new SessionEventBus(capacity: 1,
                maxRetries: 1);
            overflowBus.BeginSession("noise-overflow", 1);
            NoiseEmitter emitter = new NoiseEmitter(overflowBus);

            Assert.AreEqual(EventAdmission.Accepted,
                overflowBus.Publish(PhaseProbeEvent.Create("noise-overflow", 1,
                    "saturator", EventBusPhase.Hearing, 0f)).Admission);

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-overflow", 1, "step-01",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(0, emitter.Flush());

            // The queue stays permanently saturated, so successive flushes
            // burn the bus retry budget and then terminate in Rejected.
            Assert.AreEqual(0, emitter.Flush());

            NoisePublicationStatus status;
            ulong? factId;
            EventPublishResult result;
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-01", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Rejected, status);
            Assert.AreEqual("event-bus-queue-overflow-rejected", result.Code);
            Assert.IsNull(factId);
        }

        [Test]
        public void EmitterRejectedPublication_DoesNotConsumeFactId()
        {
            SessionEventBus bus = new SessionEventBus(capacity: 1,
                maxRetries: 0);
            bus.BeginSession("noise-fact-id-rejection", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);

            Assert.AreEqual(EventAdmission.Accepted,
                bus.Publish(PhaseProbeEvent.Create("noise-fact-id-rejection", 1,
                    "saturator", EventBusPhase.Hearing, 0f)).Admission);
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-fact-id-rejection", 1,
                    "step-rejected", "PlayerController", "Walk", "Walk",
                    Vector3.zero, Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(0, emitter.Flush());

            NoisePublicationStatus status;
            ulong? factId;
            EventPublishResult result;
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-rejected", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Rejected, status);
            Assert.IsNull(factId);

            bus.Drain(EventBusPhase.Hearing);
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-fact-id-rejection", 1,
                    "step-published", "PlayerController", "Walk", "Walk",
                    Vector3.zero, Vector3.forward, 1f, 0.1f, 4f, 1.1f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            ulong committedFactId;
            Assert.IsTrue(emitter.TryGetFactId(NoiseSourceKind.Movement,
                "step-published", out committedFactId));
            Assert.AreEqual(1UL, committedFactId);
        }

        [Test]
        public void EmitterResetForBoundary_InvalidatesPendingSourcesOnly()
        {
            // Same-session epoch reset marks still-pending sources Invalidated,
            // keeps the published fact id queryable for its committed lifetime,
            // and keeps fact ids monotonic within the session.
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("noise-reset", 1);
            NoiseEmitter emitter = new NoiseEmitter(bus);

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-reset", 1, "step-01",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0f, 4f, 1f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            ulong firstFactId;
            Assert.IsTrue(emitter.TryGetFactId(NoiseSourceKind.Movement,
                "step-01", out firstFactId));
            ulong committedFactId = firstFactId;

            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-reset", 1, "step-02",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0.1f, 4f, 1.1f).Admission);

            emitter.ResetForBoundary("noise-reset");

            NoisePublicationStatus status;
            ulong? factId;
            EventPublishResult result;
            Assert.IsTrue(emitter.TryGetPublication(NoiseSourceKind.Movement,
                "step-02", out status, out factId, out result));
            Assert.AreEqual(NoisePublicationStatus.Invalidated, status);

            // The published source is cleared from the session-local map but
            // stays represented as never-known after the reset — requerying
            // returns Unknown without resurrecting the committed fact id.
            Assert.IsFalse(emitter.TryGetFactId(NoiseSourceKind.Movement,
                "step-01", out firstFactId));

            // Fact ids restart only for a new session, not a same-session epoch.
            Assert.AreEqual(EventAdmission.Accepted,
                emitter.SubmitMovement("noise-reset", 1, "step-03",
                    "PlayerController", "Walk", "Walk", Vector3.zero,
                    Vector3.forward, 1f, 0.2f, 4f, 1.2f).Admission);
            Assert.AreEqual(1, emitter.Flush());
            ulong thirdFactId;
            Assert.IsTrue(emitter.TryGetFactId(NoiseSourceKind.Movement,
                "step-03", out thirdFactId));
            Assert.Greater(thirdFactId, committedFactId,
                "Fact ids stay monotonic across a same-session epoch reset");
        }
    }

    /// <summary>
    /// Deterministic ingress probe used only to saturate a test bus queue.
    /// </summary>
    internal sealed class PhaseProbeEvent : IEvent, IEventPhase
    {
        private PhaseProbeEvent(string sessionId, long attemptEpoch,
            string identity, EventBusPhase phase, float timestamp)
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
        public string Publisher { get { return "NoiseContractVerificationSuite"; } }
        public string Identity { get; }
        public EventBusPhase Phase { get; }

        public static PhaseProbeEvent Create(string sessionId, long attemptEpoch,
            string identity, EventBusPhase phase, float timestamp)
        {
            return new PhaseProbeEvent(sessionId, attemptEpoch, identity,
                phase, timestamp);
        }
    }
}
