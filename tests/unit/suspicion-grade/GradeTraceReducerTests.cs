using System;
using System.Collections.Generic;
using NUnit.Framework;
using WhisperWard.AI.Perception;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies immutable trace adaptation and deterministic Suspicion Grade reduction.
    /// </summary>
    public sealed class GradeTraceReducerTests
    {
        [Test]
        public void Accept_counts_one_chase_entry_once_by_entry_id()
        {
            var reducer = new GradeTraceReducer();
            var first = ChaseEntry("session", 1, "room", "guard-1", "entry-7", "event-1");
            var retry = ChaseEntry("session", 1, "room", "guard-1", "entry-7", "event-1-retry");

            Assert.That(reducer.Accept(first), Is.True);
            Assert.That(reducer.Accept(retry), Is.True);

            var aggregate = reducer.Aggregate("session", 1, "room");
            Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(1));
            Assert.That(aggregate.ContributingEventIds, Is.EqualTo(new[] { "event-1" }));
        }

        [Test]
        public void Accept_counts_only_fruitless_investigation_resolution()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(Resolution("session", 1, "room", "guard-1",
                "resolve-noise", InvestigationResolutionCause.Noise));
            reducer.Accept(Resolution("session", 1, "room", "guard-1",
                "resolve-fruitless", InvestigationResolutionCause.Fruitless));

            var aggregate = reducer.Aggregate("session", 1, "room");
            Assert.That(aggregate.FruitlessResolutionCount, Is.EqualTo(1));
            Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(0));
            Assert.That(aggregate.CaptureCount, Is.EqualTo(0));
        }

        [Test]
        public void Accept_ignores_noise_relay_clean_los_and_near_miss_records()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(NonIncident("session", 1, "room", "guard-1", "noise-1"));
            reducer.Accept(NonIncident("session", 1, "room", "guard-1", "relay-1"));
            reducer.Accept(NonIncident("session", 1, "room", "guard-1", "los-break-1"));

            var aggregate = reducer.Aggregate("session", 1, "room");
            Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(0));
            Assert.That(aggregate.FruitlessResolutionCount, Is.EqualTo(0));
            Assert.That(aggregate.CaptureCount, Is.EqualTo(0));
        }

        [Test]
        public void Accept_ignores_stale_epoch_without_erasing_current_aggregate()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 2, "room", "guard-1",
                "current", "current-event"));
            reducer.Accept(ChaseEntry("session", 1, "room", "guard-1",
                "stale", "stale-event"));

            var aggregate = reducer.Aggregate("session", 2, "room");
            Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(1));
            Assert.That(aggregate.ContributingEventIds, Does.Not.Contain("stale-event"));
        }

        [Test]
        public void Accept_rejects_malformed_record_without_mutating_valid_state()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 1, "room", "guard-1",
                "valid-entry", "valid-event"));

            var malformed = new GradeTraceRecord(
                "", 1, "room", "guard-1", "bad-entry",
                GradeIncidentType.ChaseEntry, null, "bad-event");

            Assert.That(reducer.Accept(malformed), Is.False);
            Assert.That(reducer.LastDiagnostic, Is.EqualTo("grade-trace-invalid-session-id"));

            var aggregate = reducer.Aggregate("session", 1, "room");
            Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(1));
            Assert.That(aggregate.ContributingEventIds, Is.EqualTo(new[] { "valid-event" }));
        }

        [Test]
        public void Accept_rejects_negative_epoch_without_throwing_or_mutating_state()
        {
            var reducer = new GradeTraceReducer();
            Assert.That(reducer.Accept(new GradeTraceRecord(
                "session", -1, "room", "guard-1", "entry", GradeIncidentType.Capture,
                null, "event")), Is.False);
            Assert.That(reducer.LastDiagnostic, Is.EqualTo("grade-trace-invalid-attempt-epoch"));
            Assert.That(reducer.Aggregate("session", 1, "room").CaptureCount, Is.EqualTo(0));
        }

        [Test]
        public void Aggregate_is_order_invariant()
        {
            var records = MixedTrace("session", 1, "room");
            var forward = new GradeTraceReducer();
            var reverse = new GradeTraceReducer();

            foreach (var record in records) forward.Accept(record);
            for (var i = records.Count - 1; i >= 0; i--) reverse.Accept(records[i]);

            Assert.That(forward.Aggregate("session", 1, "room"), Is.EqualTo(
                reverse.Aggregate("session", 1, "room")));
        }

        [Test]
        public void Aggregate_counts_captures_and_groups_incidents_by_guard()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 1, "room", "guard-b",
                "entry-b", "event-b"));
            reducer.Accept(Resolution("session", 1, "room", "guard-a",
                "event-a", InvestigationResolutionCause.Fruitless));
            reducer.Accept(Capture("session", 1, "room", "guard-a",
                "entry-capture", "event-c"));

            var aggregate = reducer.Aggregate("session", 1, "room");
            Assert.That(aggregate.CaptureCount, Is.EqualTo(1));
            Assert.That(aggregate.PerGuard.Count, Is.EqualTo(2));
            Assert.That(aggregate.PerGuard[0].GuardEid, Is.EqualTo("guard-a"));
            Assert.That(aggregate.PerGuard[0].FruitlessResolutionCount, Is.EqualTo(1));
            Assert.That(aggregate.PerGuard[0].CaptureCount, Is.EqualTo(1));
            Assert.That(aggregate.PerGuard[1].GuardEid, Is.EqualTo("guard-b"));
            Assert.That(aggregate.PerGuard[1].ChaseEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Aggregate_sorts_contributing_event_ids_ordinally()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 1, "room", "guard-1",
                "entry-z", "event-z"));
            reducer.Accept(Resolution("session", 1, "room", "guard-1",
                "event-a", InvestigationResolutionCause.Fruitless));
            reducer.Accept(Capture("session", 1, "room", "guard-1",
                "entry-capture", "event-m"));

            Assert.That(reducer.Aggregate("session", 1, "room").ContributingEventIds,
                Is.EqualTo(new[] { "event-a", "event-m", "event-z" }));
        }

        [Test]
        public void Adapt_copies_authoritative_decision_into_immutable_trace_record()
        {
            var decision = new ChaseEntry
            {
                EntryId = "entry-7",
                Identity = "event-7",
                SessionId = "session",
                AttemptEpoch = 4,
                Timestamp = 12f
            };

            Assert.That(GradeTraceRecord.TryAdaptDecisionRecord(
                decision, "room", "guard-1", out var adapted), Is.True);
            Assert.That(adapted.EventId, Is.EqualTo("event-7"));
            Assert.That(adapted.EntryId, Is.EqualTo("entry-7"));
            Assert.That(adapted.SessionId, Is.EqualTo("session"));
            Assert.That(adapted.AttemptEpoch, Is.EqualTo(4));
            Assert.That(adapted.RoomId, Is.EqualTo("room"));
            Assert.That(adapted.GuardEid, Is.EqualTo("guard-1"));
            Assert.That(adapted.IncidentType, Is.EqualTo(GradeIncidentType.ChaseEntry));

            decision.EntryId = "mutated-after-adaptation";
            decision.Identity = "mutated-event";
            Assert.That(adapted.EntryId, Is.EqualTo("entry-7"));
            Assert.That(adapted.EventId, Is.EqualTo("event-7"));
        }

        [Test]
        public void Adapt_non_fruitless_resolution_as_non_incident()
        {
            var decision = new InvestigateResolution
            {
                EntryId = "entry-1",
                Identity = "resolution-noise",
                SessionId = "session",
                AttemptEpoch = 1,
                Cause = "noise"
            };

            Assert.That(GradeTraceRecord.TryAdaptDecisionRecord(
                decision, "room", "guard-1", out var adapted), Is.True);
            Assert.That(adapted.IncidentType, Is.Null);
            Assert.That(adapted.ResolutionCause,
                Is.EqualTo(InvestigationResolutionCause.Noise));
        }

        [Test]
        public void Adapt_and_accept_fruitless_resolution_and_capture_copies_all_identity()
        {
            var resolution = new InvestigateResolution
            {
                EntryId = "resolution-entry",
                Identity = "fruitless-event",
                SessionId = "session",
                AttemptEpoch = 6,
                Cause = "fruitless"
            };
            var capture = new Capture
            {
                EntryId = "capture-entry",
                Identity = "capture-event",
                SessionId = "session",
                AttemptEpoch = 6
            };

            Assert.That(GradeTraceRecord.TryAdaptDecisionRecord(
                resolution, "room", "guard-1", out var adaptedResolution), Is.True);
            Assert.That(adaptedResolution.IncidentType,
                Is.EqualTo(GradeIncidentType.FruitlessResolution));
            Assert.That(adaptedResolution.ResolutionCause,
                Is.EqualTo(InvestigationResolutionCause.Fruitless));
            Assert.That(adaptedResolution.SessionId, Is.EqualTo("session"));
            Assert.That(adaptedResolution.AttemptEpoch, Is.EqualTo(6));
            Assert.That(adaptedResolution.RoomId, Is.EqualTo("room"));
            Assert.That(adaptedResolution.GuardEid, Is.EqualTo("guard-1"));
            Assert.That(adaptedResolution.EntryId, Is.EqualTo("resolution-entry"));
            Assert.That(adaptedResolution.EventId, Is.EqualTo("fruitless-event"));

            Assert.That(GradeTraceRecord.TryAdaptDecisionRecord(
                capture, "room", "guard-1", out var adaptedCapture), Is.True);
            Assert.That(adaptedCapture.IncidentType,
                Is.EqualTo(GradeIncidentType.Capture));
            Assert.That(adaptedCapture.SessionId, Is.EqualTo("session"));
            Assert.That(adaptedCapture.AttemptEpoch, Is.EqualTo(6));
            Assert.That(adaptedCapture.RoomId, Is.EqualTo("room"));
            Assert.That(adaptedCapture.GuardEid, Is.EqualTo("guard-1"));
            Assert.That(adaptedCapture.EntryId, Is.EqualTo("capture-entry"));
            Assert.That(adaptedCapture.EventId, Is.EqualTo("capture-event"));

            var reducer = new GradeTraceReducer();
            Assert.That(reducer.Accept(adaptedResolution), Is.True);
            Assert.That(reducer.Accept(adaptedCapture), Is.True);
            var aggregate = reducer.Aggregate("session", 6, "room");
            Assert.That(aggregate.FruitlessResolutionCount, Is.EqualTo(1));
            Assert.That(aggregate.CaptureCount, Is.EqualTo(1));
        }

        [Test]
        public void Reset_clears_only_the_target_session_epoch_room_scope()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 1, "target-room", "guard-1",
                "target-entry", "target-event"));
            reducer.Accept(ChaseEntry("session", 1, "other-room", "guard-1",
                "other-room-entry", "other-room-event"));
            reducer.Accept(ChaseEntry("session", 2, "target-room", "guard-1",
                "other-epoch-entry", "other-epoch-event"));
            reducer.Accept(ChaseEntry("other-session", 1, "target-room", "guard-1",
                "other-session-entry", "other-session-event"));

            reducer.Reset("session", 1, "target-room");

            Assert.That(reducer.Aggregate("session", 1, "target-room").ChaseEntryCount,
                Is.EqualTo(0));
            Assert.That(reducer.Aggregate("session", 1, "other-room").ChaseEntryCount,
                Is.EqualTo(1));
            Assert.That(reducer.Aggregate("session", 2, "target-room").ChaseEntryCount,
                Is.EqualTo(1));
            Assert.That(reducer.Aggregate("other-session", 1, "target-room").ChaseEntryCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Accept_rejects_remaining_malformed_inputs_without_mutating_state()
        {
            var reducer = new GradeTraceReducer();
            reducer.Accept(ChaseEntry("session", 1, "room", "guard-1",
                "valid-entry", "valid-event"));
            var malformedRecords = new GradeTraceRecord[]
            {
                null,
                new GradeTraceRecord("session", 1, "", "guard-1", "entry",
                    GradeIncidentType.Capture, null, "event"),
                new GradeTraceRecord("session", 1, "room", "", "entry",
                    GradeIncidentType.Capture, null, "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "",
                    GradeIncidentType.Capture, null, "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    GradeIncidentType.Capture, null, ""),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    (GradeIncidentType)99, null, "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    null, (InvestigationResolutionCause)99, "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    GradeIncidentType.FruitlessResolution, null, "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    GradeIncidentType.Capture, InvestigationResolutionCause.Noise,
                    "event"),
                new GradeTraceRecord("session", 1, "room", "guard-1", "entry",
                    null, InvestigationResolutionCause.Fruitless, "event")
            };
            var expectedDiagnostics = new[]
            {
                "grade-trace-null-record",
                "grade-trace-invalid-room-id",
                "grade-trace-invalid-guard-id",
                "grade-trace-invalid-entry-id",
                "grade-trace-invalid-event-id",
                "grade-trace-invalid-incident-type",
                "grade-trace-invalid-resolution-cause",
                "grade-trace-fruitless-cause-mismatch",
                "grade-trace-incident-cause-mismatch",
                "grade-trace-fruitless-cause-mismatch"
            };

            for (int i = 0; i < malformedRecords.Length; i++)
            {
                Assert.That(reducer.Accept(malformedRecords[i]), Is.False);
                Assert.That(reducer.LastDiagnostic,
                    Is.EqualTo(expectedDiagnostics[i]));
                Assert.That(reducer.Aggregate("session", 1, "room").ChaseEntryCount,
                    Is.EqualTo(1));
            }
        }

        private static GradeTraceRecord ChaseEntry(string sessionId, long epoch,
            string roomId, string guardEid, string entryId, string eventId)
        {
            return new GradeTraceRecord(sessionId, epoch, roomId, guardEid, entryId,
                GradeIncidentType.ChaseEntry, null, eventId);
        }

        private static GradeTraceRecord Resolution(string sessionId, long epoch,
            string roomId, string guardEid, string eventId,
            InvestigationResolutionCause cause)
        {
            var type = cause == InvestigationResolutionCause.Fruitless
                ? GradeIncidentType.FruitlessResolution : (GradeIncidentType?)null;
            return new GradeTraceRecord(sessionId, epoch, roomId, guardEid,
                "resolution-" + eventId, type, cause, eventId);
        }

        private static GradeTraceRecord Capture(string sessionId, long epoch,
            string roomId, string guardEid, string entryId, string eventId)
        {
            return new GradeTraceRecord(sessionId, epoch, roomId, guardEid, entryId,
                GradeIncidentType.Capture, null, eventId);
        }

        private static GradeTraceRecord NonIncident(string sessionId, long epoch,
            string roomId, string guardEid, string eventId)
        {
            return new GradeTraceRecord(sessionId, epoch, roomId, guardEid,
                "non-incident-" + eventId, null, null, eventId);
        }

        private static List<GradeTraceRecord> MixedTrace(string sessionId, long epoch,
            string roomId)
        {
            return new List<GradeTraceRecord>
            {
                ChaseEntry(sessionId, epoch, roomId, "guard-1", "entry-z", "event-z"),
                Resolution(sessionId, epoch, roomId, "guard-1", "event-a",
                    InvestigationResolutionCause.Fruitless),
                Capture(sessionId, epoch, roomId, "guard-2", "entry-c", "event-c"),
                NonIncident(sessionId, epoch, roomId, "guard-2", "relay")
            };
        }
    }
}
