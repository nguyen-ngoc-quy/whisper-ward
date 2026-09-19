using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Core
{
    internal interface IVirtualTickClock
    {
        bool IsPaused { get; }
        float CurrentTime { get; }
    }

    internal enum EventAdmission
    {
        Accepted,
        Rejected
    }

    internal enum NoisePublicationStatus
    {
        Pending,
        Published,
        Rejected,
        Invalidated
    }

    internal struct EventPublishResult
    {
        public EventPublishResult(EventAdmission admission, string code, ulong factId)
        {
            Admission = admission;
            Code = code ?? string.Empty;
            FactId = factId;
        }

        public EventAdmission Admission { get; }
        public string Code { get; }
        public ulong FactId { get; }
    }

    internal sealed class NoiseEmitter
    {
        private sealed class Publication
        {
            public ulong FactId;
            public NoisePublicationStatus Status;
            public string Code = string.Empty;
            public int SubmissionCount;
        }

        private readonly Dictionary<string, Publication> _publications =
            new Dictionary<string, Publication>(StringComparer.Ordinal);
        private ulong _nextFactId = 1;

        public NoiseEmitter(string sessionId, long attemptEpoch)
        {
            EventBusSessionId = sessionId;
            EventBusAttemptEpoch = attemptEpoch;
        }

        public string EventBusSessionId { get; }
        public long EventBusAttemptEpoch { get; }
        public int AllocatedFactCount { get { return (int)(_nextFactId - 1); } }

        public EventPublishResult SubmitBurst(string sessionId, long attemptEpoch,
            string flightHandleId, string publisher, Vector3 terminalPosition,
            float radius, float sourceTimestamp, string provenance)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0
                || string.IsNullOrWhiteSpace(flightHandleId))
            {
                return new EventPublishResult(EventAdmission.Rejected,
                    "burst-envelope", 0);
            }

            string dedupKey = string.Concat(sessionId, "|", attemptEpoch, "|",
                flightHandleId);
            Publication publication;
            if (_publications.TryGetValue(dedupKey, out publication))
            {
                publication.SubmissionCount++;
                return new EventPublishResult(EventAdmission.Accepted,
                    publication.Code, publication.FactId);
            }

            publication = new Publication
            {
                FactId = _nextFactId++,
                Status = NoisePublicationStatus.Pending,
                SubmissionCount = 1
            };
            _publications.Add(dedupKey, publication);
            return new EventPublishResult(EventAdmission.Accepted, string.Empty,
                publication.FactId);
        }

        public bool TryGetPublication(string sessionId, long attemptEpoch,
            string flightHandleId,
            out NoisePublicationStatus status, out ulong factId,
            out EventPublishResult result)
        {
            string dedupKey = string.Concat(sessionId, "|", attemptEpoch, "|",
                flightHandleId);
            Publication publication;
            if (_publications.TryGetValue(dedupKey, out publication))
            {
                status = publication.Status;
                factId = publication.FactId;
                result = new EventPublishResult(EventAdmission.Accepted,
                    publication.Code, publication.FactId);
                return true;
            }

            status = NoisePublicationStatus.Pending;
            factId = 0;
            result = new EventPublishResult(EventAdmission.Rejected,
                "missing-publication", 0);
            return false;
        }

        public void MarkPublished(string sessionId, long attemptEpoch,
            string flightHandleId)
        {
            Publication publication = GetPublication(sessionId, attemptEpoch,
                flightHandleId);
            publication.Status = NoisePublicationStatus.Published;
        }

        public int SubmissionCountFor(string sessionId, long attemptEpoch,
            string flightHandleId)
        {
            return GetPublication(sessionId, attemptEpoch, flightHandleId)
                .SubmissionCount;
        }

        private Publication GetPublication(string sessionId, long attemptEpoch,
            string flightHandleId)
        {
            string dedupKey = string.Concat(sessionId, "|", attemptEpoch, "|",
                flightHandleId);
            Publication publication;
            if (!_publications.TryGetValue(dedupKey, out publication))
                throw new InvalidOperationException("publication-not-found");
            return publication;
        }
    }
}

