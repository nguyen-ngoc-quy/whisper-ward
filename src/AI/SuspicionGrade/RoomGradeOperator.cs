using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Completion state assigned by the deterministic room-grade boundary.
    /// </summary>
    public enum GradeCompletionStatus
    {
        /// <summary>The room ended through capture or another failed completion.</summary>
        Failed,

        /// <summary>Mandatory completion evidence was unavailable or invalid.</summary>
        Unresolved,

        /// <summary>The room completed with valid trace and residual evidence.</summary>
        Completed
    }

    /// <summary>
    /// Grade assigned to a successfully completed room.
    /// </summary>
    public enum RoomGrade
    {
        /// <summary>No grade is available for failed or unresolved completion.</summary>
        None,

        /// <summary>The highest completed-room grade.</summary>
        S,

        /// <summary>The second-highest completed-room grade.</summary>
        A,

        /// <summary>The third-highest completed-room grade.</summary>
        B,

        /// <summary>The completed-room score is below the B threshold.</summary>
        NeedsImprovement
    }

    /// <summary>
    /// Immutable session-owned evidence that a room reached a finalization boundary.
    /// </summary>
    public sealed class RoomCompletionBoundary
    {
        /// <summary>
        /// Creates a room completion boundary and defensively copies its maps.
        /// Malformed custom map implementations are retained as unavailable evidence.
        /// </summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch within the session.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="isCapture">Whether the boundary ended in Capture.</param>
        /// <param name="isCompletedRoom">Whether the room completed successfully.</param>
        /// <param name="hasMandatoryTrace">Whether mandatory trace evidence is present.</param>
        /// <param name="hasFinalResidualSnapshot">Whether final residual evidence is present.</param>
        /// <param name="finalResidualByGuard">Authoritative final residual by guard.</param>
        /// <param name="exposureWeightByGuard">Authoritative exposure weight by guard.</param>
        public RoomCompletionBoundary(
            string sessionId,
            long attemptEpoch,
            string roomId,
            bool isCapture,
            bool isCompletedRoom,
            bool hasMandatoryTrace,
            bool hasFinalResidualSnapshot,
            IReadOnlyDictionary<string, float> finalResidualByGuard,
            IReadOnlyDictionary<string, float> exposureWeightByGuard)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            RoomId = roomId ?? string.Empty;
            IsCapture = isCapture;
            IsCompletedRoom = isCompletedRoom;
            HasMandatoryTrace = hasMandatoryTrace;
            HasFinalResidualSnapshot = hasFinalResidualSnapshot;
            FinalResidualByGuard = CopyDictionary(finalResidualByGuard,
                out bool residualCopyValid);
            ExposureWeightByGuard = CopyDictionary(exposureWeightByGuard,
                out bool exposureCopyValid);
            HasValidMapCopies = residualCopyValid && exposureCopyValid;
        }

        /// <summary>Gets the session identity.</summary>
        public string SessionId { get; }

        /// <summary>Gets the attempt epoch.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Gets the room identity.</summary>
        public string RoomId { get; }

        /// <summary>Gets whether the boundary ended in Capture.</summary>
        public bool IsCapture { get; }

        /// <summary>Gets whether the room completed successfully.</summary>
        public bool IsCompletedRoom { get; }

        /// <summary>Gets whether mandatory trace evidence is present.</summary>
        public bool HasMandatoryTrace { get; }

        /// <summary>Gets whether final residual evidence is present.</summary>
        public bool HasFinalResidualSnapshot { get; }

        /// <summary>Gets the copied authoritative final residual by guard.</summary>
        public IReadOnlyDictionary<string, float> FinalResidualByGuard { get; }

        /// <summary>Gets the copied authoritative exposure weight by guard.</summary>
        public IReadOnlyDictionary<string, float> ExposureWeightByGuard { get; }

        internal bool HasValidMapCopies { get; }

        private static IReadOnlyDictionary<string, float> CopyDictionary(
            IReadOnlyDictionary<string, float> source,
            out bool copyValid)
        {
            var copy = new Dictionary<string, float>(StringComparer.Ordinal);
            copyValid = true;
            if (source == null)
                return new ReadOnlyDictionary<string, float>(copy);

            try
            {
                int expectedCount = source.Count;
                if (expectedCount < 0)
                {
                    copyValid = false;
                    return new ReadOnlyDictionary<string, float>(copy);
                }

                long enumeratedCount = 0L;
                foreach (KeyValuePair<string, float> pair in source)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)
                        || float.IsNaN(pair.Value)
                        || float.IsInfinity(pair.Value))
                    {
                        copyValid = false;
                        break;
                    }

                    copy.Add(pair.Key, pair.Value);
                    enumeratedCount++;
                    if (enumeratedCount > expectedCount)
                    {
                        copyValid = false;
                        break;
                    }
                }

                if (copyValid && enumeratedCount != expectedCount)
                    copyValid = false;
            }
            catch (Exception)
            {
                copyValid = false;
            }

            if (!copyValid)
                copy.Clear();
            return new ReadOnlyDictionary<string, float>(copy);
        }
    }

    /// <summary>
    /// Immutable final output of one deterministic room-grade operation.
    /// </summary>
    public sealed class RoomGradeFinalized
    {
        /// <summary>
        /// Creates a final room-grade result and defensively copies collection values.
        /// </summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch within the session.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="gradeVersion">Grade output schema version.</param>
        /// <param name="completionStatus">Completion status assigned by the operator.</param>
        /// <param name="perGuardBreakdown">Per-guard component breakdowns.</param>
        /// <param name="incidentPenalty">Room incident penalty diagnostic.</param>
        /// <param name="residualPenalty">Room residual penalty diagnostic.</param>
        /// <param name="qualityScore">Final room quality score.</param>
        /// <param name="grade">Final room grade.</param>
        /// <param name="contributingEventIds">Event IDs contributing to the result.</param>
        public RoomGradeFinalized(
            string sessionId,
            long attemptEpoch,
            string roomId,
            int gradeVersion,
            GradeCompletionStatus completionStatus,
            IReadOnlyList<GuardGradeBreakdown> perGuardBreakdown,
            float incidentPenalty,
            float residualPenalty,
            float qualityScore,
            RoomGrade grade,
            IReadOnlyList<string> contributingEventIds)
            : this(sessionId, attemptEpoch, roomId, gradeVersion,
                completionStatus, perGuardBreakdown, incidentPenalty,
                residualPenalty, true, qualityScore, grade,
                contributingEventIds)
        {
        }

        /// <summary>
        /// Creates a final room-grade result with explicit residual-penalty availability.
        /// </summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch within the session.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="gradeVersion">Grade output schema version.</param>
        /// <param name="completionStatus">Completion status assigned by the operator.</param>
        /// <param name="perGuardBreakdown">Per-guard component breakdowns.</param>
        /// <param name="incidentPenalty">Room incident penalty diagnostic.</param>
        /// <param name="residualPenalty">Room residual penalty diagnostic.</param>
        /// <param name="hasResidualPenalty">Whether the residual diagnostic is available.</param>
        /// <param name="qualityScore">Final room quality score.</param>
        /// <param name="grade">Final room grade.</param>
        /// <param name="contributingEventIds">Event IDs contributing to the result.</param>
        public RoomGradeFinalized(
            string sessionId,
            long attemptEpoch,
            string roomId,
            int gradeVersion,
            GradeCompletionStatus completionStatus,
            IReadOnlyList<GuardGradeBreakdown> perGuardBreakdown,
            float incidentPenalty,
            float residualPenalty,
            bool hasResidualPenalty,
            float qualityScore,
            RoomGrade grade,
            IReadOnlyList<string> contributingEventIds)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            RoomId = roomId ?? string.Empty;
            GradeVersion = gradeVersion;
            CompletionStatus = completionStatus;
            PerGuardBreakdown = CopyList(perGuardBreakdown);
            IncidentPenalty = incidentPenalty;
            ResidualPenalty = residualPenalty;
            HasResidualPenalty = hasResidualPenalty;
            QualityScore = qualityScore;
            Grade = grade;
            ContributingEventIds = CopySortedEventIds(contributingEventIds);
        }

        /// <summary>Gets the session identity.</summary>
        public string SessionId { get; }

        /// <summary>Gets the attempt epoch.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Gets the room identity.</summary>
        public string RoomId { get; }

        /// <summary>Gets the grade output schema version.</summary>
        public int GradeVersion { get; }

        /// <summary>Gets the completion status.</summary>
        public GradeCompletionStatus CompletionStatus { get; }

        /// <summary>Gets the copied per-guard component breakdowns.</summary>
        public IReadOnlyList<GuardGradeBreakdown> PerGuardBreakdown { get; }

        /// <summary>Gets the room incident penalty diagnostic.</summary>
        public float IncidentPenalty { get; }

        /// <summary>Gets the room residual penalty diagnostic.</summary>
        public float ResidualPenalty { get; }

        /// <summary>Gets whether the residual penalty diagnostic is available.</summary>
        public bool HasResidualPenalty { get; }

        /// <summary>Gets the final room quality score.</summary>
        public float QualityScore { get; }

        /// <summary>Gets the final room grade.</summary>
        public RoomGrade Grade { get; }

        /// <summary>Gets ordinally sorted contributing event IDs.</summary>
        public IReadOnlyList<string> ContributingEventIds { get; }

        private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> source)
        {
            var copy = new List<T>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    copy.Add(source[i]);
            }

            return new ReadOnlyCollection<T>(copy);
        }

        private static IReadOnlyList<string> CopySortedEventIds(
            IReadOnlyList<string> source)
        {
            var copy = new List<string>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    copy.Add(source[i]);
            }

            copy.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(copy);
        }
    }

    /// <summary>
    /// Pure deterministic finalizer for the Suspicion Meter / Grade design contract.
    /// It consumes injected completion evidence and never polls Unity or gameplay state.
    /// </summary>
    public static class RoomGradeOperator
    {
        private readonly struct GuardInput
        {
            public GuardInput(string guardEid, int chaseEntryCount,
                int fruitlessResolutionCount, int captureCount,
                float residual, float exposureWeight)
            {
                GuardEid = guardEid;
                ChaseEntryCount = chaseEntryCount;
                FruitlessResolutionCount = fruitlessResolutionCount;
                CaptureCount = captureCount;
                Residual = residual;
                ExposureWeight = exposureWeight;
            }

            public readonly string GuardEid;
            public readonly int ChaseEntryCount;
            public readonly int FruitlessResolutionCount;
            public readonly int CaptureCount;
            public readonly float Residual;
            public readonly float ExposureWeight;
        }

        private readonly struct GuardScore
        {
            public GuardScore(GuardGradeBreakdown breakdown)
            {
                Breakdown = breakdown;
            }

            public readonly GuardGradeBreakdown Breakdown;
        }

        /// <summary>
        /// Finalizes one room using the approved incident, residual, and grade formulas.
        /// For multiple guards, room component diagnostics and quality are exposure-weighted
        /// means of the per-guard values; each per-guard incident penalty retains its cap.
        /// </summary>
        /// <param name="boundary">Session-owned room completion evidence.</param>
        /// <param name="aggregate">Reduced trace aggregate for the same room scope.</param>
        /// <param name="configuration">Registry-backed grade configuration.</param>
        /// <returns>An immutable completed, failed, or unresolved room result.</returns>
        public static RoomGradeFinalized FinalizeRoom(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            SuspicionGradeConfiguration configuration)
        {
            if (boundary == null || !IsValidBoundaryIdentity(boundary))
                return CreateUnresolved(boundary, aggregate, configuration);

            bool explicitFailure = boundary.IsCapture
                || !boundary.IsCompletedRoom;
            bool validAggregate = HasValidMatchingAggregate(boundary, aggregate);

            // A successful boundary may be promoted to Failed by a valid trace
            // Capture, even when the boundary flags and Capture weight disagree.
            // Invalid maps remain unresolved for successful boundaries.
            if (!explicitFailure && !boundary.HasValidMapCopies)
                return CreateUnresolved(boundary, aggregate, configuration);
            if (!explicitFailure && validAggregate && aggregate.CaptureCount > 0)
                return CreateFailed(boundary, aggregate, configuration);
            if (explicitFailure)
                return CreateFailed(boundary, aggregate, configuration);

            if (configuration == null
                || !boundary.HasMandatoryTrace
                || !boundary.HasFinalResidualSnapshot
                || !validAggregate)
                return CreateUnresolved(boundary, aggregate, configuration);

            List<GuardScore> guardScores;
            if (!TryBuildCompletedScores(boundary, aggregate, configuration,
                out guardScores))
                return CreateUnresolved(boundary, aggregate, configuration);

            float incidentPenalty;
            float residualPenalty;
            float qualityScore;
            if (!TryCalculateRoomValues(guardScores, out incidentPenalty,
                out residualPenalty, out qualityScore))
                return CreateUnresolved(boundary, aggregate, configuration);

            return new RoomGradeFinalized(
                boundary.SessionId,
                boundary.AttemptEpoch,
                boundary.RoomId,
                configuration.GradeOperatorSchemaVersion,
                GradeCompletionStatus.Completed,
                ExtractBreakdowns(guardScores),
                incidentPenalty,
                residualPenalty,
                true,
                qualityScore,
                MapGrade(qualityScore, configuration),
                aggregate.ContributingEventIds);
        }

        private static bool TryBuildCompletedScores(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            SuspicionGradeConfiguration configuration,
            out List<GuardScore> guardScores)
        {
            guardScores = new List<GuardScore>();
            List<GuardInput> inputs;
            if (!TryBuildGuardInputs(boundary, aggregate, out inputs))
                return false;

            for (int i = 0; i < inputs.Count; i++)
            {
                GuardScore score;
                if (!TryCalculateGuardScore(inputs[i], configuration,
                    out score))
                    return false;
                guardScores.Add(score);
            }

            return guardScores.Count > 0;
        }

        private static bool TryBuildGuardInputs(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            out List<GuardInput> inputs)
        {
            inputs = new List<GuardInput>();
            var countsByGuard = new Dictionary<string, GuardGradeBreakdown>(
                StringComparer.Ordinal);
            long chaseTotal;
            long fruitlessTotal;
            long captureTotal;
            if (!TryValidateAggregateCounts(aggregate, countsByGuard,
                out chaseTotal, out fruitlessTotal, out captureTotal))
                return false;

            var guardIds = new List<string>();
            foreach (string guardId in countsByGuard.Keys)
                guardIds.Add(guardId);
            if (!TryAddResidualGuardIds(boundary, countsByGuard, guardIds))
                return false;

            guardIds.Sort(StringComparer.Ordinal);
            if (guardIds.Count == 0
                || !TryValidateAggregateTotals(aggregate, countsByGuard.Count,
                    chaseTotal, fruitlessTotal, captureTotal, guardIds.Count))
                return false;
            if (!TryValidateExposureKeys(boundary, guardIds))
                return false;

            bool hasPerGuardCounts = countsByGuard.Count > 0;
            for (int i = 0; i < guardIds.Count; i++)
            {
                GuardInput input;
                if (!TryCreateGuardInput(boundary, aggregate, countsByGuard,
                    guardIds[i], hasPerGuardCounts, guardIds.Count == 1,
                    out input))
                    return false;
                inputs.Add(input);
            }

            return true;
        }

        private static bool TryValidateAggregateCounts(
            GradeTraceAggregate aggregate,
            Dictionary<string, GuardGradeBreakdown> countsByGuard,
            out long chaseTotal,
            out long fruitlessTotal,
            out long captureTotal)
        {
            chaseTotal = 0L;
            fruitlessTotal = 0L;
            captureTotal = 0L;
            if (aggregate == null
                || aggregate.ChaseEntryCount < 0
                || aggregate.FruitlessResolutionCount < 0
                || aggregate.CaptureCount < 0
                || aggregate.PerGuard == null)
                return false;

            try
            {
                for (int i = 0; i < aggregate.PerGuard.Count; i++)
                {
                    GuardGradeBreakdown breakdown = aggregate.PerGuard[i];
                    if (breakdown == null
                        || string.IsNullOrWhiteSpace(breakdown.GuardEid)
                        || breakdown.ChaseEntryCount < 0
                        || breakdown.FruitlessResolutionCount < 0
                        || breakdown.CaptureCount < 0
                        || countsByGuard.ContainsKey(breakdown.GuardEid))
                        return false;

                    countsByGuard.Add(breakdown.GuardEid, breakdown);
                    chaseTotal = checked(chaseTotal + breakdown.ChaseEntryCount);
                    fruitlessTotal = checked(fruitlessTotal
                        + breakdown.FruitlessResolutionCount);
                    captureTotal = checked(captureTotal + breakdown.CaptureCount);
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            return true;
        }

        private static bool TryValidateAggregateTotals(
            GradeTraceAggregate aggregate,
            int perGuardCount,
            long chaseTotal,
            long fruitlessTotal,
            long captureTotal,
            int guardCount)
        {
            if (perGuardCount > 0
                && (chaseTotal != aggregate.ChaseEntryCount
                    || fruitlessTotal != aggregate.FruitlessResolutionCount
                    || captureTotal != aggregate.CaptureCount))
                return false;
            if (perGuardCount == 0 && guardCount > 1
                && (aggregate.ChaseEntryCount != 0
                    || aggregate.FruitlessResolutionCount != 0
                    || aggregate.CaptureCount != 0))
                return false;
            return true;
        }

        private static bool TryAddResidualGuardIds(
            RoomCompletionBoundary boundary,
            Dictionary<string, GuardGradeBreakdown> countsByGuard,
            List<string> guardIds)
        {
            try
            {
                foreach (KeyValuePair<string, float> pair
                    in boundary.FinalResidualByGuard)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)
                        || float.IsNaN(pair.Value)
                        || float.IsInfinity(pair.Value))
                        return false;
                    if (!countsByGuard.ContainsKey(pair.Key))
                        guardIds.Add(pair.Key);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static bool TryValidateExposureKeys(
            RoomCompletionBoundary boundary, List<string> guardIds)
        {
            var guardSet = new HashSet<string>(guardIds, StringComparer.Ordinal);
            try
            {
                foreach (KeyValuePair<string, float> pair
                    in boundary.ExposureWeightByGuard)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)
                        || !guardSet.Contains(pair.Key)
                        || !IsFinite(pair.Value)
                        || pair.Value <= 0f)
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static bool TryCreateGuardInput(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            Dictionary<string, GuardGradeBreakdown> countsByGuard,
            string guardId,
            bool hasPerGuardCounts,
            bool allowImplicitExposure,
            out GuardInput input)
        {
            input = default(GuardInput);
            float residual;
            if (!boundary.FinalResidualByGuard.TryGetValue(guardId,
                out residual) || !IsFinite(residual))
                return false;

            GuardGradeBreakdown breakdown;
            if (!countsByGuard.TryGetValue(guardId, out breakdown))
            {
                breakdown = new GuardGradeBreakdown(
                    guardId,
                    hasPerGuardCounts ? 0 : aggregate.ChaseEntryCount,
                    hasPerGuardCounts ? 0 : aggregate.FruitlessResolutionCount,
                    hasPerGuardCounts ? 0 : aggregate.CaptureCount);
            }

            float exposureWeight;
            if (!boundary.ExposureWeightByGuard.TryGetValue(
                guardId, out exposureWeight))
            {
                if (!allowImplicitExposure)
                    return false;
                exposureWeight = 1f;
            }
            if (!IsFinite(exposureWeight) || exposureWeight <= 0f)
                return false;

            input = new GuardInput(guardId, breakdown.ChaseEntryCount,
                breakdown.FruitlessResolutionCount, breakdown.CaptureCount,
                residual, exposureWeight);
            return true;
        }

        private static bool TryCalculateGuardScore(
            GuardInput input,
            SuspicionGradeConfiguration configuration,
            out GuardScore score)
        {
            score = default(GuardScore);
            float incidentPenalty;
            if (!TryCalculateIncidentPenalty(input.ChaseEntryCount,
                input.FruitlessResolutionCount, input.CaptureCount,
                configuration, out incidentPenalty))
                return false;

            double normalizedResidual = Clamp(
                (double)input.Residual / configuration.RMax, 0d, 1d);
            double residualPenalty = configuration.GradeWeightResidual
                * normalizedResidual;
            double quality = 100d - incidentPenalty - residualPenalty;
            if (!IsFinite(residualPenalty) || !IsFinite(quality))
                return false;

            float residualPenaltyValue;
            float qualityValue;
            if (!TryFiniteFloat(residualPenalty, out residualPenaltyValue)
                || !TryFiniteFloat(Clamp(quality, 0d, 100d), out qualityValue))
                return false;

            score = new GuardScore(new GuardGradeBreakdown(
                input.GuardEid,
                input.ChaseEntryCount,
                input.FruitlessResolutionCount,
                input.CaptureCount,
                incidentPenalty,
                residualPenaltyValue,
                qualityValue,
                input.ExposureWeight));
            return true;
        }

        private static bool TryCalculateRoomValues(
            IReadOnlyList<GuardScore> guardScores,
            out float incidentPenalty,
            out float residualPenalty,
            out float qualityScore)
        {
            incidentPenalty = 0f;
            residualPenalty = 0f;
            qualityScore = 0f;
            if (guardScores == null || guardScores.Count == 0)
                return false;
            if (guardScores.Count == 1)
            {
                GuardGradeBreakdown only = guardScores[0].Breakdown;
                incidentPenalty = only.IncidentPenalty;
                residualPenalty = only.ResidualPenalty;
                qualityScore = only.QualityScore;
                return IsFinite(incidentPenalty) && IsFinite(residualPenalty)
                    && IsFinite(qualityScore);
            }

            double weightedIncident = 0d;
            double weightedResidual = 0d;
            double weightedQuality = 0d;
            double totalWeight = 0d;
            for (int i = 0; i < guardScores.Count; i++)
            {
                GuardGradeBreakdown breakdown = guardScores[i].Breakdown;
                double weight = breakdown.ExposureWeight;
                weightedIncident += breakdown.IncidentPenalty * weight;
                weightedResidual += breakdown.ResidualPenalty * weight;
                weightedQuality += breakdown.QualityScore * weight;
                totalWeight += weight;
            }

            if (!IsFinite(weightedIncident) || !IsFinite(weightedResidual)
                || !IsFinite(weightedQuality) || !IsFinite(totalWeight)
                || totalWeight <= 0d)
                return false;

            return TryFiniteFloat(weightedIncident / totalWeight, out incidentPenalty)
                && TryFiniteFloat(weightedResidual / totalWeight,
                    out residualPenalty)
                && TryFiniteFloat(Clamp(weightedQuality / totalWeight, 0d, 100d),
                    out qualityScore);
        }

        private static bool TryCalculateIncidentPenalty(
            int chaseEntryCount,
            int fruitlessResolutionCount,
            int captureCount,
            SuspicionGradeConfiguration configuration,
            out float penalty)
        {
            penalty = 0f;
            double rawPenalty = (double)configuration.GradeWeightChase
                * chaseEntryCount
                + (double)configuration.GradeWeightFruitless
                * fruitlessResolutionCount
                + (double)configuration.GradeWeightCapture
                * captureCount;
            if (!IsFinite(rawPenalty))
                return false;

            double capped = Math.Min(configuration.GradePenaltyCap, rawPenalty);
            return TryFiniteFloat(capped, out penalty);
        }

        private static bool TryBuildFailedDiagnostics(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            SuspicionGradeConfiguration configuration,
            out IReadOnlyList<GuardGradeBreakdown> breakdowns,
            out float incidentPenalty)
        {
            breakdowns = new ReadOnlyCollection<GuardGradeBreakdown>(
                new List<GuardGradeBreakdown>());
            incidentPenalty = 0f;
            if (aggregate == null
                || !HasValidMatchingAggregate(boundary, aggregate)
                || !boundary.HasValidMapCopies)
                return false;

            var result = new List<GuardGradeBreakdown>();
            float aggregateIncident;
            if (configuration == null
                || !TryCalculateIncidentPenalty(aggregate.ChaseEntryCount,
                    aggregate.FruitlessResolutionCount, aggregate.CaptureCount,
                    configuration, out aggregateIncident))
                aggregateIncident = 0f;

            var orderedBreakdowns = new List<GuardGradeBreakdown>();
            for (int i = 0; i < aggregate.PerGuard.Count; i++)
                orderedBreakdowns.Add(aggregate.PerGuard[i]);
            orderedBreakdowns.Sort(delegate(GuardGradeBreakdown left,
                GuardGradeBreakdown right)
            {
                return StringComparer.Ordinal.Compare(left.GuardEid, right.GuardEid);
            });

            for (int i = 0; i < orderedBreakdowns.Count; i++)
            {
                GuardGradeBreakdown source = orderedBreakdowns[i];
                float perGuardIncident = 0f;
                if (configuration != null
                    && !TryCalculateIncidentPenalty(source.ChaseEntryCount,
                        source.FruitlessResolutionCount, source.CaptureCount,
                        configuration, out perGuardIncident))
                    return false;
                result.Add(new GuardGradeBreakdown(source.GuardEid,
                    source.ChaseEntryCount, source.FruitlessResolutionCount,
                    source.CaptureCount, perGuardIncident, 0f, 0f, 0f));
            }

            breakdowns = new ReadOnlyCollection<GuardGradeBreakdown>(result);
            incidentPenalty = aggregateIncident;
            return true;
        }

        private static bool TryValidateAggregateForDiagnostics(
            GradeTraceAggregate aggregate)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long chaseTotal = 0L;
            long fruitlessTotal = 0L;
            long captureTotal = 0L;
            try
            {
                for (int i = 0; i < aggregate.PerGuard.Count; i++)
                {
                    GuardGradeBreakdown breakdown = aggregate.PerGuard[i];
                    if (breakdown == null
                        || string.IsNullOrWhiteSpace(breakdown.GuardEid)
                        || !seen.Add(breakdown.GuardEid)
                        || breakdown.ChaseEntryCount < 0
                        || breakdown.FruitlessResolutionCount < 0
                        || breakdown.CaptureCount < 0)
                        return false;
                    chaseTotal = checked(chaseTotal + breakdown.ChaseEntryCount);
                    fruitlessTotal = checked(fruitlessTotal
                        + breakdown.FruitlessResolutionCount);
                    captureTotal = checked(captureTotal + breakdown.CaptureCount);
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            if (aggregate.ChaseEntryCount < 0
                || aggregate.FruitlessResolutionCount < 0
                || aggregate.CaptureCount < 0)
                return false;

            if (aggregate.PerGuard.Count == 0)
                return aggregate.ChaseEntryCount == 0
                    && aggregate.FruitlessResolutionCount == 0
                    && aggregate.CaptureCount == 0;

            return chaseTotal == aggregate.ChaseEntryCount
                && fruitlessTotal == aggregate.FruitlessResolutionCount
                && captureTotal == aggregate.CaptureCount;
        }

        private static bool HasValidContributingEventIds(
            GradeTraceAggregate aggregate)
        {
            if (aggregate == null || aggregate.ContributingEventIds == null
                || aggregate.ChaseEntryCount < 0
                || aggregate.FruitlessResolutionCount < 0
                || aggregate.CaptureCount < 0)
                return false;

            long incidentCount;
            try
            {
                incidentCount = checked((long)aggregate.ChaseEntryCount
                    + aggregate.FruitlessResolutionCount
                    + aggregate.CaptureCount);
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < aggregate.ContributingEventIds.Count; i++)
                {
                    string eventId = aggregate.ContributingEventIds[i];
                    if (string.IsNullOrWhiteSpace(eventId)
                        || !seenIds.Add(eventId))
                        return false;
                }

                return aggregate.ContributingEventIds.Count == incidentCount;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static IReadOnlyList<GuardGradeBreakdown> ExtractBreakdowns(
            IReadOnlyList<GuardScore> guardScores)
        {
            var breakdowns = new List<GuardGradeBreakdown>(guardScores.Count);
            for (int i = 0; i < guardScores.Count; i++)
                breakdowns.Add(guardScores[i].Breakdown);
            return new ReadOnlyCollection<GuardGradeBreakdown>(breakdowns);
        }

        private static RoomGrade MapGrade(
            float qualityScore, SuspicionGradeConfiguration configuration)
        {
            if (!IsFinite(qualityScore))
                return RoomGrade.None;
            if (qualityScore >= configuration.GradeThresholdS)
                return RoomGrade.S;
            if (qualityScore >= configuration.GradeThresholdA)
                return RoomGrade.A;
            if (qualityScore >= configuration.GradeThresholdB)
                return RoomGrade.B;
            return RoomGrade.NeedsImprovement;
        }

        private static bool HasMatchingAggregate(
            RoomCompletionBoundary boundary, GradeTraceAggregate aggregate)
        {
            return boundary != null
                && aggregate != null
                && IsValidBoundaryIdentity(boundary)
                && IsValidAggregateIdentity(aggregate)
                && aggregate.AttemptEpoch == boundary.AttemptEpoch
                && string.Equals(aggregate.SessionId, boundary.SessionId,
                    StringComparison.Ordinal)
                && string.Equals(aggregate.RoomId, boundary.RoomId,
                    StringComparison.Ordinal);
        }

        private static bool HasValidMatchingAggregate(
            RoomCompletionBoundary boundary, GradeTraceAggregate aggregate)
        {
            return HasMatchingAggregate(boundary, aggregate)
                && HasValidContributingEventIds(aggregate)
                && TryValidateAggregateForDiagnostics(aggregate);
        }

        private static bool IsValidBoundaryIdentity(
            RoomCompletionBoundary boundary)
        {
            return boundary != null
                && IsValidIdentity(boundary.SessionId, boundary.AttemptEpoch,
                    boundary.RoomId);
        }

        private static bool IsValidAggregateIdentity(GradeTraceAggregate aggregate)
        {
            return aggregate != null
                && IsValidIdentity(aggregate.SessionId, aggregate.AttemptEpoch,
                    aggregate.RoomId);
        }

        private static bool IsValidIdentity(
            string sessionId, long attemptEpoch, string roomId)
        {
            return !string.IsNullOrWhiteSpace(sessionId)
                && attemptEpoch >= 0
                && !string.IsNullOrWhiteSpace(roomId);
        }

        private static RoomGradeFinalized CreateFailed(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            SuspicionGradeConfiguration configuration)
        {
            IReadOnlyList<GuardGradeBreakdown> breakdowns;
            float incidentPenalty;
            bool hasDiagnostics = TryBuildFailedDiagnostics(boundary, aggregate,
                configuration, out breakdowns, out incidentPenalty);
            return new RoomGradeFinalized(
                boundary.SessionId,
                boundary.AttemptEpoch,
                boundary.RoomId,
                configuration == null ? 0 : configuration.GradeOperatorSchemaVersion,
                GradeCompletionStatus.Failed,
                hasDiagnostics ? breakdowns : new List<GuardGradeBreakdown>(),
                hasDiagnostics ? incidentPenalty : 0f,
                0f,
                false,
                0f,
                RoomGrade.None,
                hasDiagnostics ? GetValidatedEventIds(boundary, aggregate) : null);
        }

        private static RoomGradeFinalized CreateUnresolved(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate,
            SuspicionGradeConfiguration configuration)
        {
            bool validAggregate = HasValidMatchingAggregate(boundary, aggregate);
            string sessionId = boundary == null
                ? string.Empty : boundary.SessionId;
            long attemptEpoch = boundary == null ? 0L : boundary.AttemptEpoch;
            string roomId = boundary == null ? string.Empty : boundary.RoomId;
            int version = configuration == null
                ? 0 : configuration.GradeOperatorSchemaVersion;

            return new RoomGradeFinalized(
                sessionId,
                attemptEpoch,
                roomId,
                version,
                GradeCompletionStatus.Unresolved,
                new List<GuardGradeBreakdown>(),
                0f,
                0f,
                false,
                0f,
                RoomGrade.None,
                validAggregate ? GetValidatedEventIds(boundary, aggregate)
                    : null);
        }

        private static IReadOnlyList<string> GetValidatedEventIds(
            RoomCompletionBoundary boundary,
            GradeTraceAggregate aggregate)
        {
            if (!HasValidMatchingAggregate(boundary, aggregate))
                return null;
            return aggregate.ContributingEventIds;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool TryFiniteFloat(double value, out float result)
        {
            result = 0f;
            if (!IsFinite(value) || value > float.MaxValue
                || value < -float.MaxValue)
                return false;
            result = (float)value;
            return IsFinite(result);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
