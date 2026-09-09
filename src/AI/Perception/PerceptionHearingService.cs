using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Immutable active-guard input captured by Perception at a hearing boundary.
    /// The stable sort position is the guard's feet position; the eye endpoint is
    /// derived from feet plus the authored local eye offset.
    /// </summary>
    public sealed class GuardHearingSnapshot
    {
        /// <summary>
        /// Compatibility constructor when the provider already supplies an eye
        /// endpoint. New providers should use the feet/eye-offset overload.
        /// </summary>
        public GuardHearingSnapshot(string guardEid, Vector3 eyePosition,
            float residualR)
            : this(guardEid, eyePosition, Vector3.zero, residualR)
        {
        }

        /// <summary>Creates a stable feet and eye-offset snapshot.</summary>
        public GuardHearingSnapshot(string guardEid, Vector3 feetPosition,
            Vector3 eyeOffset, float residualR)
        {
            if (string.IsNullOrWhiteSpace(guardEid))
                throw new ArgumentException("guardEid");
            if (!IsFinite(feetPosition) || !IsFinite(eyeOffset))
                throw new ArgumentException("guard-position");
            if (float.IsNaN(residualR) || float.IsInfinity(residualR))
                throw new ArgumentException("residualR");

            GuardEid = guardEid;
            FeetPosition = feetPosition;
            EyeOffset = eyeOffset;
            ResidualR = residualR;
        }

        public string GuardEid { get; }
        public Vector3 FeetPosition { get; }
        public Vector3 EyeOffset { get; }
        public Vector3 EyePosition { get { return FeetPosition + EyeOffset; } }
        public Vector3 StableSnapshotPosition { get { return FeetPosition; } }
        public float ResidualR { get; }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Supplies a stable, active-guard snapshot at the scheduled hearing boundary.
    /// Implementations must not expose a live mutable collection to the service.
    /// </summary>
    public interface IGuardHearingSnapshotProvider
    {
        IList<GuardHearingSnapshot> CaptureActiveGuardSnapshots();
    }

    /// <summary>
    /// Hearing-boundary adapter. It retains raw facts at a transactional handoff,
    /// evaluates them on an independent virtual 5 Hz schedule, and publishes
    /// immutable per-guard relays for the FSM phase.
    /// </summary>
    public sealed class PerceptionHearingService
    {
        private sealed class PendingFact
        {
            public NoisePublished Fact;
            public long Sequence;
            public float AdmittedAt;
            public float Deadline;
            public int NextGuardIndex;
            // A fact that cannot fit in the current work budget retains the exact
            // boundary snapshot it started against. Reusing only an index into a
            // later live snapshot could skip or duplicate guards after churn.
            public IList<GuardHearingSnapshot> GuardSnapshot;
        }

        private sealed class PendingRelay
        {
            public NoiseHeardRelay Relay;
            public int RetryCount;
            public float Deadline;
            public bool IsNewEntry;
        }

        /// <summary>
        /// Synthetic identity used only when a due-boundary rejection has no
        /// gameplay event envelope. It lets detailed trace sinks retain the same
        /// session, epoch, retry, queue, and ordering fields as event failures.
        /// </summary>
        private sealed class BoundaryDiagnosticEvent : IEvent, IEventPhase
        {
            public BoundaryDiagnosticEvent(string sessionId, long attemptEpoch,
                float timestamp)
            {
                SessionId = sessionId;
                AttemptEpoch = attemptEpoch;
                Timestamp = timestamp;
                Publisher = "PerceptionHearingService";
                Identity = "hearing-boundary:" + timestamp.ToString("R");
            }

            public string SessionId { get; private set; }
            public long AttemptEpoch { get; private set; }
            public float Timestamp { get; private set; }
            public string Publisher { get; private set; }
            public string Identity { get; private set; }
            public EventBusPhase Phase { get { return EventBusPhase.Hearing; } }
        }

        private sealed class PairWork
        {
            public NoisePublished Fact;
            public GuardHearingSnapshot Snapshot;
            public long Sequence;
            public float Deadline;
        }

        private readonly IEventBus _eventBus;
        private readonly IVirtualTickClock _clock;
        private readonly PhysicsQueryProfile _physicsProfile;
        private readonly IGuardHearingSnapshotProvider _snapshotProvider;
        private readonly float _hearingYHardCutoff;
        private readonly float _movementOriginOffset;
        private readonly float _hearingInterval;
        private readonly int _maxHearingBoundariesPerFrame;
        private readonly int _maxHearingGuards;
        private readonly int _maxHearingFacts;
        private readonly int _maxHearingPairs;
        private readonly int _hearingWorkCapacity;
        private readonly int _rawFactQueueCapacity;
        private readonly int _relayQueueCapacity;
        private readonly int _maxDeferredHearingBoundaries;
        private readonly int _maxRelayRetries;
        private readonly float _mathRelativeTolerance;
        private readonly IEventDiagnosticSink _diagnosticSink;
        private readonly Dictionary<string, string> _activeEntryByGuard =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, LivenessFact> _livenessByGuard =
            new Dictionary<string, LivenessFact>(StringComparer.Ordinal);
        private readonly HashSet<EventIdentityKey> _acceptedFacts =
            new HashSet<EventIdentityKey>();
        private readonly List<PendingFact> _pendingFacts = new List<PendingFact>();
        private readonly List<PairWork> _deferredPairs = new List<PairWork>();
        private readonly List<PendingRelay> _pendingRelays = new List<PendingRelay>();
        private readonly List<float> _deferredBoundaries = new List<float>();
        private readonly HashSet<EventIdentityKey> _deadlineFailedFacts =
            new HashSet<EventIdentityKey>();
        private SubscriptionToken _noiseToken;
        private SubscriptionToken _livenessToken;
        private SubscriptionToken _outcomeToken;
        private SubscriptionToken _clockToken;
        private bool _isBound;
        private bool _clockSubscribed;
        private bool _hearingPhaseInitialized;
        private float _nextHearingBoundary;
        private long _nextSequence = 1;
        private ulong _nextEntryId = 1;

        /// <summary>
        /// Creates a hearing service using the registry-compatible default caps.
        /// </summary>
        public PerceptionHearingService(IEventBus eventBus, IVirtualTickClock clock,
            PhysicsQueryProfile physicsProfile,
            IGuardHearingSnapshotProvider snapshotProvider,
            float hearingYHardCutoff, int relayQueueCapacity = 512,
            int maxRelayRetries = 3)
            : this(eventBus, clock, physicsProfile, snapshotProvider,
                hearingYHardCutoff, 0.25f, 0.2f, 1, 4, 30, 8, 30, 512,
                512, relayQueueCapacity, maxRelayRetries, 0.000001f, null)
        {
        }

        /// <summary>
        /// Creates a hearing service from one immutable runtime configuration.
        /// </summary>
        public PerceptionHearingService(IEventBus eventBus, IVirtualTickClock clock,
            PhysicsQueryProfile physicsProfile,
            IGuardHearingSnapshotProvider snapshotProvider,
            float hearingYHardCutoff, NoiseRuntimeConfiguration configuration,
            IEventDiagnosticSink diagnosticSink = null)
            : this(eventBus, clock, physicsProfile, snapshotProvider,
                hearingYHardCutoff,
                configuration == null ? 0.25f : configuration.F12NoiseOriginOffset,
                configuration == null ? 0.2f : configuration.HearingIntervalSeconds,
                configuration == null ? 1 : configuration.MaxHearingBoundariesPerFrame,
                configuration == null ? 4 : configuration.MaxDeferredHearingBoundaries,
                configuration == null ? 30 : configuration.MaxHearingGuards,
                configuration == null ? 8 : configuration.MaxHearingFacts,
                configuration == null ? 30 : configuration.MaxHearingPairs,
                configuration == null ? 512 : configuration.HearingWorkCapacity,
                configuration == null ? 128 : configuration.RawFactQueueCapacity,
                configuration == null ? 512 : configuration.RelayQueueCapacity,
                configuration == null ? 3 : configuration.MaxRetries,
                configuration == null ? 0.000001f : configuration.MathRelativeTolerance,
                diagnosticSink)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
        }

        private PerceptionHearingService(IEventBus eventBus, IVirtualTickClock clock,
            PhysicsQueryProfile physicsProfile,
            IGuardHearingSnapshotProvider snapshotProvider,
            float hearingYHardCutoff, float movementOriginOffset,
            float hearingInterval, int maxHearingBoundariesPerFrame,
            int maxDeferredHearingBoundaries, int maxHearingGuards,
            int maxHearingFacts, int maxHearingPairs,
            int hearingWorkCapacity, int rawFactQueueCapacity,
            int relayQueueCapacity, int maxRelayRetries,
            float mathRelativeTolerance, IEventDiagnosticSink diagnosticSink)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _physicsProfile = physicsProfile
                ?? throw new ArgumentNullException(nameof(physicsProfile));
            _snapshotProvider = snapshotProvider
                ?? throw new ArgumentNullException(nameof(snapshotProvider));
            if (!IsFinitePositive(hearingYHardCutoff))
                throw new ArgumentOutOfRangeException(nameof(hearingYHardCutoff));
            if (!IsFiniteNonNegative(movementOriginOffset))
                throw new ArgumentOutOfRangeException(nameof(movementOriginOffset));
            if (!IsFinitePositive(hearingInterval))
                throw new ArgumentOutOfRangeException(nameof(hearingInterval));
            if (maxHearingBoundariesPerFrame <= 0
                || maxDeferredHearingBoundaries <= 0 || maxHearingGuards <= 0
                || maxHearingFacts <= 0 || maxHearingPairs <= 0
                || hearingWorkCapacity <= 0 || rawFactQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(hearingWorkCapacity));
            if (relayQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(relayQueueCapacity));
            if (maxRelayRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRelayRetries));
            if (!IsFinitePositive(mathRelativeTolerance))
                throw new ArgumentOutOfRangeException(nameof(mathRelativeTolerance));

            _hearingYHardCutoff = hearingYHardCutoff;
            _movementOriginOffset = movementOriginOffset;
            _hearingInterval = hearingInterval;
            _maxHearingBoundariesPerFrame = maxHearingBoundariesPerFrame;
            _maxDeferredHearingBoundaries = maxDeferredHearingBoundaries;
            _maxHearingGuards = maxHearingGuards;
            _maxHearingFacts = maxHearingFacts;
            _maxHearingPairs = maxHearingPairs;
            _hearingWorkCapacity = hearingWorkCapacity;
            _rawFactQueueCapacity = rawFactQueueCapacity;
            _relayQueueCapacity = relayQueueCapacity;
            _maxRelayRetries = maxRelayRetries;
            _mathRelativeTolerance = mathRelativeTolerance;
            _diagnosticSink = diagnosticSink;
            LastRejectionCode = string.Empty;
        }

        public int PendingFactCount { get { return _pendingFacts.Count; } }
        public int DeferredPairCount { get { return _deferredPairs.Count; } }
        public int PendingRelayCount { get { return _pendingRelays.Count; } }
        public int RelayQueueCapacity { get { return _relayQueueCapacity; } }
        public int HearingWorkCapacity { get { return _hearingWorkCapacity; } }
        /// <summary>Number of relay records rejected at the hearing boundary.</summary>
        public int RejectedRelayCount { get; private set; }

        /// <summary>Number of raw facts rejected or deadline-expired.</summary>
        public int RejectedFactCount { get; private set; }

        /// <summary>Number of due boundaries rejected by bounded catch-up.</summary>
        public int RejectedBoundaryCount { get; private set; }

        /// <summary>Virtual time of the most recently rejected boundary.</summary>
        public float LastRejectedBoundaryTime { get; private set; }

        /// <summary>Stable code for the most recent hearing rejection.</summary>
        public string LastRejectionCode { get; private set; }

        /// <summary>
        /// Subscribes to transactional raw facts, liveness snapshots, relay
        /// outcomes, and the injected virtual clock.
        /// </summary>
        public void Bind()
        {
            BindWithClock(true);
        }

        /// <summary>
        /// Binds transport subscriptions without subscribing to the clock. The
        /// session phase coordinator calls ProcessHearingTick explicitly so hearing
        /// always runs between raw ingress and FSM decisions.
        /// </summary>
        public void BindWithoutClock()
        {
            BindWithClock(false);
        }

        private void BindWithClock(bool subscribeClock)
        {
            if (_isBound) return;
            _noiseToken = _eventBus.SubscribeHandoff<NoisePublished>(AcceptNoise);
            _livenessToken = _eventBus.Subscribe<LivenessFact>(OnLivenessFact);
            _outcomeToken = _eventBus.Subscribe<NoiseConsumptionOutcome>(
                OnNoiseConsumptionOutcome);
            _clockSubscribed = subscribeClock;
            if (subscribeClock)
                _clockToken = _clock.Subscribe(OnClockTick);
            if (!_hearingPhaseInitialized)
            {
                _nextHearingBoundary = GetNextBoundary(_clock.CurrentTime);
                _hearingPhaseInitialized = true;
            }
            _isBound = true;
        }

        /// <summary>Removes subscriptions without resetting episode state.</summary>
        public void Unbind()
        {
            if (!_isBound) return;
            _eventBus.Unsubscribe(_noiseToken);
            _eventBus.Unsubscribe(_livenessToken);
            _eventBus.Unsubscribe(_outcomeToken);
            if (_clockSubscribed)
                _clock.Unsubscribe(_clockToken);
            _clockSubscribed = false;
            _isBound = false;
        }

        /// <summary>
        /// Transactional raw-fact admission called by the Event Bus. The fact is
        /// retained until the next due boundary and is never evaluated in the
        /// transport callback.
        /// </summary>
        public EventHandoffResult AcceptNoise(EventEnvelope envelope)
        {
            if (envelope == null || envelope.Payload == null)
                return EventHandoffResult.Rejected("perception-hearing-invalid-envelope");
            NoisePublished noise = envelope.Payload as NoisePublished;
            if (noise == null)
                return EventHandoffResult.Rejected("perception-hearing-invalid-fact-type");
            if (!string.Equals(noise.SessionId, _eventBus.SessionId,
                    StringComparison.Ordinal)
                || noise.AttemptEpoch != _eventBus.AttemptEpoch)
            {
                return EventHandoffResult.Rejected("perception-hearing-stale-fact");
            }

            var factKey = new EventIdentityKey(noise.SessionId, noise.AttemptEpoch,
                noise.FactId.ToString("D"));
            if (_acceptedFacts.Contains(factKey))
                return EventHandoffResult.Duplicate();
            if (_pendingFacts.Count >= _rawFactQueueCapacity)
                return EventHandoffResult.Retry("perception-hearing-raw-queue-full");

            float admittedAt = _clock.CurrentTime;
            float deadlineAnchor = noise.SourceEventClassRank
                == (int)NoiseSourceKind.Burst
                ? noise.TerminalPublicationTime : noise.SourceTimestamp;
            float deadline = deadlineAnchor + _hearingInterval;
            if (admittedAt > deadline + _mathRelativeTolerance)
            {
                RecordRejectedFact("perception-hearing-deadline-missed", noise);
                return EventHandoffResult.Rejected(
                    "perception-hearing-deadline-missed");
            }

            _acceptedFacts.Add(factKey);
            _pendingFacts.Add(new PendingFact
            {
                Fact = noise,
                Sequence = _nextSequence++,
                AdmittedAt = admittedAt,
                Deadline = deadline,
                NextGuardIndex = 0
            });
            return EventHandoffResult.Accepted();
        }

        /// <summary>
        /// Compatibility adapter for older direct callback fixtures.
        /// </summary>
        public void OnNoisePublished(NoisePublished noise)
        {
            if (noise == null) return;
            AcceptNoise(new EventEnvelope(noise.SessionId, noise.AttemptEpoch,
                noise.Timestamp, noise.Publisher, noise.Identity, noise.Phase, noise));
        }

        /// <summary>
        /// Retries relay publication oldest-first. A terminal result is retained
        /// in the diagnostic counters and never silently dropped.
        /// </summary>
        public void FlushPendingRelays()
        {
            for (int i = 0; i < _pendingRelays.Count;)
            {
                PendingRelay pending = _pendingRelays[i];
                if (_clock.CurrentTime > pending.Deadline + _mathRelativeTolerance)
                {
                    // The source deadline belongs to the raw fact, not to the
                    // retry attempt. MarkFactDeadlineMissed removes every relay
                    // for that fact and releases only reservations created by a
                    // relay that never reached the FSM.
                    MarkFactDeadlineMissed(pending.Relay);
                    continue;
                }

                EventPublishResult result = _eventBus.Publish(pending.Relay);
                if (result.Admission == EventAdmission.Retry)
                {
                    pending.RetryCount = Math.Max(pending.RetryCount + 1,
                        result.RetryCount);
                    if (pending.RetryCount > _maxRelayRetries)
                    {
                        RejectRelay(pending.Relay,
                            "perception-hearing-relay-retry-exhausted",
                            pending.RetryCount);
                        ReleasePendingReservation(pending);
                        _pendingRelays.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                    continue;
                }

                if (result.Admission == EventAdmission.Rejected)
                    RejectRelay(pending.Relay, result.Code, pending.RetryCount);
                _pendingRelays.RemoveAt(i);
            }
        }

        /// <summary>
        /// Closes a guard's active compatibility reservation after an FSM terminal
        /// outcome. Authoritative runtime closure normally arrives via the outcome.
        /// </summary>
        public bool CloseEntry(string guardEid, string entryId)
        {
            if (string.IsNullOrWhiteSpace(guardEid) || string.IsNullOrWhiteSpace(entryId))
                return false;
            string activeEntry;
            if (!_activeEntryByGuard.TryGetValue(guardEid, out activeEntry)
                || !string.Equals(activeEntry, entryId, StringComparison.Ordinal))
                return false;
            _activeEntryByGuard.Remove(guardEid);
            return true;
        }

        /// <summary>
        /// Clears raw facts, deferred work, relay work, reservations, and liveness
        /// reads after the lifecycle owner advances the bus generation.
        /// </summary>
        public void ResetForBoundary()
        {
            _activeEntryByGuard.Clear();
            _livenessByGuard.Clear();
            _acceptedFacts.Clear();
            _pendingFacts.Clear();
            _deferredPairs.Clear();
            _pendingRelays.Clear();
            _deferredBoundaries.Clear();
            _deadlineFailedFacts.Clear();
            RejectedRelayCount = 0;
            RejectedFactCount = 0;
            RejectedBoundaryCount = 0;
            LastRejectedBoundaryTime = 0f;
            LastRejectionCode = string.Empty;
            _nextEntryId = 1;
            _nextSequence = 1;
            // The hearing phase is session-stable. Epoch resets invalidate work but
            // do not move the 5 Hz boundary relative to the virtual clock.
            if (!_hearingPhaseInitialized)
            {
                _nextHearingBoundary = GetNextBoundary(_clock.CurrentTime);
                _hearingPhaseInitialized = true;
            }
        }

        /// <summary>Returns the last FSM liveness fact for a guard, if present.</summary>
        public bool TryGetLiveness(string guardEid, out LivenessFact fact)
        {
            return _livenessByGuard.TryGetValue(guardEid, out fact);
        }

        private void OnClockTick(long tick, float delta)
        {
            if (!_isBound || _clock.IsPaused) return;

            // Standalone fixture mode admits raw facts here. Production mode admits
            // them in SessionPhaseCoordinator immediately before ProcessHearingTick.
            _eventBus.Drain(EventBusPhase.Hearing);
            ProcessHearingTick(tick, delta);
        }

        /// <summary>
        /// Processes due hearing boundaries after the coordinator has drained raw
        /// hearing ingress. This is the authoritative production scheduler seam.
        /// </summary>
        public void ProcessHearingTick(long tick, float delta)
        {
            if (!_isBound || _clock.IsPaused) return;
            ScheduleDueBoundaries(_clock.CurrentTime);

            int boundaries = 0;
            while (_deferredBoundaries.Count > 0
                && boundaries < _maxHearingBoundariesPerFrame)
            {
                float boundaryTime = _deferredBoundaries[0];
                _deferredBoundaries.RemoveAt(0);
                EvaluateBoundary(boundaryTime);
                boundaries++;
            }
        }

        private void ScheduleDueBoundaries(float now)
        {
            float epsilon = _mathRelativeTolerance;
            while (now + epsilon >= _nextHearingBoundary)
            {
                if (_deferredBoundaries.Count >= _maxDeferredHearingBoundaries)
                {
                    RecordBoundaryFailure(_nextHearingBoundary,
                        "perception-deferred-boundary-overflow-rejected");
                }
                else
                {
                    _deferredBoundaries.Add(_nextHearingBoundary);
                }
                _nextHearingBoundary += _hearingInterval;
            }
        }

        private void EvaluateBoundary(float boundaryTime)
        {
            FlushPendingRelays();
            var captured = _snapshotProvider.CaptureActiveGuardSnapshots();
            if (captured == null || captured.Count == 0)
            {
                // No active guard is a valid empty snapshot, but admitted facts
                // and previously staged pairs still have deadlines and must not
                // remain queued indefinitely.
                ExpireFactsWithoutGuards(boundaryTime);
                ExpirePairsWithoutGuards(boundaryTime);
                return;
            }

            var snapshots = new List<GuardHearingSnapshot>(captured);
            snapshots.Sort(CompareSnapshots);
            if (snapshots.Count > _maxHearingGuards)
                snapshots.RemoveRange(_maxHearingGuards,
                    snapshots.Count - _maxHearingGuards);

            // Stage newly eligible facts whenever capacity remains. Existing
            // deferred pairs sort ahead of these additions, so this does not let
            // newer work overtake older retained work.
            if (_deferredPairs.Count < _hearingWorkCapacity)
                StageFacts(snapshots, boundaryTime);

            _deferredPairs.Sort(ComparePairs);
            int pairCount = Math.Min(_maxHearingPairs, _deferredPairs.Count);
            var remaining = new List<PairWork>(_deferredPairs.Count - pairCount);
            for (int i = pairCount; i < _deferredPairs.Count; i++)
                remaining.Add(_deferredPairs[i]);

            for (int i = 0; i < pairCount; i++)
            {
                PairWork pair = _deferredPairs[i];
                if (boundaryTime > pair.Deadline + _mathRelativeTolerance
                    || _clock.CurrentTime > pair.Deadline + _mathRelativeTolerance)
                {
                    MarkFactDeadlineMissed(pair.Fact);
                    continue;
                }
                EvaluatePair(pair, boundaryTime);
            }

            _deferredPairs.Clear();
            for (int i = 0; i < remaining.Count; i++)
            {
                if (!_deadlineFailedFacts.Contains(GetFactKey(remaining[i].Fact)))
                    _deferredPairs.Add(remaining[i]);
            }
        }

        private void StageFacts(IList<GuardHearingSnapshot> snapshots,
            float boundaryTime)
        {
            _pendingFacts.Sort(CompareFacts);
            int factCount = Math.Min(_maxHearingFacts, _pendingFacts.Count);
            var retained = new List<PendingFact>(_pendingFacts.Count);
            int stagedCount = 0;

            for (int i = 0; i < _pendingFacts.Count; i++)
            {
                PendingFact pending = _pendingFacts[i];
                if (_deadlineFailedFacts.Contains(GetFactKey(pending.Fact)))
                    continue;
                if (pending.AdmittedAt > boundaryTime + _mathRelativeTolerance
                    || stagedCount >= factCount)
                {
                    retained.Add(pending);
                    continue;
                }

                stagedCount++;
                if (pending.GuardSnapshot == null)
                    pending.GuardSnapshot = snapshots;
                IList<GuardHearingSnapshot> factSnapshots = pending.GuardSnapshot;
                int availableGuards = factSnapshots.Count - pending.NextGuardIndex;
                int guardCount = Math.Min(_maxHearingGuards, availableGuards);
                int capacity = _hearingWorkCapacity - _deferredPairs.Count;
                int toStage = Math.Min(guardCount, capacity);
                for (int j = 0; j < toStage; j++)
                {
                    GuardHearingSnapshot snapshot =
                        factSnapshots[pending.NextGuardIndex + j];
                    _deferredPairs.Add(new PairWork
                    {
                        Fact = pending.Fact,
                        Snapshot = snapshot,
                        Sequence = pending.Sequence,
                        Deadline = pending.Deadline
                    });
                }
                pending.NextGuardIndex += toStage;
                if (pending.NextGuardIndex < factSnapshots.Count)
                    retained.Add(pending);
                // Capacity pressure retains the fact and its cursor. It is
                // deferred work, not a rejection; only a terminal deadline or
                // explicit newest-item overflow is counted as failure.
            }

            _pendingFacts.Clear();
            _pendingFacts.AddRange(retained);
        }

        private void EvaluatePair(PairWork pair, float boundaryTime)
        {
            NoisePublished noise = pair.Fact;
            GuardHearingSnapshot snapshot = pair.Snapshot;
            if (!IsHeard(noise, snapshot)) return;

            bool isNewEntry;
            string entryId = GetOrReserveEntry(snapshot.GuardEid, noise.FactId,
                out isNewEntry);
            var relay = new NoiseHeardRelay(
                noise.SessionId,
                noise.AttemptEpoch,
                noise.FactId,
                entryId,
                snapshot.GuardEid,
                noise.Kind,
                noise.Source,
                noise.Position,
                noise.Radius,
                noise.SourceEventClassRank,
                noise.OriginProvenance,
                noise.AuthoritativeOrigin,
                noise.SourceTimestamp,
                boundaryTime,
                snapshot.ResidualR,
                noise.TerminalPublicationTime);
            QueueRelay(relay, pair.Deadline, isNewEntry);
        }

        private bool IsHeard(NoisePublished noise, GuardHearingSnapshot snapshot)
        {
            Vector3 origin = noise.AuthoritativeOrigin;
            if (noise.SourceEventClassRank == (int)NoiseSourceKind.Movement)
                origin.y += _movementOriginOffset;
            Vector3 eye = snapshot.EyePosition;
            float deltaY = Mathf.Abs(snapshot.FeetPosition.y - origin.y);
            if (deltaY >= _hearingYHardCutoff)
                return false;

            float effectiveRadius = noise.Radius
                * Mathf.Max(0f, 1f - deltaY / _hearingYHardCutoff);
            if (effectiveRadius <= 0f) return false;

            Vector3 delta = origin - eye;
            float planarDistance = new Vector2(delta.x, delta.z).magnitude;
            if (planarDistance > effectiveRadius) return false;
            return _physicsProfile.Linecast(eye, origin).Clear;
        }

        private string GetOrReserveEntry(string guardEid, ulong factId,
            out bool isNewEntry)
        {
            LivenessFact liveness;
            if (_livenessByGuard.TryGetValue(guardEid, out liveness)
                && string.Equals(liveness.Operation, "open", StringComparison.Ordinal))
            {
                isNewEntry = false;
                // Liveness carries the Perception-owned opaque identity. Never
                // translate it through an independent numeric counter.
                return liveness.EntryId;
            }

            string entryId;
            if (_activeEntryByGuard.TryGetValue(guardEid, out entryId))
            {
                isNewEntry = false;
                return entryId;
            }

            ulong entryNumber = _nextEntryId++;
            if (entryNumber == 0)
                throw new InvalidOperationException("perception-entry-id-exhausted");
            entryId = "perception-entry:" + _eventBus.SessionId + ":"
                + _eventBus.AttemptEpoch + ":" + entryNumber.ToString("D");
            _activeEntryByGuard.Add(guardEid, entryId);
            isNewEntry = true;
            return entryId;
        }

        private void OnLivenessFact(LivenessFact fact)
        {
            if (fact == null || !string.Equals(fact.SessionId, _eventBus.SessionId,
                StringComparison.Ordinal) || fact.AttemptEpoch != _eventBus.AttemptEpoch)
                return;
            if (string.IsNullOrWhiteSpace(fact.GuardEid)
                || string.IsNullOrWhiteSpace(fact.EntryId))
                return;

            LivenessFact previous;
            if (_livenessByGuard.TryGetValue(fact.GuardEid, out previous)
                && CompareLiveness(previous, fact) >= 0)
                return;

            _livenessByGuard[fact.GuardEid] = fact;
            if (string.Equals(fact.Operation, "open", StringComparison.Ordinal)
                || string.Equals(fact.Operation, "promote", StringComparison.Ordinal))
            {
                // Promote is an in-place tier change: retain the exact opaque
                // entry identity rather than allocating a second reservation.
                _activeEntryByGuard[fact.GuardEid] = fact.EntryId;
            }
            else if (string.Equals(fact.Operation, "close", StringComparison.Ordinal))
            {
                RemoveEntryIfCurrent(fact.GuardEid, fact.EntryId);
            }
        }

        private void OnNoiseConsumptionOutcome(NoiseConsumptionOutcome outcome)
        {
            // Consumption is an outcome for one relay, not authoritative episode
            // liveness. Only FSM-owned LivenessFact transitions close reservations.
        }

        private bool QueueRelay(NoiseHeardRelay relay, float deadline,
            bool isNewEntry)
        {
            EventPublishResult result = _eventBus.Publish(relay);
            if (result.Admission != EventAdmission.Retry)
            {
                if (result.Admission == EventAdmission.Rejected)
                {
                    RejectRelay(relay, result.Code, result.RetryCount);
                    if (isNewEntry)
                        RemoveEntryIfCurrent(relay.GuardEid, relay.EntryId);
                }
                return result.Admission == EventAdmission.Accepted
                    || result.Admission == EventAdmission.Duplicate;
            }

            if (_pendingRelays.Count >= _relayQueueCapacity)
            {
                RejectRelay(relay, "perception-hearing-relay-queue-full",
                    result.RetryCount);
                if (isNewEntry)
                    RemoveEntryIfCurrent(relay.GuardEid, relay.EntryId);
                return false;
            }

            _pendingRelays.Add(new PendingRelay
            {
                Relay = relay,
                RetryCount = result.RetryCount,
                Deadline = deadline,
                IsNewEntry = isNewEntry
            });
            return true;
        }

        private void RejectRelay(NoiseHeardRelay relay, string code,
            int retryCount = 0)
        {
            RejectedRelayCount++;
            string rejectionCode = string.IsNullOrWhiteSpace(code)
                ? "perception-hearing-queue-overflow-rejected" : code;
            LastRejectionCode = rejectionCode;
            RecordDiagnostic(rejectionCode, relay, retryCount,
                _pendingRelays.Count, _relayQueueCapacity, "relay",
                relay.SourceTimestamp.ToString("R") + "|" + relay.FactId
                    + "|" + relay.GuardEid);
        }

        private void ReleasePendingReservation(PendingRelay pending)
        {
            if (pending != null && pending.IsNewEntry)
                RemoveEntryIfCurrent(pending.Relay.GuardEid,
                    pending.Relay.EntryId);
        }

        private void RecordRejectedFact(string code, NoisePublished fact = null,
            int retryCount = 0)
        {
            RejectedFactCount++;
            string rejectionCode = string.IsNullOrWhiteSpace(code)
                ? "perception-hearing-queue-overflow-rejected" : code;
            LastRejectionCode = rejectionCode;
            if (fact != null)
            {
                RecordDiagnostic(rejectionCode, fact, retryCount,
                    _pendingFacts.Count, _rawFactQueueCapacity, "raw-fact",
                    fact.SourceTimestamp.ToString("R") + "|" + fact.FactId);
            }
        }

        private void RecordBoundaryFailure(float boundaryTime, string code)
        {
            RejectedBoundaryCount++;
            LastRejectedBoundaryTime = boundaryTime;
            string rejectionCode = string.IsNullOrWhiteSpace(code)
                ? "perception-deferred-boundary-overflow-rejected" : code;
            LastRejectionCode = rejectionCode;

            if (_diagnosticSink == null) return;
            IEvent boundaryEvent = new BoundaryDiagnosticEvent(
                _eventBus.SessionId, _eventBus.AttemptEpoch, boundaryTime);
            var envelope = new EventEnvelope(boundaryEvent.SessionId,
                boundaryEvent.AttemptEpoch, boundaryEvent.Timestamp,
                boundaryEvent.Publisher, boundaryEvent.Identity,
                EventBusPhase.Hearing, boundaryEvent);
            RecordDiagnostic(rejectionCode, envelope, 0,
                _deferredBoundaries.Count, _maxDeferredHearingBoundaries,
                "deferred-boundary", boundaryTime.ToString("R"));
        }

        private void RecordDiagnostic(string code, IEvent evt, int retryCount,
            int queueCapacity)
        {
            if (evt == null) return;
            EventBusPhase phase = evt is IEventPhase phased
                ? phased.Phase : EventBusPhase.Hearing;
            var envelope = new EventEnvelope(evt.SessionId, evt.AttemptEpoch,
                evt.Timestamp, evt.Publisher, evt.Identity, phase, evt);
            string queueName = evt is NoiseHeardRelay ? "relay" : "raw-fact";
            string orderingKey = evt.Timestamp.ToString("R") + "|"
                + evt.Identity;
            RecordDiagnostic(code, envelope, retryCount,
                queueName == "relay" ? _pendingRelays.Count : _pendingFacts.Count,
                queueCapacity, queueName, orderingKey);
        }

        private void RecordDiagnostic(string code, EventEnvelope envelope,
            int retryCount, int queueDepth, int queueCapacity,
            string queueName, string orderingKey)
        {
            if (_diagnosticSink == null) return;
            IEventDiagnosticDetailSink detailed =
                _diagnosticSink as IEventDiagnosticDetailSink;
            if (detailed != null)
            {
                detailed.RecordDetailed(code, envelope, retryCount, queueDepth,
                    queueCapacity, queueName, orderingKey);
                return;
            }
            _diagnosticSink.Record(code, envelope, retryCount, queueDepth,
                queueCapacity);
        }

        private void ExpireFactsWithoutGuards(float boundaryTime)
        {
            var expired = new List<NoisePublished>();
            for (int i = 0; i < _pendingFacts.Count; i++)
            {
                PendingFact pending = _pendingFacts[i];
                if (boundaryTime > pending.Deadline + _mathRelativeTolerance)
                    expired.Add(pending.Fact);
            }
            for (int i = 0; i < expired.Count; i++)
                MarkFactDeadlineMissed(expired[i]);
        }

        private void MarkFactDeadlineMissed(NoisePublished fact)
        {
            EventIdentityKey key = GetFactKey(fact);
            if (_deadlineFailedFacts.Add(key))
                RecordRejectedFact("perception-hearing-deadline-missed", fact);

            for (int i = _pendingFacts.Count - 1; i >= 0; i--)
            {
                if (GetFactKey(_pendingFacts[i].Fact).Equals(key))
                    _pendingFacts.RemoveAt(i);
            }
            for (int i = _deferredPairs.Count - 1; i >= 0; i--)
            {
                if (GetFactKey(_deferredPairs[i].Fact).Equals(key))
                    _deferredPairs.RemoveAt(i);
            }
            for (int i = _pendingRelays.Count - 1; i >= 0; i--)
            {
                PendingRelay pending = _pendingRelays[i];
                if (!GetFactKey(pending.Relay).Equals(key)) continue;
                RejectRelay(pending.Relay,
                    "perception-hearing-deadline-missed", pending.RetryCount);
                ReleasePendingReservation(pending);
                _pendingRelays.RemoveAt(i);
            }
        }

        private void ExpirePairsWithoutGuards(float boundaryTime)
        {
            var expired = new List<NoisePublished>();
            for (int i = 0; i < _deferredPairs.Count; i++)
            {
                PairWork pair = _deferredPairs[i];
                if (boundaryTime > pair.Deadline + _mathRelativeTolerance)
                    expired.Add(pair.Fact);
            }
            for (int i = 0; i < expired.Count; i++)
                MarkFactDeadlineMissed(expired[i]);
        }

        private void RemoveEntryIfCurrent(string guardEid, string entryId)
        {
            string activeEntry;
            if (_activeEntryByGuard.TryGetValue(guardEid, out activeEntry)
                && string.Equals(activeEntry, entryId, StringComparison.Ordinal))
                _activeEntryByGuard.Remove(guardEid);
        }

        private EventIdentityKey GetFactKey(NoisePublished fact)
        {
            return new EventIdentityKey(fact.SessionId, fact.AttemptEpoch,
                fact.FactId.ToString("D"));
        }

        private EventIdentityKey GetFactKey(NoiseHeardRelay relay)
        {
            return new EventIdentityKey(relay.SessionId, relay.AttemptEpoch,
                relay.FactId.ToString("D"));
        }

        private float GetNextBoundary(float currentTime)
        {
            if (!IsFinite(currentTime) || currentTime <= 0f)
                return _hearingInterval;

            float ratio = currentTime / _hearingInterval;
            float nearestBoundary = Mathf.Round(ratio);
            if (Mathf.Abs(ratio - nearestBoundary)
                <= _mathRelativeTolerance / _hearingInterval)
                return nearestBoundary * _hearingInterval;
            return Mathf.Ceil(ratio) * _hearingInterval;
        }

        private static int CompareLiveness(LivenessFact left, LivenessFact right)
        {
            int compare = left.EvaluatedAt.CompareTo(right.EvaluatedAt);
            if (compare != 0) return compare;
            compare = left.SourceTimestamp.CompareTo(right.SourceTimestamp);
            if (compare != 0) return compare;
            return string.Compare(left.Identity, right.Identity,
                StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int CompareFacts(PendingFact left, PendingFact right)
        {
            int compare = left.Fact.SourceTimestamp.CompareTo(right.Fact.SourceTimestamp);
            if (compare != 0) return compare;
            compare = left.Fact.SourceEventClassRank.CompareTo(right.Fact.SourceEventClassRank);
            if (compare != 0) return compare;
            compare = CompareSourceEventIds(left.Fact.SourceEventId,
                right.Fact.SourceEventId);
            if (compare != 0) return compare;
            compare = left.Fact.FactId.CompareTo(right.Fact.FactId);
            return compare != 0 ? compare : left.Sequence.CompareTo(right.Sequence);
        }

        private static int ComparePairs(PairWork left, PairWork right)
        {
            int compare = left.Fact.SourceTimestamp.CompareTo(
                right.Fact.SourceTimestamp);
            if (compare != 0) return compare;
            compare = left.Fact.SourceEventClassRank.CompareTo(
                right.Fact.SourceEventClassRank);
            if (compare != 0) return compare;
            compare = CompareSourceEventIds(left.Fact.SourceEventId,
                right.Fact.SourceEventId);
            if (compare != 0) return compare;
            compare = left.Fact.FactId.CompareTo(right.Fact.FactId);
            if (compare != 0) return compare;
            compare = string.Compare(left.Snapshot.GuardEid,
                right.Snapshot.GuardEid, StringComparison.Ordinal);
            return compare != 0 ? compare : left.Sequence.CompareTo(right.Sequence);
        }

        /// <summary>
        /// Same-timestamp source-event identity comparison. The registered
        /// admission keys order by the numeric step_id / flight_handle_id
        /// source ids; a plain string compare would order "10" before "9"
        /// and disagree with the emitter's allocation order. Matches
        /// NoiseEmitter.CompareSourceEventIds.
        /// </summary>
        private static int CompareSourceEventIds(string left, string right)
        {
            ulong leftNumber;
            ulong rightNumber;
            bool leftIsNumeric = ulong.TryParse(left, NumberStyles.None,
                CultureInfo.InvariantCulture, out leftNumber);
            bool rightIsNumeric = ulong.TryParse(right, NumberStyles.None,
                CultureInfo.InvariantCulture, out rightNumber);
            if (leftIsNumeric && rightIsNumeric)
                return leftNumber.CompareTo(rightNumber);
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private static int CompareSnapshots(GuardHearingSnapshot left,
            GuardHearingSnapshot right)
        {
            return string.Compare(left.GuardEid, right.GuardEid,
                StringComparison.Ordinal);
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