namespace WhisperWard.AI.Perception
{
    internal interface IBurstPhysicsQuery
    {
        void PrepareBatch();
        bool HasInitialOverlap(Vector3 origin, float projectileRadiusMeters);
        ClosestContactResult Sweep(Vector3 current, Vector3 next,
            float projectileRadiusMeters);
    }

    internal struct ClosestContactResult
    {
        public ClosestContactResult(bool hasContact, float distanceAlongSegment,
            Vector3 point, Vector3 normal)
        {
            HasContact = hasContact;
            DistanceAlongSegment = distanceAlongSegment;
            Point = point;
            Normal = normal;
        }

        public bool HasContact { get; }
        public float DistanceAlongSegment { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
    }

    internal static class BurstCollisionResolver
    {
        public static Vector3 GetPublishedContact(ClosestContactResult contact,
            float projectileRadiusMeters, float epsilonContactMeters)
        {
            Vector3 normal = contact.Normal == Vector3.zero
                ? Vector3.up : contact.Normal.normalized;
            return contact.Point + normal
                * (projectileRadiusMeters + epsilonContactMeters);
        }
    }

    internal sealed class BurstRuntimeConfiguration
    {
        public float PickupReachRadiusMeters { get; set; } = 2f;
        public float ReleaseHeightMeters { get; set; } = 1.5f;
        public float ProjectileRadiusMeters { get; set; } = 0.05f;
        public float LaunchAngleDegrees { get; set; } = 38f;
        public float InitialSpeedMetersPerSecond { get; set; } = 10f;
        public float GravityMagnitude { get; set; } = 9.81f;
        public float FlightTimeoutSeconds { get; set; } = 3f;
        public float FixedSubstepSeconds { get; set; } = 0.5f;
        public int MaxBacklogTicks { get; set; } = 12;
        public float HearingRadiusMeters { get; set; } = 10.5f;
        public float EpsilonContactMeters { get; set; } = 0.001f;
    }
}

namespace WhisperWard.AI.Testing
{
    [TestFixture]
    public sealed class BurstSimulationServiceVerificationSuite
    {
        private const string SessionId = "session-burst";
        private const long AttemptEpoch = 7;
        private static readonly Vector3 PlayerPosition = new Vector3(1f, 0f, 1f);
        private static readonly Vector3 PickupPosition = new Vector3(1.5f, 0f, 1f);
        private static readonly Vector3 Forward = new Vector3(1f, 0f, 0f);

        [Test]
        public void FlightHandleId_IsStringSourceIdentity_AndIsNotReused()
        {
            BurstHarness harness = new BurstHarness();

            harness.PickupAndThrow(1.25f, out BurstThrowSnapshot firstSnapshot);
            harness.AdvanceTicks(6);
            BurstTerminalRecord firstTerminal = harness.LastTerminal();

            Assert.That(firstSnapshot.FlightHandleId, Is.Not.Empty);
            Assert.That(harness.Service.CurrentFlightHandleId, Is.EqualTo(string.Empty));
            Assert.That(firstTerminal.FlightHandleId, Is.EqualTo(firstSnapshot.FlightHandleId));

            NoiseSourceRecord firstSource = NoiseSourceRecord.Burst(SessionId,
                AttemptEpoch, firstSnapshot.FlightHandleId, "BurstSimulation",
                firstTerminal.TerminalPosition, 10.5f, firstSnapshot.SourceTimestamp,
                "burst-landing");
            Assert.That(firstSource.SourceEventId, Is.EqualTo(firstSnapshot.FlightHandleId));
            Assert.That(firstSource.BurstFlightHandleId, Is.EqualTo(firstSnapshot.FlightHandleId));
            Assert.That(
                () => new BurstThrowSnapshot(" ", "BurstSimulation", PlayerPosition,
                    Forward, Vector3.zero, 1f),
                Throws.ArgumentException.With.Message.EqualTo("flightHandleId"));
            Assert.That(
                () => NoiseSourceRecord.Burst(SessionId, AttemptEpoch, " ",
                    "BurstSimulation", firstTerminal.TerminalPosition, 10.5f,
                    firstSnapshot.SourceTimestamp, "burst-landing"),
                Throws.ArgumentException.With.Message.EqualTo("flightHandleId"));

            harness.Service.ResetForSession();
            harness.PickupAndThrow(1.25f, out BurstThrowSnapshot secondSnapshot);

            Assert.That(secondSnapshot.FlightHandleId, Is.Not.Empty);
            Assert.That(secondSnapshot.FlightHandleId, Is.Not.EqualTo(firstSnapshot.FlightHandleId));
        }

