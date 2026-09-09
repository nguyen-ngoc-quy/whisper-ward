using System;
using System.Collections.Generic;
using NUnit.Framework;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies Event Bus delivery, session barriers, and idempotent room
    /// finalization for the Suspicion Meter / Grade composition adapter.
    /// </summary>
    public sealed class SuspicionGradeSystemTests
    {
        [Test]
        public void test_system_accepts_snapshot_and_does_not_mutate_source_projection()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 25f, 10f, 100f, 20f, "Investigate");

            system.AcceptSnapshot(snapshot);

            Assert.That(presenter.LastReadModel, Is.Not.Null);
            Assert.That(presenter.LastReadModel.MeterPercent, Is.EqualTo(25));
            Assert.That(presenter.LastReadModel.RCurrent, Is.EqualTo(10f));
            Assert.That(snapshot.ACurrent, Is.EqualTo(25f));
            Assert.That(snapshot.RCurrent, Is.EqualTo(10f));
            system.Dispose();
        }

        [Test]
        public void test_system_delivers_typed_snapshot_through_presentation_phase()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            var snapshotEvent = new SuspicionSnapshotEvent(
                Snapshot("session", 1, 40f, 4f, 100f, 20f, "Investigate"),
                "snapshot-1", 0f);

            Assert.That(bus.Publish(snapshotEvent).IsAccepted, Is.True);
            Assert.That(presenter.LastReadModel, Is.Null);
            Assert.That(bus.Drain(EventBusPhase.Presentation), Is.EqualTo(1));

            Assert.That(presenter.LastReadModel, Is.Not.Null);
            Assert.That(presenter.LastReadModel.MeterPercent, Is.EqualTo(40));
            system.Dispose();
        }

        [Test]
        public void test_system_delivers_typed_trace_and_boundary_through_presentation_phase()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => publicationCount++);

            Assert.That(bus.Publish(new GradeTraceEvent(
                ChaseTrace("session", 1, "entry-1", "trace-event"), 0f)).IsAccepted,
                Is.True);
            Assert.That(bus.Publish(new RoomCompletionBoundaryEvent(
                CompletedBoundary("session", 1, "room", 0f), 0f)).IsAccepted,
                Is.True);

            Assert.That(bus.Drain(EventBusPhase.Presentation), Is.EqualTo(2));
            Assert.That(publicationCount, Is.EqualTo(0));
            Assert.That(bus.Drain(EventBusPhase.Presentation), Is.EqualTo(1));
            Assert.That(publicationCount, Is.EqualTo(1));
            system.Dispose();
        }

        [Test]
        public void test_system_rejects_decision_record_without_explicit_context()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("session", 1);
            SessionBoundaryService boundary = new SessionBoundaryService(bus);
            var presenter = new FakeSuspicionMeterPresenter();
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration(),
                presenter, null);
            var decision = new ChaseEntry
            {
                EntryId = "entry-1",
                Identity = "decision-1",
                SessionId = "session",
                AttemptEpoch = 1,
                Timestamp = 1f
            };

            Assert.That(bus.Publish(decision).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));

            Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(result.ContributingEventIds, Has.Count.EqualTo(0));
            system.Dispose();
        }

        [Test]
        public void test_system_isolates_throwing_presenter_from_projection_state()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            ThrowingSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateThrowingPresenterSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 25f, 10f, 100f, 20f, "Investigate");

            Assert.DoesNotThrow(() => system.AcceptSnapshot(snapshot));
            SuspicionMeterReadModel readModel;
            Assert.That(system.TryGetReadModel(snapshot.Identity, out readModel),
                Is.True);
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-presenter-failed"));
            system.Dispose();
        }

        [Test]
        public void test_system_unbind_stops_typed_event_delivery_without_losing_safety()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            system.Unbind();

            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 25f, 10f, 100f, 20f, "Investigate"),
                "snapshot-after-unbind", 0f)).IsAccepted, Is.True);
            Assert.That(bus.Drain(EventBusPhase.Presentation), Is.EqualTo(0));
            Assert.That(presenter.LastReadModel, Is.Null);
            system.Dispose();
        }

        [Test]
        public void test_system_adapts_decision_record_only_with_explicit_room_and_guard_binding()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            system.BindDecisionContext("room", "guard-1");
            var decision = new ChaseEntry
            {
                EntryId = "entry-1",
                Identity = "decision-1",
                SessionId = "session",
                AttemptEpoch = 1,
                Timestamp = 1f
            };

            Assert.That(bus.Publish(decision).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));

            Assert.That(result.IncidentPenalty, Is.EqualTo(25f));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "decision-1" }));
            system.Dispose();
        }

        [Test]
        public void test_system_ignores_old_epoch_after_session_boundary()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            system.AcceptTrace(ChaseTrace("session", 1, "old-entry", "old-event"));

            boundary.BeginEpoch(2);
            system.AcceptTrace(ChaseTrace("session", 1,
                "late-old-entry", "late-old-event"));
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 2, "room", 0f));

            Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(result.ContributingEventIds,
                Does.Not.Contain("old-event"));
            Assert.That(result.ContributingEventIds,
                Does.Not.Contain("late-old-event"));
            system.Dispose();
        }

        [Test]
        public void test_system_reset_clears_read_models_traces_and_finalization_cache()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 35f, 5f, 100f, 20f, "Investigate");
            system.AcceptSnapshot(snapshot);
            system.AcceptTrace(ChaseTrace("session", 1, "entry-1", "event-1"));
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);
            RoomGradeFinalized first = system.FinalizeRoom(room);

            system.ResetForBoundary("session", 1);
            SuspicionMeterReadModel readModel;
            RoomGradeFinalized second = system.FinalizeRoom(room);

            Assert.That(system.TryGetReadModel(snapshot.Identity, out readModel),
                Is.False);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(second.ContributingEventIds, Has.Count.EqualTo(0));
            system.Dispose();
        }

        [Test]
        public void test_system_returns_same_result_and_publishes_one_final_event_for_retries()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => publicationCount++);
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);

            RoomGradeFinalized first = system.FinalizeRoom(room);
            RoomGradeFinalized second = system.FinalizeRoom(room);
            bus.Drain(EventBusPhase.Presentation);

            Assert.That(second, Is.SameAs(first));
            Assert.That(system.FinalizedCount, Is.EqualTo(1));
            Assert.That(publicationCount, Is.EqualTo(1));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_rejects_malformed_trace_without_erasing_valid_state()
        {
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            system.AcceptTrace(ChaseTrace("session", 1,
                "valid-entry", "valid-event"));

            Assert.DoesNotThrow(() => system.AcceptTrace(new GradeTraceRecord(
                "", 1, "room", "guard-1", "bad-entry",
                GradeIncidentType.ChaseEntry, null, "bad-event")));
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));

            Assert.That(result.IncidentPenalty, Is.EqualTo(25f));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "valid-event" }));
            system.Dispose();
        }

        [Test]
        public void test_system_allows_unavailable_presenter_without_concrete_ui()
        {
            SessionEventBus bus = new SessionEventBus();
            bus.BeginSession("session", 1);
            SessionBoundaryService boundary = new SessionBoundaryService(bus);
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration());

            Assert.DoesNotThrow(() => system.AcceptSnapshot(
                new GuardSuspicionSnapshot(
                    new SuspicionGradeIdentity("session", 1, "room", "guard-1"),
                    false, 0f, true, 0f, true, 100f, 20f,
                    "Patrol", "", "")));
            SuspicionMeterReadModel model;
            Assert.That(system.TryGetReadModel(
                new SuspicionGradeIdentity("session", 1, "room", "guard-1"),
                out model), Is.True);
            Assert.That(model.IsAvailable, Is.False);
            system.Dispose();
        }

        [Test]
        public void test_system_typed_events_preserve_identity_and_presentation_phase()
        {
            // Arrange
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 25f, 10f, 100f, 20f, "Investigate");
            GradeTraceRecord trace = ChaseTrace(
                "session", 1, "entry-1", "event-1");
            RoomCompletionBoundary boundary = CompletedBoundary(
                "session", 1, "room", 0f);

            // Act
            var snapshotEvent = new SuspicionSnapshotEvent(
                snapshot, "snapshot-1", 2f);
            var traceEvent = new GradeTraceEvent(trace, 3f);
            var boundaryEvent = new RoomCompletionBoundaryEvent(boundary, 4f);
            var finalized = new RoomGradeFinalized(
                "session", 1, "room", 1, GradeCompletionStatus.Completed,
                new List<GuardGradeBreakdown>(), 0f, 0f, 100f,
                RoomGrade.S, new string[0]);
            var finalizedEvent = new RoomGradeFinalizedEvent(finalized, 5f);

            // Assert
            Assert.That(snapshotEvent.Identity, Is.EqualTo("snapshot-1"));
            Assert.That(snapshotEvent.Phase, Is.EqualTo(EventBusPhase.Presentation));
            Assert.That(traceEvent.EventId, Is.EqualTo("event-1"));
            Assert.That(traceEvent.Identity, Is.EqualTo("event-1"));
            Assert.That(traceEvent.Phase, Is.EqualTo(EventBusPhase.Presentation));
            Assert.That(boundaryEvent.Identity,
                Is.EqualTo("room-boundary:session|1|room"));
            Assert.That(boundaryEvent.Phase,
                Is.EqualTo(EventBusPhase.Presentation));
            Assert.That(finalizedEvent.Identity,
                Is.EqualTo("room-grade:session|1|room"));
            Assert.That(finalizedEvent.Phase,
                Is.EqualTo(EventBusPhase.Presentation));
        }

        [Test]
        public void test_system_rejects_null_trace_without_erasing_valid_state()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            system.AcceptTrace(ChaseTrace(
                "session", 1, "entry-1", "event-1"));

            // Act
            Assert.DoesNotThrow(() => system.AcceptTrace(null));
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));

            // Assert
            Assert.That(result.IncidentPenalty, Is.EqualTo(25f));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "event-1" }));
            system.Dispose();
        }

        [Test]
        public void test_system_retries_final_result_publication_after_queue_retry()
        {
            // Arrange
            var bus = new SessionEventBus(1, 3);
            bus.BeginSession("session", 1);
            var boundary = new SessionBoundaryService(bus);
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration());
            system.BindDecisionContext("room", "guard-1");
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => publicationCount++);
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 20f, 0f, 100f, 20f, "Investigate"),
                "queue-filler", 1f)).Admission, Is.EqualTo(EventAdmission.Accepted));
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);

            // Act
            RoomGradeFinalized first = system.FinalizeRoom(room);
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-final-result-publication-retry"));
            bus.Drain(EventBusPhase.Presentation);
            RoomGradeFinalized second = system.FinalizeRoom(room);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(second, Is.SameAs(first));
            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-final-result-publication-recovered"));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_retries_final_result_publication_with_stable_timestamp_after_intervening_event()
        {
            // Arrange
            var bus = new SessionEventBus(1, 3);
            bus.BeginSession("session", 1);
            var boundary = new SessionBoundaryService(bus);
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration());
            float observedTimestamp = -1f;
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt =>
                {
                    observedTimestamp = evt.Timestamp;
                    publicationCount++;
                });
            Assert.That(bus.Publish(new GradeTraceEvent(
                ChaseTrace("session", 1, "timestamp-entry", "timestamp-trace"),
                42.5f)).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 20f, 0f, 100f, 20f, "Investigate"),
                "queue-filler", 50f)).IsAccepted, Is.True);
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);

            // Act
            RoomGradeFinalized first = system.FinalizeRoom(room);
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-final-result-publication-retry"));
            bus.Drain(EventBusPhase.Presentation);
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 30f, 0f, 100f, 20f, "Investigate"),
                "intervening-snapshot", 99f)).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            RoomGradeFinalized second = system.FinalizeRoom(room);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(second, Is.SameAs(first));
            Assert.That(observedTimestamp, Is.EqualTo(42.5f));
            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-final-result-publication-recovered"));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_records_sink_for_stale_snapshot_event()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            var sink = new CapturingDiagnosticSink();
            SuspicionGradeSystem system = CreateSystemWithSink(
                out bus, out boundary, out presenter, sink);
            system.ResetForBoundary("session", 2);
            var snapshotEvent = new SuspicionSnapshotEvent(
                Snapshot("session", 1, 20f, 0f, 100f, 20f, "Investigate"),
                "stale-snapshot-event", 7f);

            // Act
            Assert.That(bus.Publish(snapshotEvent).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-stale-snapshot"));
            Assert.That(sink.HasCode("suspicion-grade-stale-snapshot"),
                Is.True);
            Assert.That(sink.LastEnvelope, Is.Not.Null);
            Assert.That(sink.LastEnvelope.Identity,
                Is.EqualTo("stale-snapshot-event"));
            Assert.That(sink.LastEnvelope.SessionId, Is.EqualTo("session"));
            Assert.That(sink.LastEnvelope.AttemptEpoch, Is.EqualTo(1));
            Assert.That(sink.LastEnvelope.Timestamp, Is.EqualTo(7f));
            Assert.That(sink.LastEnvelope.Publisher, Is.EqualTo("Perception"));
            Assert.That(sink.LastEnvelope.Phase,
                Is.EqualTo(EventBusPhase.Presentation));
            system.Dispose();
        }

        [Test]
        public void test_system_records_sink_for_invalid_current_generation_boundary_event()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            var sink = new CapturingDiagnosticSink();
            SuspicionGradeSystem system = CreateSystemWithSink(
                out bus, out boundary, out presenter, sink);
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => publicationCount++);
            var invalidBoundary = new RoomCompletionBoundary(
                "session", 1, "", false, true, true, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float> { { "guard-1", 1f } });
            var boundaryEvent = new RoomCompletionBoundaryEvent(
                invalidBoundary, 13f);

            // Act
            Assert.That(bus.Publish(boundaryEvent).IsAccepted, Is.True);
            Assert.DoesNotThrow(() =>
                bus.Drain(EventBusPhase.Presentation));
            int followUpDeliveries = bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(followUpDeliveries, Is.EqualTo(0));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-invalid-boundary"));
            Assert.That(sink.HasCode("suspicion-grade-invalid-boundary"),
                Is.True);
            Assert.That(sink.LastEnvelope, Is.Not.Null);
            Assert.That(sink.LastEnvelope.Identity,
                Is.EqualTo("room-boundary:session|1|"));
            Assert.That(sink.LastEnvelope.SessionId, Is.EqualTo("session"));
            Assert.That(sink.LastEnvelope.AttemptEpoch, Is.EqualTo(1));
            Assert.That(sink.LastEnvelope.Timestamp, Is.EqualTo(13f));
            Assert.That(sink.LastEnvelope.Publisher,
                Is.EqualTo("SessionBoundary"));
            Assert.That(sink.LastEnvelope.Phase,
                Is.EqualTo(EventBusPhase.Presentation));
            Assert.That(system.FinalizedCount, Is.EqualTo(0));
            Assert.That(publicationCount, Is.EqualTo(0));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_records_rejected_final_result_publication_and_retries_later()
        {
            // Arrange
            var bus = new SessionEventBus(1, 0);
            bus.BeginSession("session", 1);
            var boundary = new SessionBoundaryService(bus);
            var sink = new CapturingDiagnosticSink();
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration(),
                null, sink);
            float observedTimestamp = -1f;
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt =>
                {
                    observedTimestamp = evt.Timestamp;
                    publicationCount++;
                });
            Assert.That(bus.Publish(new GradeTraceEvent(
                ChaseTrace("session", 1, "rejection-timestamp-entry",
                    "rejection-timestamp-trace"), 42.5f)).IsAccepted,
                Is.True);
            bus.Drain(EventBusPhase.Presentation);
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 20f, 0f, 100f, 20f, "Investigate"),
                "queue-filler", 50f)).IsAccepted, Is.True);
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);

            // Act
            RoomGradeFinalized first = system.FinalizeRoom(room);
            string rejectionDiagnostic = system.LastDiagnostic;
            bus.Drain(EventBusPhase.Presentation);
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                Snapshot("session", 1, 30f, 0f, 100f, 20f, "Investigate"),
                "intervening-rejection-snapshot", 99f)).IsAccepted,
                Is.True);
            bus.Drain(EventBusPhase.Presentation);
            RoomGradeFinalized second = system.FinalizeRoom(room);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(first, Is.SameAs(second));
            Assert.That(rejectionDiagnostic, Is.EqualTo(
                "suspicion-grade-final-result-publication-rejected"));
            Assert.That(sink.HasCode(
                "suspicion-grade-final-result-publication-rejected"), Is.True);
            Assert.That(sink.LastEnvelope, Is.Not.Null);
            Assert.That(sink.LastEnvelope.Identity,
                Is.EqualTo("room-grade:session|1|room"));
            Assert.That(observedTimestamp, Is.EqualTo(42.5f));
            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-final-result-publication-recovered"));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_records_sink_for_stale_trace_event()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            var sink = new CapturingDiagnosticSink();
            SuspicionGradeSystem system = CreateSystemWithSink(
                out bus, out boundary, out presenter, sink);
            system.ResetForBoundary("session", 2);
            var traceEvent = new GradeTraceEvent(
                ChaseTrace("session", 1, "stale-entry", "stale-event"), 7f);

            // Act
            Assert.That(bus.Publish(traceEvent).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-stale-trace"));
            Assert.That(sink.HasCode("suspicion-grade-stale-trace"), Is.True);
            Assert.That(sink.LastEnvelope, Is.Not.Null);
            Assert.That(sink.LastEnvelope.Identity, Is.EqualTo("stale-event"));
            Assert.That(sink.LastEnvelope.SessionId, Is.EqualTo("session"));
            Assert.That(sink.LastEnvelope.AttemptEpoch, Is.EqualTo(1));
            Assert.That(sink.LastEnvelope.Timestamp, Is.EqualTo(7f));
            Assert.That(sink.LastEnvelope.Publisher, Is.EqualTo("GuardFSM"));
            Assert.That(sink.LastEnvelope.Phase,
                Is.EqualTo(EventBusPhase.Presentation));
            system.Dispose();
        }

        [Test]
        public void test_system_finalized_event_uses_boundary_authoritative_timestamp()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            float observedTimestamp = -1f;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => observedTimestamp = evt.Timestamp);
            var boundaryEvent = new RoomCompletionBoundaryEvent(
                CompletedBoundary("session", 1, "room", 0f), 42.5f);

            // Act
            Assert.That(bus.Publish(boundaryEvent).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(observedTimestamp, Is.EqualTo(42.5f));
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        [Test]
        public void test_system_ignores_known_non_incident_decision_without_sink_record()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            var sink = new CapturingDiagnosticSink();
            SuspicionGradeSystem system = CreateSystemWithSink(
                out bus, out boundary, out presenter, sink);
            var decision = new InvestigateCommit
            {
                EntryId = "entry-commit",
                Identity = "decision-commit",
                SessionId = "session",
                AttemptEpoch = 1,
                Timestamp = 3f
            };

            // Act
            Assert.That(bus.Publish(decision).IsAccepted, Is.True);
            var chaseEnd = new ChaseEnd
            {
                EntryId = "entry-chase-end",
                Identity = "decision-chase-end",
                SessionId = "session",
                AttemptEpoch = 1,
                Timestamp = 4f
            };
            Assert.That(bus.Publish(chaseEnd).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);
            string nonIncidentDiagnostic = system.LastDiagnostic;
            RoomGradeFinalized result = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));

            // Assert
            Assert.That(nonIncidentDiagnostic,
                Is.EqualTo("suspicion-grade-decision-non-incident-ignored"));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-finalized"));
            Assert.That(result.ContributingEventIds, Has.Count.EqualTo(0));
            Assert.That(sink.Records, Has.Count.EqualTo(0));
            system.Dispose();
        }

        [Test]
        public void test_system_distinguishes_invalid_snapshot_identity_from_stale_epoch()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot invalidSnapshot = Snapshot(
                "", 1, 20f, 0f, 100f, 20f, "Investigate");
            GuardSuspicionSnapshot staleSnapshot = Snapshot(
                "session", 0, 20f, 0f, 100f, 20f, "Investigate");

            // Act
            system.AcceptSnapshot(invalidSnapshot);
            string invalidDiagnostic = system.LastDiagnostic;
            system.AcceptSnapshot(staleSnapshot);

            // Assert
            Assert.That(invalidDiagnostic,
                Is.EqualTo("suspicion-grade-invalid-snapshot"));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-stale-snapshot"));
            system.Dispose();
        }

        [Test]
        public void test_system_rebind_resynchronizes_after_missed_epoch_boundary()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot oldSnapshot = Snapshot(
                "session", 1, 35f, 0f, 100f, 20f, "Investigate");
            system.AcceptSnapshot(oldSnapshot);
            system.AcceptTrace(ChaseTrace(
                "session", 1, "old-entry", "old-trace"));
            RoomGradeFinalized oldResult = system.FinalizeRoom(
                CompletedBoundary("session", 1, "room", 0f));
            Assert.That(system.FinalizedCount, Is.EqualTo(1));
            system.Unbind();
            boundary.BeginEpoch(2);

            // Act
            system.Bind();
            SuspicionMeterReadModel oldReadModel;
            bool retainedOldModel = system.TryGetReadModel(
                oldSnapshot.Identity, out oldReadModel);
            RoomGradeFinalized newResult = system.FinalizeRoom(
                CompletedBoundary("session", 2, "room", 0f));
            GuardSuspicionSnapshot snapshot = new GuardSuspicionSnapshot(
                new SuspicionGradeIdentity("session", 2, "room", "guard-1"),
                true, 35f, true, 0f, true, 100f, 20f,
                "Investigate", "rebound", "entry-2");
            Assert.That(bus.Publish(new SuspicionSnapshotEvent(
                snapshot, "rebound-snapshot", 8f)).IsAccepted, Is.True);
            bus.Drain(EventBusPhase.Presentation);

            // Assert
            Assert.That(system.ActiveSessionId, Is.EqualTo("session"));
            Assert.That(system.ActiveAttemptEpoch, Is.EqualTo(2));
            Assert.That(retainedOldModel, Is.False);
            Assert.That(system.FinalizedCount, Is.EqualTo(1));
            Assert.That(newResult, Is.Not.SameAs(oldResult));
            Assert.That(newResult.ContributingEventIds,
                Does.Not.Contain("old-trace"));
            SuspicionMeterReadModel readModel;
            Assert.That(system.TryGetReadModel(snapshot.Identity, out readModel),
                Is.True);
            system.Dispose();
        }

        [Test]
        public void test_system_invalid_reset_preserves_projection_trace_and_finalization()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 25f, 0f, 100f, 20f, "Investigate");
            system.AcceptSnapshot(snapshot);
            system.AcceptTrace(ChaseTrace(
                "session", 1, "entry-1", "event-1"));
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);
            RoomGradeFinalized first = system.FinalizeRoom(room);

            // Act
            system.ResetForBoundary(null, 2);
            string nullResetDiagnostic = system.LastDiagnostic;
            bool modelAfterNullReset = system.TryGetReadModel(
                snapshot.Identity, out _);
            RoomGradeFinalized afterNullReset = system.FinalizeRoom(room);
            system.ResetForBoundary("session", -1);
            string negativeResetDiagnostic = system.LastDiagnostic;
            bool modelAfterNegativeReset = system.TryGetReadModel(
                snapshot.Identity, out _);
            RoomGradeFinalized afterNegativeReset = system.FinalizeRoom(room);

            // Assert
            Assert.That(nullResetDiagnostic,
                Is.EqualTo("suspicion-grade-invalid-boundary-reset"));
            Assert.That(negativeResetDiagnostic,
                Is.EqualTo("suspicion-grade-invalid-boundary-reset"));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-finalization-duplicate"));
            Assert.That(modelAfterNullReset, Is.True);
            Assert.That(modelAfterNegativeReset, Is.True);
            Assert.That(afterNullReset, Is.SameAs(first));
            Assert.That(afterNegativeReset, Is.SameAs(first));
            Assert.That(afterNegativeReset.ContributingEventIds,
                Is.EqualTo(new[] { "event-1" }));
            system.Dispose();
        }

        [Test]
        public void test_system_reports_snapshot_tchase_divergence_without_changing_projection()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            SuspicionGradeSystem system = CreateSystem(
                out bus, out boundary, out presenter);
            GuardSuspicionSnapshot snapshot = Snapshot(
                "session", 1, 40f, 0f, 80f, 20f, "Investigate");

            // Act
            system.AcceptSnapshot(snapshot);

            // Assert
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-tchase-divergence"));
            SuspicionMeterReadModel readModel;
            Assert.That(system.TryGetReadModel(snapshot.Identity, out readModel),
                Is.True);
            Assert.That(readModel.TChase, Is.EqualTo(80f));
            Assert.That(readModel.MeterPercent, Is.EqualTo(50));
            system.Dispose();
        }

        [Test]
        public void test_system_reports_same_epoch_duplicate_suppression_after_reset()
        {
            // Arrange
            SessionEventBus bus;
            SessionBoundaryService boundary;
            FakeSuspicionMeterPresenter presenter;
            var sink = new CapturingDiagnosticSink();
            SuspicionGradeSystem system = CreateSystemWithSink(
                out bus, out boundary, out presenter, sink);
            RoomCompletionBoundary room = CompletedBoundary(
                "session", 1, "room", 0f);
            int publicationCount = 0;
            SubscriptionToken resultToken = bus.Subscribe<RoomGradeFinalizedEvent>(
                evt => publicationCount++);
            RoomGradeFinalized first = system.FinalizeRoom(room);
            bus.Drain(EventBusPhase.Presentation);
            system.ResetForBoundary("session", 1);

            // Act
            RoomGradeFinalized second = system.FinalizeRoom(room);
            int sinkCountAfterSuppression = sink.Records.Count;
            RoomGradeFinalized third = system.FinalizeRoom(room);

            // Assert
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(third, Is.SameAs(second));
            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(system.LastDiagnostic,
                Is.EqualTo("suspicion-grade-finalization-duplicate"));
            Assert.That(sink.Records.Count, Is.EqualTo(sinkCountAfterSuppression));
            Assert.That(sink.HasCode(
                "suspicion-grade-final-result-publication-duplicate-suppressed"),
                Is.True);
            bus.Unsubscribe(resultToken);
            system.Dispose();
        }

        private static SuspicionGradeSystem CreateSystem(
            out SessionEventBus bus,
            out SessionBoundaryService boundary,
            out FakeSuspicionMeterPresenter presenter)
        {
            bus = new SessionEventBus();
            bus.BeginSession("session", 1);
            boundary = new SessionBoundaryService(bus);
            presenter = new FakeSuspicionMeterPresenter();
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration(),
                presenter, null);
            system.BindDecisionContext("room", "guard-1");
            return system;
        }

        private static SuspicionGradeSystem CreateSystemWithSink(
            out SessionEventBus bus,
            out SessionBoundaryService boundary,
            out FakeSuspicionMeterPresenter presenter,
            CapturingDiagnosticSink sink)
        {
            bus = new SessionEventBus();
            bus.BeginSession("session", 1);
            boundary = new SessionBoundaryService(bus);
            presenter = new FakeSuspicionMeterPresenter();
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration(),
                presenter, sink);
            system.BindDecisionContext("room", "guard-1");
            return system;
        }

        private static SuspicionGradeSystem CreateThrowingPresenterSystem(
            out SessionEventBus bus,
            out SessionBoundaryService boundary,
            out ThrowingSuspicionMeterPresenter presenter)
        {
            bus = new SessionEventBus();
            bus.BeginSession("session", 1);
            boundary = new SessionBoundaryService(bus);
            presenter = new ThrowingSuspicionMeterPresenter();
            var system = new SuspicionGradeSystem(bus, boundary,
                SuspicionGradeTestFixtures.CreateStarterConfiguration(),
                presenter, null);
            system.BindDecisionContext("room", "guard-1");
            return system;
        }

        private static GuardSuspicionSnapshot Snapshot(string sessionId,
            long epoch, float accumulator, float residual, float chaseThreshold,
            float entryThreshold, string state)
        {
            return new GuardSuspicionSnapshot(
                new SuspicionGradeIdentity(sessionId, epoch, "room", "guard-1"),
                true, accumulator, true, residual, true, chaseThreshold,
                entryThreshold, state, "snapshot-cause", "entry-1");
        }

        private static GradeTraceRecord ChaseTrace(string sessionId, long epoch,
            string entryId, string eventId)
        {
            return new GradeTraceRecord(sessionId, epoch, "room", "guard-1",
                entryId, GradeIncidentType.ChaseEntry, null, eventId);
        }

        private static RoomCompletionBoundary CompletedBoundary(
            string sessionId, long epoch, string roomId, float residual)
        {
            return new RoomCompletionBoundary(sessionId, epoch, roomId,
                false, true, true, true,
                new Dictionary<string, float> { { "guard-1", residual } },
                new Dictionary<string, float> { { "guard-1", 1f } });
        }

        private sealed class FakeSuspicionMeterPresenter
            : ISuspicionMeterPresenter
        {
            public SuspicionMeterReadModel LastReadModel { get; private set; }

            public void Present(SuspicionMeterReadModel readModel)
            {
                LastReadModel = readModel;
            }
        }

        private sealed class ThrowingSuspicionMeterPresenter
            : ISuspicionMeterPresenter
        {
            public void Present(SuspicionMeterReadModel readModel)
            {
                throw new InvalidOperationException("presenter-test-failure");
            }
        }

        private sealed class CapturingDiagnosticSink : IEventDiagnosticSink
        {
            public readonly List<string> Records = new List<string>();
            public EventEnvelope LastEnvelope { get; private set; }

            public void Record(string code, EventEnvelope envelope,
                int retryCount, int queueDepth, int queueCapacity)
            {
                Records.Add(code);
                LastEnvelope = envelope;
            }

            public bool HasCode(string code)
            {
                return Records.Contains(code);
            }
        }
    }
}
