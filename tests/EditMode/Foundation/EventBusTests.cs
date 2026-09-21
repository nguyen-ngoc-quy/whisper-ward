using System;
using System.Collections.Generic;
using NUnit.Framework;
using WhisperWard.Foundation.Events;

namespace WhisperWard.Tests.EditMode.Foundation
{
    public sealed class EventBusTests
    {
        private sealed class TestNoiseEvent : IEvent
        {
            public string EventType => "TestNoiseEvent";
            public readonly float Decibels;

            public TestNoiseEvent(float decibels)
            {
                Decibels = decibels;
            }
        }

        [Test]
        public void test_event_bus_immutable_envelope_and_subscription_delivery()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);
            var receivedPayloads = new List<TestNoiseEvent>();

            bus.Subscribe<TestNoiseEvent>(ev => receivedPayloads.Add(ev), VirtualClockPhase.Sensor);

            var envelope = new EventEnvelope(
                "session-1",
                1,
                10.5,
                "player",
                VirtualClockPhase.Sensor,
                "Noise",
                1001,
                new TestNoiseEvent(65.0f)
            );

            // Act
            var publishResult = bus.Publish(in envelope);
            int drainedCount = bus.DrainPhase(VirtualClockPhase.Sensor);

            // Assert
            Assert.That(publishResult.Status, Is.EqualTo(EventHandoffStatus.Accepted));
            Assert.That(drainedCount, Is.EqualTo(1));
            Assert.That(receivedPayloads.Count, Is.EqualTo(1));
            Assert.That(receivedPayloads[0].Decibels, Is.EqualTo(65.0f));
        }

        [Test]
        public void test_event_bus_ingress_deduplication_rejects_duplicate_identity()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);

            var env1 = new EventEnvelope("session-1", 1, 1.0, "player", VirtualClockPhase.Sensor, "Noise", 42, new TestNoiseEvent(50f));
            var env2 = new EventEnvelope("session-1", 1, 2.0, "player", VirtualClockPhase.Sensor, "Noise", 42, new TestNoiseEvent(75f));

            // Act
            var res1 = bus.Publish(in env1);
            var res2 = bus.Publish(in env2);

            // Assert
            Assert.That(res1.Status, Is.EqualTo(EventHandoffStatus.Accepted));
            Assert.That(res2.Status, Is.EqualTo(EventHandoffStatus.Duplicate));
            Assert.That(res2.RejectionReason, Is.EqualTo("event-bus-duplicate-identity"));
            Assert.That(bus.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void test_event_bus_queue_overflow_rejects_newest()
        {
            // Arrange: bounded queue capacity = 2
            var bus = new EventBus("session-1", 1, capacity: 2);

            var env1 = new EventEnvelope("session-1", 1, 1.0, "player", VirtualClockPhase.Sensor, "Noise", 1, new TestNoiseEvent(10f));
            var env2 = new EventEnvelope("session-1", 1, 2.0, "player", VirtualClockPhase.Sensor, "Noise", 2, new TestNoiseEvent(20f));
            var env3 = new EventEnvelope("session-1", 1, 3.0, "player", VirtualClockPhase.Sensor, "Noise", 3, new TestNoiseEvent(30f));

            // Act
            var res1 = bus.Publish(in env1);
            var res2 = bus.Publish(in env2);
            var res3 = bus.Publish(in env3);

            // Assert
            Assert.That(res1.Status, Is.EqualTo(EventHandoffStatus.Accepted));
            Assert.That(res2.Status, Is.EqualTo(EventHandoffStatus.Accepted));
            Assert.That(res3.Status, Is.EqualTo(EventHandoffStatus.Rejected));
            Assert.That(res3.RejectionReason, Is.EqualTo("event-bus-queue-overflow-rejected"));
            Assert.That(bus.PendingCount, Is.EqualTo(2));
        }

        [Test]
        public void test_event_bus_virtual_clock_deterministic_ordering_tuple()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);
            var dispatchLog = new List<ulong>();

            bus.Subscribe<TestNoiseEvent>(ev => { }, VirtualClockPhase.Sensor);
            bus.SubscribeHandoff(env =>
            {
                dispatchLog.Add(env.EventIdentity);
                return EventHandoffResult.Accepted;
            }, VirtualClockPhase.Sensor);

            // Publish shuffled: (Time, Rank)
            // Item A: Time=2.0, Rank=1, Id=10
            // Item B: Time=1.0, Rank=2, Id=20
            // Item C: Time=1.0, Rank=1, Id=30 (should be first)
            var envA = new EventEnvelope("session-1", 1, 2.0, "p", VirtualClockPhase.Sensor, "N", 10, new TestNoiseEvent(1f), sourceClassRank: 1);
            var envB = new EventEnvelope("session-1", 1, 1.0, "p", VirtualClockPhase.Sensor, "N", 20, new TestNoiseEvent(1f), sourceClassRank: 2);
            var envC = new EventEnvelope("session-1", 1, 1.0, "p", VirtualClockPhase.Sensor, "N", 30, new TestNoiseEvent(1f), sourceClassRank: 1);

            bus.Publish(in envA);
            bus.Publish(in envB);
            bus.Publish(in envC);

            // Act
            bus.DrainPhase(VirtualClockPhase.Sensor);

            // Assert: Ordering must be C (t=1.0, r=1), then B (t=1.0, r=2), then A (t=2.0, r=1)
            Assert.That(dispatchLog, Is.EqualTo(new List<ulong> { 30, 20, 10 }));
        }

        [Test]
        public void test_event_bus_epoch_barrier_invalidates_stale_events()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);
            var delivered = new List<ulong>();

            bus.SubscribeHandoff(env =>
            {
                delivered.Add(env.EventIdentity);
                return EventHandoffResult.Accepted;
            }, VirtualClockPhase.Sensor);

            var envEpoch1 = new EventEnvelope("session-1", 1, 1.0, "p", VirtualClockPhase.Sensor, "N", 101, new TestNoiseEvent(1f));
            bus.Publish(in envEpoch1);

            // Act: Advance epoch barrier to 2
            bus.BeginEpoch(2);

            // Drain phase
            int drained = bus.DrainPhase(VirtualClockPhase.Sensor);

            // Assert: Pending event from epoch 1 was purged atomically
            Assert.That(drained, Is.EqualTo(0));
            Assert.That(delivered.Count, Is.EqualTo(0));
            Assert.That(bus.CurrentEpoch, Is.EqualTo(2));
        }

        [Test]
        public void test_event_bus_session_barrier_clears_pending_and_dedup()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);

            var envOld = new EventEnvelope("session-1", 1, 1.0, "p", VirtualClockPhase.Sensor, "N", 201, new TestNoiseEvent(1f));
            bus.Publish(in envOld);
            Assert.That(bus.PendingCount, Is.EqualTo(1));

            // Act: Start new session
            bus.BeginSession("session-2");

            // Assert: Pending queue is empty
            Assert.That(bus.PendingCount, Is.EqualTo(0));
            Assert.That(bus.CurrentSessionId, Is.EqualTo("session-2"));
            Assert.That(bus.CurrentEpoch, Is.EqualTo(0));

            // Old session event should be rejected
            var resOld = bus.Publish(in envOld);
            Assert.That(resOld.Status, Is.EqualTo(EventHandoffStatus.Rejected));
            Assert.That(resOld.RejectionReason, Is.EqualTo("event-bus-stale-session"));

            // New session event with same identity can be admitted because dedup cleared
            var envNew = new EventEnvelope("session-2", 0, 1.0, "p", VirtualClockPhase.Sensor, "N", 201, new TestNoiseEvent(1f));
            var resNew = bus.Publish(in envNew);
            Assert.That(resNew.Status, Is.EqualTo(EventHandoffStatus.Accepted));
        }

        [Test]
        public void test_event_bus_unsubscribe_prevents_further_delivery()
        {
            // Arrange
            var bus = new EventBus("session-1", 1);
            int callCount = 0;
            var token = bus.Subscribe<TestNoiseEvent>(_ => callCount++, VirtualClockPhase.Sensor);

            var env = new EventEnvelope("session-1", 1, 1.0, "p", VirtualClockPhase.Sensor, "N", 301, new TestNoiseEvent(1f));
            bus.Publish(in env);

            // Act: Unsubscribe and then drain
            bool unsubResult = bus.Unsubscribe(token);
            bus.DrainPhase(VirtualClockPhase.Sensor);

            // Assert
            Assert.That(unsubResult, Is.True);
            Assert.That(callCount, Is.EqualTo(0));
        }
    }
}