        [Test]
        public void ContactAtTimeoutBoundary_WinsAndPublishesOneFact()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.ScheduleContact(6, 1f, new Vector3(4f, 0f, 1f), Vector3.up);
            harness.PickupAndThrow(1f, out BurstThrowSnapshot snapshot);

            harness.AdvanceTicks(6);
            BurstTerminalRecord terminal = harness.LastTerminal();

            Assert.That(terminal.TerminalEvent, Is.EqualTo(BurstTerminalEvent.Contact));
            Assert.That(terminal.ContactEventTime, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(terminal.TimeoutBoundarySeconds, Is.EqualTo(3f));
            Assert.That(terminal.ComparisonResult, Is.EqualTo("contact_before_or_at_timeout"));
            Assert.That(terminal.FactPublicationState, Is.EqualTo(BurstFactPublicationState.Pending));
            Assert.That(terminal.FactId, Is.GreaterThan(0ul));
            Assert.That(harness.Emitter.SubmissionCountFor(SessionId, AttemptEpoch,
                snapshot.FlightHandleId), Is.EqualTo(1));

            harness.Emitter.MarkPublished(SessionId, AttemptEpoch, snapshot.FlightHandleId);
            harness.Service.ResolvePendingTerminalPublication();
            terminal = harness.LastTerminal();

            Assert.That(terminal.FactPublicationState, Is.EqualTo(BurstFactPublicationState.Published));
            Assert.That(terminal.FactId, Is.GreaterThan(0ul));
        }

        [Test]
        public void ContactAfterTimeoutBoundary_ResolvesTimeoutWithoutLateFact()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.ScheduleContact(7, 0.5f, new Vector3(5f, 0f, 1f), Vector3.up);
            harness.PickupAndThrow(1f, out BurstThrowSnapshot snapshot);

            harness.AdvanceTicks(6);
            BurstTerminalRecord terminal = harness.LastTerminal();
            int submissionsAtTimeout = harness.Emitter.SubmissionCountFor(SessionId,
                AttemptEpoch, snapshot.FlightHandleId);

            harness.AdvanceTicks(2);

            Assert.That(terminal.TerminalEvent, Is.EqualTo(BurstTerminalEvent.Timeout));
            Assert.That(terminal.ContactFraction, Is.Null);
            Assert.That(terminal.ContactEventTime, Is.Null);
            Assert.That(terminal.ComparisonResult, Is.EqualTo("timeout_wins"));
            Assert.That(terminal.FactPublicationState, Is.EqualTo(BurstFactPublicationState.Pending));
            Assert.That(terminal.FactId, Is.GreaterThan(0ul));
            Assert.That(harness.Emitter.SubmissionCountFor(SessionId, AttemptEpoch,
                snapshot.FlightHandleId), Is.EqualTo(submissionsAtTimeout));
            Assert.That(harness.LastTerminal(), Is.EqualTo(terminal));
        }

        [Test]
        public void DeathCancellation_WinsBeforeLateCallback_AndDoesNotRefund()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.ScheduleContact(4, 0.5f, new Vector3(3f, 0f, 1f), Vector3.up);
            harness.PickupAndThrow(1f, out BurstThrowSnapshot snapshot);

            harness.AdvanceTicks(1);
            bool cancelled = harness.Service.CancelForDeath();
            BurstTerminalRecord terminal = harness.LastTerminal();
            harness.AdvanceTicks(4);

