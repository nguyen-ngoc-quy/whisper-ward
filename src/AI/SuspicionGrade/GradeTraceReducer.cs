using System;
using System.Collections.Generic;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Reduces immutable grade trace records into deterministic room aggregates.
    /// The reducer never polls or recalculates Perception or FSM state.
    /// </summary>
    public sealed class GradeTraceReducer
    {
        private sealed class GuardCounts
        {
            public GuardCounts(string guardEid)
            {
                GuardEid = guardEid;
            }

            public readonly string GuardEid;
            public int ChaseEntryCount;
            public int FruitlessResolutionCount;
            public int CaptureCount;
        }

        private sealed class ScopedIncidentData
        {
            public readonly Dictionary<string, GuardCounts> GuardCounts =
                new Dictionary<string, GuardCounts>(StringComparer.Ordinal);
            public readonly List<string> EventIds = new List<string>();
            public int ChaseEntryCount;
            public int FruitlessResolutionCount;
            public int CaptureCount;
        }

        private readonly struct IncidentKey : IEquatable<IncidentKey>
        {
            public IncidentKey(string sessionId, long attemptEpoch,
                string entryId, GradeIncidentType incidentType)
            {
                SessionId = sessionId;
                AttemptEpoch = attemptEpoch;
                EntryId = entryId;
                IncidentType = incidentType;
            }

            private readonly string SessionId;
            private readonly long AttemptEpoch;
            private readonly string EntryId;
            private readonly GradeIncidentType IncidentType;

            public bool Equals(IncidentKey other)
            {
                return AttemptEpoch == other.AttemptEpoch
                    && IncidentType == other.IncidentType
                    && string.Equals(SessionId, other.SessionId,
                        StringComparison.Ordinal)
                    && string.Equals(EntryId, other.EntryId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is IncidentKey && Equals((IncidentKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(SessionId);
                    hash = hash * 31 + AttemptEpoch.GetHashCode();
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(EntryId);
                    return hash * 31 + IncidentType.GetHashCode();
                }
            }
        }

        private readonly HashSet<IncidentKey> _incidentKeys =
            new HashSet<IncidentKey>();
        private readonly Dictionary<IncidentKey, GradeTraceRecord> _firstIncidentRecords =
            new Dictionary<IncidentKey, GradeTraceRecord>();

        /// <summary>
        /// Gets the stable diagnostic for the most recent acceptance or reset call.
        /// Read operations do not alter this value.
        /// </summary>
        public string LastDiagnostic { get; private set; } = "grade-trace-none";

        /// <summary>
        /// Validates and accepts one immutable trace record. Invalid records and
        /// duplicate incidents are harmless and never remove accepted state.
        /// </summary>
        /// <param name="record">Immutable record at the trace adapter boundary.</param>
        /// <returns>True for a valid record, including a duplicate or non-incident.</returns>
        public bool Accept(GradeTraceRecord record)
        {
            string diagnostic;
            if (!TryValidate(record, out diagnostic))
            {
                LastDiagnostic = diagnostic;
                return false;
            }

            if (!record.IncidentType.HasValue)
            {
                LastDiagnostic = "grade-trace-non-incident-ignored";
                return true;
            }

            IncidentKey key = CreateKey(record);
            if (!_incidentKeys.Add(key))
            {
                LastDiagnostic = "grade-trace-duplicate-incident";
                return true;
            }

            _firstIncidentRecords.Add(key, record);
            LastDiagnostic = "grade-trace-accepted";
            return true;
        }

        /// <summary>
        /// Builds an aggregate for exactly one session, attempt epoch, and room.
        /// Records from other generations or rooms remain harmless and are excluded.
        /// </summary>
        /// <param name="sessionId">Queried session identity.</param>
        /// <param name="attemptEpoch">Queried attempt epoch.</param>
        /// <param name="roomId">Queried room identity.</param>
        /// <returns>A deterministic immutable room aggregate.</returns>
        public GradeTraceAggregate Aggregate(string sessionId, long attemptEpoch,
            string roomId)
        {
            if (!IsValidScope(sessionId, attemptEpoch, roomId))
                return CreateEmptyAggregate(sessionId, attemptEpoch, roomId);

            ScopedIncidentData scoped = CollectScopedIncidents(
                sessionId, attemptEpoch, roomId);
            List<GuardGradeBreakdown> perGuard = BuildPerGuard(scoped.GuardCounts);
            scoped.EventIds.Sort(StringComparer.Ordinal);
            return CreateAggregate(sessionId, attemptEpoch, roomId, scoped,
                perGuard);
        }

        /// <summary>
        /// Removes only accepted incidents belonging to the supplied room boundary.
        /// Invalid reset scopes are ignored without throwing.
        /// </summary>
        /// <param name="sessionId">Session identity to reset.</param>
        /// <param name="attemptEpoch">Attempt epoch to reset.</param>
        /// <param name="roomId">Room identity to reset.</param>
        public void Reset(string sessionId, long attemptEpoch, string roomId)
        {
            if (!IsValidScope(sessionId, attemptEpoch, roomId))
            {
                LastDiagnostic = "grade-trace-invalid-reset-scope";
                return;
            }

            var keysToRemove = new List<IncidentKey>();
            foreach (KeyValuePair<IncidentKey, GradeTraceRecord> pair
                in _firstIncidentRecords)
            {
                if (MatchesScope(pair.Value, sessionId, attemptEpoch, roomId))
                    keysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _incidentKeys.Remove(keysToRemove[i]);
                _firstIncidentRecords.Remove(keysToRemove[i]);
            }

            LastDiagnostic = "grade-trace-reset";
        }

        private static IncidentKey CreateKey(GradeTraceRecord record)
        {
            return new IncidentKey(record.SessionId, record.AttemptEpoch,
                record.EntryId, record.IncidentType.Value);
        }

        private ScopedIncidentData CollectScopedIncidents(string sessionId,
            long attemptEpoch, string roomId)
        {
            var scoped = new ScopedIncidentData();
            foreach (GradeTraceRecord record in _firstIncidentRecords.Values)
            {
                if (MatchesScope(record, sessionId, attemptEpoch, roomId))
                    AddIncident(scoped, record);
            }

            return scoped;
        }

        private static void AddIncident(ScopedIncidentData scoped,
            GradeTraceRecord record)
        {
            scoped.EventIds.Add(record.EventId);
            GuardCounts counts;
            if (!scoped.GuardCounts.TryGetValue(record.GuardEid, out counts))
            {
                counts = new GuardCounts(record.GuardEid);
                scoped.GuardCounts.Add(record.GuardEid, counts);
            }

            switch (record.IncidentType.Value)
            {
                case GradeIncidentType.ChaseEntry:
                    scoped.ChaseEntryCount++;
                    counts.ChaseEntryCount++;
                    break;
                case GradeIncidentType.FruitlessResolution:
                    scoped.FruitlessResolutionCount++;
                    counts.FruitlessResolutionCount++;
                    break;
                case GradeIncidentType.Capture:
                    scoped.CaptureCount++;
                    counts.CaptureCount++;
                    break;
            }
        }

        private static List<GuardGradeBreakdown> BuildPerGuard(
            Dictionary<string, GuardCounts> guardCounts)
        {
            var guardIds = new List<string>(guardCounts.Keys);
            guardIds.Sort(StringComparer.Ordinal);
            var perGuard = new List<GuardGradeBreakdown>(guardIds.Count);
            for (int i = 0; i < guardIds.Count; i++)
            {
                GuardCounts counts = guardCounts[guardIds[i]];
                perGuard.Add(new GuardGradeBreakdown(counts.GuardEid,
                    counts.ChaseEntryCount, counts.FruitlessResolutionCount,
                    counts.CaptureCount));
            }

            return perGuard;
        }

        private static GradeTraceAggregate CreateEmptyAggregate(string sessionId,
            long attemptEpoch, string roomId)
        {
            return new GradeTraceAggregate(sessionId, attemptEpoch, roomId, 0, 0,
                0, new List<GuardGradeBreakdown>(), new List<string>());
        }

        private static GradeTraceAggregate CreateAggregate(string sessionId,
            long attemptEpoch, string roomId, ScopedIncidentData scoped,
            IReadOnlyList<GuardGradeBreakdown> perGuard)
        {
            return new GradeTraceAggregate(sessionId, attemptEpoch, roomId,
                scoped.ChaseEntryCount, scoped.FruitlessResolutionCount,
                scoped.CaptureCount, perGuard, scoped.EventIds);
        }

        private static bool TryValidate(GradeTraceRecord record,
            out string diagnostic)
        {
            if (!TryValidateIdentity(record, out diagnostic)) return false;
            return TryValidateClassification(record, out diagnostic);
        }

        private static bool TryValidateIdentity(GradeTraceRecord record,
            out string diagnostic)
        {
            if (record == null)
                return Fail("grade-trace-null-record", out diagnostic);
            if (string.IsNullOrWhiteSpace(record.SessionId))
                return Fail("grade-trace-invalid-session-id", out diagnostic);
            if (record.AttemptEpoch < 0)
                return Fail("grade-trace-invalid-attempt-epoch", out diagnostic);
            if (string.IsNullOrWhiteSpace(record.RoomId))
                return Fail("grade-trace-invalid-room-id", out diagnostic);
            if (string.IsNullOrWhiteSpace(record.GuardEid))
                return Fail("grade-trace-invalid-guard-id", out diagnostic);
            if (string.IsNullOrWhiteSpace(record.EntryId))
                return Fail("grade-trace-invalid-entry-id", out diagnostic);
            if (string.IsNullOrWhiteSpace(record.EventId))
                return Fail("grade-trace-invalid-event-id", out diagnostic);

            diagnostic = "grade-trace-valid-identity";
            return true;
        }

        private static bool TryValidateClassification(GradeTraceRecord record,
            out string diagnostic)
        {
            if (!TryValidateEnums(record, out diagnostic)) return false;
            return TryValidateCauseConsistency(record, out diagnostic);
        }

        private static bool TryValidateEnums(GradeTraceRecord record,
            out string diagnostic)
        {
            if (record.IncidentType.HasValue
                && !Enum.IsDefined(typeof(GradeIncidentType),
                    record.IncidentType.Value))
                return Fail("grade-trace-invalid-incident-type", out diagnostic);
            if (record.ResolutionCause.HasValue
                && !Enum.IsDefined(typeof(InvestigationResolutionCause),
                    record.ResolutionCause.Value))
                return Fail("grade-trace-invalid-resolution-cause", out diagnostic);

            diagnostic = "grade-trace-valid-enums";
            return true;
        }

        private static bool TryValidateCauseConsistency(GradeTraceRecord record,
            out string diagnostic)
        {
            if (record.IncidentType == GradeIncidentType.FruitlessResolution
                && record.ResolutionCause != InvestigationResolutionCause.Fruitless)
                return Fail("grade-trace-fruitless-cause-mismatch", out diagnostic);
            if (record.IncidentType.HasValue
                && record.IncidentType != GradeIncidentType.FruitlessResolution
                && record.ResolutionCause.HasValue)
                return Fail("grade-trace-incident-cause-mismatch", out diagnostic);
            if (!record.IncidentType.HasValue
                && record.ResolutionCause == InvestigationResolutionCause.Fruitless)
                return Fail("grade-trace-fruitless-cause-mismatch", out diagnostic);

            diagnostic = "grade-trace-valid";
            return true;
        }

        private static bool Fail(string diagnostic, out string result)
        {
            result = diagnostic;
            return false;
        }

        private static bool IsValidScope(string sessionId, long attemptEpoch,
            string roomId)
        {
            return !string.IsNullOrWhiteSpace(sessionId)
                && attemptEpoch >= 0
                && !string.IsNullOrWhiteSpace(roomId);
        }

        private static bool MatchesScope(GradeTraceRecord record,
            string sessionId, long attemptEpoch, string roomId)
        {
            return record.AttemptEpoch == attemptEpoch
                && string.Equals(record.SessionId, sessionId,
                    StringComparison.Ordinal)
                && string.Equals(record.RoomId, roomId,
                    StringComparison.Ordinal);
        }
    }
}