            Assert.That(cancelled, Is.True);
            Assert.That(terminal.TerminalEvent, Is.EqualTo(BurstTerminalEvent.DeathCancelled));
            Assert.That(terminal.ComparisonResult, Is.EqualTo("death_wins"));
            Assert.That(terminal.ContactFraction, Is.Null);
            Assert.That(terminal.ContactEventTime, Is.Null);
            Assert.That(terminal.CancellationReason, Is.EqualTo("death_cancelled"));
            Assert.That(terminal.FactPublicationState, Is.EqualTo(BurstFactPublicationState.NotApplicable));
            Assert.That(terminal.FactId, Is.EqualTo(0ul));
            Assert.That(terminal.NoRefund, Is.True);
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(0));
            Assert.That(snapshot.FlightHandleId, Is.Not.Empty);
            Assert.That(harness.Service.State, Is.EqualTo(BurstFlightState.Consumed));
        }

        [Test]
        public void InitialOverlap_IsRejectedBeforeSpend()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.InitialOverlap = true;
            Assert.That(harness.Service.TryPickup("pickup-01", PlayerPosition,
                PickupPosition), Is.EqualTo(BurstThrowAdmission.Accepted));

            BurstThrowAdmission admission = harness.Service.TryThrow(
                harness.CreateRequest(1f), out BurstThrowSnapshot snapshot);

            Assert.That(admission, Is.EqualTo(BurstThrowAdmission.Rejected));
            Assert.That(harness.Service.State, Is.EqualTo(BurstFlightState.Carried));
            Assert.That(harness.Service.LastRejectionCode, Is.EqualTo("INITIAL_OVERLAP_REJECTED"));
            Assert.That(string.IsNullOrEmpty(snapshot.FlightHandleId), Is.True);
            Assert.That(harness.Service.CurrentFlightHandleId, Is.EqualTo(string.Empty));
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(0));
        }

        [Test]
        public void RetryAndDuplicatePublish_PreserveFactId()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.ScheduleContact(1, 0.5f, new Vector3(2f, 0f, 1f), Vector3.up);
            harness.PickupAndThrow(1f, out BurstThrowSnapshot snapshot);
            harness.AdvanceTicks(1);

            BurstTerminalRecord terminal = harness.LastTerminal();
            EventPublishResult duplicate = harness.Emitter.SubmitBurst(SessionId,
                AttemptEpoch, snapshot.FlightHandleId, "BurstSimulation",
                terminal.TerminalPosition, 10.5f, snapshot.SourceTimestamp,
                "burst-landing");

            Assert.That(terminal.FactId, Is.GreaterThan(0ul));
            Assert.That(duplicate.FactId, Is.EqualTo(terminal.FactId));
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(1));
            Assert.That(harness.Emitter.SubmissionCountFor(SessionId, AttemptEpoch,
                snapshot.FlightHandleId), Is.EqualTo(2));

            harness.Emitter.MarkPublished(SessionId, AttemptEpoch, snapshot.FlightHandleId);
            harness.Service.ResolvePendingTerminalPublication();
            terminal = harness.LastTerminal();

            Assert.That(terminal.FactPublicationState, Is.EqualTo(BurstFactPublicationState.Published));
            Assert.That(terminal.FactId, Is.EqualTo(duplicate.FactId));
        }

        [Test]
        public void PauseResume_FreezesFlightTime_AndDoesNotBufferAudio()
        {
            BurstHarness harness = new BurstHarness();
            harness.Physics.ScheduleContact(2, 0.5f, new Vector3(2.5f, 0f, 1f), Vector3.up);
            harness.PickupAndThrow(1f, out BurstThrowSnapshot snapshot);
            Vector3 prePausePosition = harness.Service.CurrentPosition;

            harness.Clock.SetPaused(true);
            harness.AdvanceTicks(2);
            Vector3 pausedPosition = harness.Service.CurrentPosition;

            Assert.That(pausedPosition, Is.EqualTo(prePausePosition));
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(0));
            Assert.That(harness.Service.LastBatch.TickCount, Is.EqualTo(0));

            harness.Clock.SetPaused(false);
            harness.AdvanceTicks(1);
            Assert.That(harness.Service.HasActiveFlight, Is.True);
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(0));

            harness.AdvanceTicks(1);
            BurstTerminalRecord terminal = harness.LastTerminal();

            Assert.That(snapshot.FlightHandleId, Is.Not.Empty);
            Assert.That(terminal.TerminalEvent, Is.EqualTo(BurstTerminalEvent.Contact));
            Assert.That(terminal.FactId, Is.GreaterThan(0ul));
            Assert.That(harness.Emitter.AllocatedFactCount, Is.EqualTo(1));
        }

        private sealed class BurstHarness
        {
            private long _tick;

            public BurstHarness()
            {
                Clock = new FakeClock();
                Emitter = new NoiseEmitter(SessionId, AttemptEpoch);
                Physics = new FakePhysicsQuery();
                Service = new BurstSimulationService(new BurstRuntimeConfiguration(),
                    Physics, Emitter, Clock);
            }

            public FakeClock Clock { get; }
            public NoiseEmitter Emitter { get; }
            public FakePhysicsQuery Physics { get; }
            public BurstSimulationService Service { get; }

            public BurstThrowRequest CreateRequest(float sourceTimestamp)
            {
                return new BurstThrowRequest(SessionId, AttemptEpoch,
                    "BurstSimulation", PlayerPosition, Forward, Vector3.zero,
                    sourceTimestamp);
            }

            public void PickupAndThrow(float sourceTimestamp,
                out BurstThrowSnapshot acceptedSnapshot)
            {
                Assert.That(Service.TryPickup("pickup-01", PlayerPosition,
                    PickupPosition), Is.EqualTo(BurstThrowAdmission.Accepted));
                Assert.That(Service.TryThrow(CreateRequest(sourceTimestamp),
                    out acceptedSnapshot), Is.EqualTo(BurstThrowAdmission.Accepted));
                Assert.That(Service.CurrentFlightHandleId,
                    Is.EqualTo(acceptedSnapshot.FlightHandleId));
            }

            public void AdvanceTicks(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Clock.Advance(0.5f);
                    Service.Advance(++_tick, 0.5f);
                }
            }

            public BurstTerminalRecord LastTerminal()
            {
                BurstTerminalRecord terminal;
                if (!Service.TryGetLastTerminal(out terminal))
                    Assert.Fail("Expected terminal record.");
                return terminal;
            }
        }

        private sealed class FakeClock : IVirtualTickClock
        {
            public bool IsPaused { get; private set; }
            public float CurrentTime { get; private set; }

            public void SetPaused(bool isPaused)
            {
                IsPaused = isPaused;
            }

            public void Advance(float delta)
            {
                if (!IsPaused)
                    CurrentTime += delta;
            }
        }

        private sealed class FakePhysicsQuery : IBurstPhysicsQuery
        {
            private readonly Dictionary<int, ScheduledContact> _contacts =
                new Dictionary<int, ScheduledContact>();
            private int _sweepCount;

            public bool InitialOverlap { get; set; }

            public void ScheduleContact(int sweepNumber, float fraction,
                Vector3 point, Vector3 normal)
            {
                _contacts[sweepNumber] = new ScheduledContact(fraction, point, normal);
            }

            public void PrepareBatch()
            {
            }

            public bool HasInitialOverlap(Vector3 origin, float projectileRadiusMeters)
            {
                return InitialOverlap;
            }

            public ClosestContactResult Sweep(Vector3 current, Vector3 next,
                float projectileRadiusMeters)
            {
                _sweepCount++;
                ScheduledContact scheduled;
                if (!_contacts.TryGetValue(_sweepCount, out scheduled))
                    return new ClosestContactResult(false, 0f, Vector3.zero, Vector3.zero);

                float segmentLength = Vector3.Distance(current, next);
                return new ClosestContactResult(true,
                    Mathf.Clamp01(scheduled.Fraction) * segmentLength,
                    scheduled.Point, scheduled.Normal);
            }

            private struct ScheduledContact
            {
                public ScheduledContact(float fraction, Vector3 point, Vector3 normal)
                {
                    Fraction = fraction;
                    Point = point;
                    Normal = normal;
                }

                public float Fraction { get; }
                public Vector3 Point { get; }
                public Vector3 Normal { get; }
            }
        }
    }
}
