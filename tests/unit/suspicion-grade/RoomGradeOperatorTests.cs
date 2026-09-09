using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies deterministic room-grade finalization without Unity runtime state.
    /// </summary>
    public sealed class RoomGradeOperatorTests
    {
        [TestCase(0f, 0f, 100f, RoomGrade.S)]
        [TestCase(100f, 10f, 90f, RoomGrade.A)]
        [TestCase(150f, 10f, 90f, RoomGrade.A)]
        public void test_room_grade_finalize_maps_clean_completed_room_and_clamps_residual(
            float residual, float expectedPenalty, float expectedScore,
            RoomGrade expectedGrade)
        {
            // Arrange
            RoomCompletionBoundary boundary = CompletedBoundary(residual);
            GradeTraceAggregate aggregate = EmptyAggregate();
            SuspicionGradeConfiguration configuration = StarterConfiguration();

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, configuration);

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Completed));
            Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(result.ResidualPenalty, Is.EqualTo(expectedPenalty));
            Assert.That(result.QualityScore, Is.EqualTo(expectedScore));
            Assert.That(result.Grade, Is.EqualTo(expectedGrade));
            Assert.That(result.HasResidualPenalty, Is.True);
        }

        [Test]
        public void test_room_grade_finalize_applies_incident_weights_and_cap()
        {
            // Arrange
            GradeTraceAggregate aggregate = Aggregate(4, 2, 0);
            RoomCompletionBoundary boundary = CompletedBoundary(0f);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.IncidentPenalty, Is.EqualTo(90f));
            Assert.That(result.QualityScore, Is.EqualTo(10f));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.NeedsImprovement));
            Assert.That(result.PerGuardBreakdown, Has.Count.EqualTo(1));
            Assert.That(result.PerGuardBreakdown[0].IncidentPenalty,
                Is.EqualTo(90f));
        }

        [Test]
        public void test_room_grade_finalize_preserves_failed_capture_diagnostics_without_grade()
        {
            // Arrange
            GradeTraceAggregate aggregate = Aggregate(0, 0, 1);
            RoomCompletionBoundary boundary = CaptureBoundary();

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Failed));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(result.IncidentPenalty, Is.EqualTo(50f));
            Assert.That(result.HasResidualPenalty, Is.False);
            Assert.That(result.PerGuardBreakdown, Has.Count.EqualTo(1));
            Assert.That(result.PerGuardBreakdown[0].CaptureCount,
                Is.EqualTo(1));
            Assert.That(result.PerGuardBreakdown[0].IncidentPenalty,
                Is.EqualTo(50f));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "event-1" }));
        }

        [Test]
        public void test_room_grade_finalize_capture_trace_forces_failed_even_when_boundary_completed()
        {
            // Arrange
            GradeTraceAggregate aggregate = Aggregate(0, 0, 1);
            RoomCompletionBoundary boundary = CompletedBoundary(0f);
            SuspicionGradeConfiguration configuration =
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightCapture: 0f);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, configuration);

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Failed));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(result.PerGuardBreakdown[0].CaptureCount,
                Is.EqualTo(1));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "event-1" }));
            Assert.That(result.HasResidualPenalty, Is.False);
        }

        [Test]
        public void test_room_grade_finalize_non_capture_incomplete_completion_is_failed()
        {
            // Arrange
            RoomCompletionBoundary boundary = Boundary(
                false, false, true, false,
                new Dictionary<string, float>(),
                new Dictionary<string, float>());

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Failed));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(result.HasResidualPenalty, Is.False);
        }

        [Test]
        public void test_room_grade_finalize_capture_precedes_missing_configuration()
        {
            // Arrange
            RoomCompletionBoundary boundary = CaptureBoundary();
            GradeTraceAggregate aggregate = Aggregate(0, 0, 1);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, null);

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Failed));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(result.GradeVersion, Is.EqualTo(0));
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "event-1" }));
            Assert.That(result.PerGuardBreakdown, Has.Count.EqualTo(1));
            Assert.That(result.PerGuardBreakdown[0].CaptureCount,
                Is.EqualTo(1));
            Assert.That(result.HasResidualPenalty, Is.False);
        }

        [Test]
        public void test_room_grade_finalize_marks_incomplete_evidence_unresolved()
        {
            // Arrange
            SuspicionGradeConfiguration configuration = StarterConfiguration();

            // Act
            RoomGradeFinalized missingTrace = RoomGradeOperator.FinalizeRoom(
                IncompleteTraceBoundary(), EmptyAggregate(), configuration);
            RoomGradeFinalized missingResidual = RoomGradeOperator.FinalizeRoom(
                MissingResidualBoundary(), EmptyAggregate(), configuration);

            // Assert
            Assert.That(missingTrace.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(missingResidual.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(missingTrace.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(missingResidual.Grade, Is.EqualTo(RoomGrade.None));
            Assert.That(missingTrace.HasResidualPenalty, Is.False);
            Assert.That(missingResidual.HasResidualPenalty, Is.False);
        }

        [TestCase(90f, RoomGrade.S)]
        [TestCase(75f, RoomGrade.A)]
        [TestCase(60f, RoomGrade.B)]
        [TestCase(59f, RoomGrade.NeedsImprovement)]
        public void test_room_grade_finalize_uses_exact_grade_boundaries(
            float score, RoomGrade expectedGrade)
        {
            // Arrange
            SuspicionGradeConfiguration configuration =
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightChase: 1f,
                    gradeWeightFruitless: 0f, gradeWeightCapture: 0f,
                    gradeWeightResidual: 0f);
            GradeTraceAggregate aggregate = Aggregate(
                chaseEntries: (int)(100f - score),
                fruitlessResolutions: 0, captures: 0);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, configuration);

            // Assert
            Assert.That(result.QualityScore, Is.EqualTo(score));
            Assert.That(result.Grade, Is.EqualTo(expectedGrade));
        }

        [Test]
        public void test_room_grade_finalize_uses_weighted_components_and_quality_for_multiple_guards()
        {
            // Arrange
            var perGuard = new List<GuardGradeBreakdown>
            {
                new GuardGradeBreakdown("guard-a", 1, 0, 0),
                new GuardGradeBreakdown("guard-b", 0, 1, 0)
            };
            var aggregate = new GradeTraceAggregate("session", 1, "room",
                1, 1, 0, perGuard,
                new[] { "event-b", "event-a" });
            var boundary = Boundary(
                isCapture: false, isCompletedRoom: true,
                hasMandatoryTrace: true, hasFinalResidualSnapshot: true,
                new Dictionary<string, float>
                {
                    { "guard-a", 0f }, { "guard-b", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-a", 1f }, { "guard-b", 3f }
                });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.IncidentPenalty, Is.EqualTo(13.75f));
            Assert.That(result.ResidualPenalty, Is.EqualTo(0f));
            Assert.That(result.QualityScore, Is.EqualTo(86.25f));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.A));
            Assert.That(result.PerGuardBreakdown[0].GuardEid,
                Is.EqualTo("guard-a"));
            Assert.That(result.PerGuardBreakdown[0].IncidentPenalty,
                Is.EqualTo(25f));
            Assert.That(result.PerGuardBreakdown[0].QualityScore,
                Is.EqualTo(75f));
            Assert.That(result.PerGuardBreakdown[1].GuardEid,
                Is.EqualTo("guard-b"));
            Assert.That(result.PerGuardBreakdown[1].IncidentPenalty,
                Is.EqualTo(10f));
            Assert.That(result.PerGuardBreakdown[1].QualityScore,
                Is.EqualTo(90f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void test_room_grade_finalize_rejects_invalid_multi_guard_exposure(
            float invalidExposure)
        {
            // Arrange
            var aggregate = MultiGuardEmptyAggregate();
            var boundary = Boundary(
                isCapture: false, isCompletedRoom: true,
                hasMandatoryTrace: true, hasFinalResidualSnapshot: true,
                new Dictionary<string, float>
                {
                    { "guard-a", 0f }, { "guard-b", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-a", 1f }, { "guard-b", invalidExposure }
                });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void test_room_grade_finalize_rejects_omitted_multi_guard_exposure(
            bool emptyExposureMap)
        {
            // Arrange
            var exposures = emptyExposureMap
                ? new Dictionary<string, float>()
                : new Dictionary<string, float> { { "guard-a", 1f } };
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-a", 0f }, { "guard-b", 0f }
                }, exposures);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, MultiGuardEmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void test_room_grade_finalize_rejects_non_finite_multi_guard_exposure(
            float exposure)
        {
            // Arrange
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-a", 0f }, { "guard-b", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-a", 1f }, { "guard-b", exposure }
                });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, MultiGuardEmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
        }

        [Test]
        public void test_room_grade_finalize_accepts_implicit_one_guard_exposure_only_for_one_guard()
        {
            // Arrange
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float>());

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Completed));
            Assert.That(result.PerGuardBreakdown[0].ExposureWeight,
                Is.EqualTo(1f));
        }

        [Test]
        public void test_room_grade_finalize_accepts_max_finite_single_guard_exposure()
        {
            // Arrange
            var boundary = Boundary(
                isCapture: false, isCompletedRoom: true,
                hasMandatoryTrace: true, hasFinalResidualSnapshot: true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float>
                {
                    { "guard-1", float.MaxValue }
                });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Completed));
            Assert.That(result.QualityScore, Is.EqualTo(100f));
        }

        [Test]
        public void test_room_grade_finalize_rejects_count_sum_mismatch_without_overflow()
        {
            // Arrange
            var aggregate = new GradeTraceAggregate("session", 1, "room",
                int.MaxValue, 0, 0,
                new[]
                {
                    new GuardGradeBreakdown("guard-a", int.MaxValue, 0, 0),
                    new GuardGradeBreakdown("guard-b", int.MaxValue, 0, 0)
                }, new string[0]);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                Boundary(
                    false, true, true, true,
                    new Dictionary<string, float>
                    {
                        { "guard-a", 0f }, { "guard-b", 0f }
                    },
                    new Dictionary<string, float>
                    {
                        { "guard-a", 1f }, { "guard-b", 1f }
                    }), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
        }

        [Test]
        public void test_room_grade_finalize_rejects_non_finite_residual_and_orphan_exposure()
        {
            // Arrange
            RoomCompletionBoundary nonFiniteResidual = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-1", float.NaN }
                },
                new Dictionary<string, float>
                {
                    { "guard-1", 1f }
                });
            RoomCompletionBoundary orphanExposure = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-1", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-1", 1f }, { "orphan", 1f }
                });
            RoomCompletionBoundary nullResidualKey = Boundary(
                false, true, true, true,
                new NullKeyReadOnlyDictionary(),
                new Dictionary<string, float>());

            // Act
            RoomGradeFinalized residualResult = RoomGradeOperator.FinalizeRoom(
                nonFiniteResidual, EmptyAggregate(), StarterConfiguration());
            RoomGradeFinalized exposureResult = RoomGradeOperator.FinalizeRoom(
                orphanExposure, EmptyAggregate(), StarterConfiguration());
            RoomGradeFinalized nullKeyResult = RoomGradeOperator.FinalizeRoom(
                nullResidualKey, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(residualResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(exposureResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(nullKeyResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
        }

        [Test]
        public void test_room_grade_finalize_fails_closed_for_invalid_boundary_and_aggregate_scope()
        {
            // Arrange
            RoomCompletionBoundary blankBoundary = new RoomCompletionBoundary(
                "", 1, "room", false, true, true, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float> { { "guard-1", 1f } });
            RoomCompletionBoundary negativeBoundary = new RoomCompletionBoundary(
                "session", -1, "room", false, true, true, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float> { { "guard-1", 1f } });
            RoomCompletionBoundary invalidCaptureBoundary = new RoomCompletionBoundary(
                "", 1, "room", true, false, false, false,
                new Dictionary<string, float>(),
                new Dictionary<string, float>());
            RoomCompletionBoundary invalidFailedBoundary = new RoomCompletionBoundary(
                "session", -1, "room", false, false, false, false,
                new Dictionary<string, float>(),
                new Dictionary<string, float>());
            GradeTraceAggregate blankAggregate = new GradeTraceAggregate(
                "", 1, "room", 0, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 0) },
                new[] { "bad-event" });
            GradeTraceAggregate mismatchedAggregate = new GradeTraceAggregate(
                "other-session", 1, "room", 0, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 0) },
                new[] { "other-event" });

            // Act
            RoomGradeFinalized blankResult = RoomGradeOperator.FinalizeRoom(
                blankBoundary, EmptyAggregate(), StarterConfiguration());
            RoomGradeFinalized negativeResult = RoomGradeOperator.FinalizeRoom(
                negativeBoundary, EmptyAggregate(), StarterConfiguration());
            RoomGradeFinalized invalidCaptureResult = RoomGradeOperator.FinalizeRoom(
                invalidCaptureBoundary, Aggregate(0, 0, 1), null);
            RoomGradeFinalized invalidFailedResult = RoomGradeOperator.FinalizeRoom(
                invalidFailedBoundary, EmptyAggregate(), null);
            RoomGradeFinalized aggregateResult = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), blankAggregate, StarterConfiguration());
            RoomGradeFinalized mismatchResult = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), mismatchedAggregate, StarterConfiguration());

            // Assert
            Assert.That(blankResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(negativeResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(invalidCaptureResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(invalidFailedResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(aggregateResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(mismatchResult.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(aggregateResult.ContributingEventIds,
                Has.Count.EqualTo(0));
            Assert.That(mismatchResult.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_invalid_contributing_ids_with_incidents()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 1, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 1, 0, 0) },
                new[] { "", null });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_failed_mismatch_does_not_copy_foreign_event_ids()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "other-session", 1, "room", 0, 0, 1,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 1) },
                new[] { "foreign-event" });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CaptureBoundary(), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Failed));
            Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
            Assert.That(result.PerGuardBreakdown, Has.Count.EqualTo(0));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_unattributed_capture_on_completed_boundary()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 0, 0, 1,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 1) },
                new string[0]);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, ZeroCaptureConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_incidents_with_empty_attribution()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 1, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 1, 0, 0) },
                new string[0]);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_clean_aggregate_with_phantom_attribution()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 0, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 0) },
                new[] { "phantom-event" });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_clean_aggregate_with_blank_attribution()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 0, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 0, 0, 0) },
                new[] { "", null });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_duplicate_attribution_ids()
        {
            // Arrange
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 2, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 2, 0, 0) },
                new[] { "duplicate-event", "duplicate-event" });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_dictionary_count_mismatch_without_throwing()
        {
            // Arrange
            var hostileResiduals = new CountMismatchReadOnlyDictionary(
                new Dictionary<string, float> { { "guard-1", 0f } });
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true, hostileResiduals,
                new Dictionary<string, float> { { "guard-1", 1f } });

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
            Assert.That(result.ContributingEventIds,
                Has.Count.EqualTo(0));
        }

        [Test]
        public void test_room_grade_finalize_rejects_dictionary_count_throw_without_throwing()
        {
            // Arrange
            var hostileExposure = new ThrowingCountReadOnlyDictionary(
                new Dictionary<string, float> { { "guard-1", 1f } });
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                hostileExposure);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, EmptyAggregate(), StarterConfiguration());

            // Assert
            Assert.That(result.CompletionStatus,
                Is.EqualTo(GradeCompletionStatus.Unresolved));
        }

        [Test]
        public void test_room_grade_finalize_returns_single_guard_score_directly()
        {
            // Arrange
            GradeTraceAggregate aggregate = Aggregate(1, 0, 0);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                CompletedBoundary(0f), aggregate, StarterConfiguration());

            // Assert
            Assert.That(result.QualityScore, Is.EqualTo(75f));
            Assert.That(result.PerGuardBreakdown[0].ExposureWeight,
                Is.EqualTo(1f));
        }

        [Test]
        public void test_room_grade_finalize_is_invariant_to_input_insertion_order()
        {
            // Arrange
            var forwardAggregate = new GradeTraceAggregate("session", 1, "room",
                1, 1, 0,
                new[]
                {
                    new GuardGradeBreakdown("guard-b", 0, 1, 0),
                    new GuardGradeBreakdown("guard-a", 1, 0, 0)
                }, new[] { "event-z", "event-a" });
            var reverseAggregate = new GradeTraceAggregate("session", 1, "room",
                1, 1, 0,
                new[]
                {
                    new GuardGradeBreakdown("guard-a", 1, 0, 0),
                    new GuardGradeBreakdown("guard-b", 0, 1, 0)
                }, new[] { "event-a", "event-z" });
            RoomCompletionBoundary forwardBoundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-b", 0f }, { "guard-a", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-b", 3f }, { "guard-a", 1f }
                });
            RoomCompletionBoundary reverseBoundary = Boundary(
                false, true, true, true,
                new Dictionary<string, float>
                {
                    { "guard-a", 0f }, { "guard-b", 0f }
                },
                new Dictionary<string, float>
                {
                    { "guard-a", 1f }, { "guard-b", 3f }
                });

            // Act
            RoomGradeFinalized forward = RoomGradeOperator.FinalizeRoom(
                forwardBoundary, forwardAggregate, StarterConfiguration());
            RoomGradeFinalized reverse = RoomGradeOperator.FinalizeRoom(
                reverseBoundary, reverseAggregate, StarterConfiguration());

            // Assert
            AssertEquivalent(forward, reverse);
        }

        [Test]
        public void test_room_grade_finalize_output_collections_are_read_only_after_input_mutation()
        {
            // Arrange
            var sourceIds = new List<string> { "event-z", "event-a" };
            var sourceResiduals = new Dictionary<string, float>
            {
                { "guard-1", 0f }
            };
            var sourceExposure = new Dictionary<string, float>
            {
                { "guard-1", 1f }
            };
            RoomCompletionBoundary boundary = Boundary(
                false, true, true, true, sourceResiduals, sourceExposure);
            GradeTraceAggregate aggregate = new GradeTraceAggregate(
                "session", 1, "room", 2, 0, 0,
                new[] { new GuardGradeBreakdown("guard-1", 2, 0, 0) },
                sourceIds);

            // Act
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, StarterConfiguration());
            sourceIds[0] = "mutated";
            sourceResiduals["guard-1"] = 100f;
            sourceExposure["guard-1"] = 2f;
            IList<string> outputIds = (IList<string>)result.ContributingEventIds;
            bool mutationRejected = false;
            try
            {
                outputIds[0] = "mutated-output";
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }

            // Assert
            Assert.That(result.ContributingEventIds,
                Is.EqualTo(new[] { "event-a", "event-z" }));
            Assert.That(result.ResidualPenalty, Is.EqualTo(0f));
            Assert.That(result.PerGuardBreakdown[0].ExposureWeight,
                Is.EqualTo(1f));
            Assert.That(mutationRejected, Is.True);
        }

        private static SuspicionGradeConfiguration StarterConfiguration()
        {
            return SuspicionGradeTestFixtures.CreateStarterConfiguration();
        }

        private static SuspicionGradeConfiguration ZeroCaptureConfiguration()
        {
            return SuspicionGradeTestFixtures.CreateConfiguration(
                90f, 75f, 60f, gradeWeightCapture: 0f);
        }

        private static GradeTraceAggregate EmptyAggregate()
        {
            return Aggregate(0, 0, 0);
        }

        private static GradeTraceAggregate MultiGuardEmptyAggregate()
        {
            return new GradeTraceAggregate("session", 1, "room", 0, 0, 0,
                new[]
                {
                    new GuardGradeBreakdown("guard-a", 0, 0, 0),
                    new GuardGradeBreakdown("guard-b", 0, 0, 0)
                }, new string[0]);
        }

        private static GradeTraceAggregate Aggregate(
            int chaseEntries, int fruitlessResolutions, int captures)
        {
            int incidentCount = checked(chaseEntries + fruitlessResolutions
                + captures);
            var eventIds = new List<string>(incidentCount);
            for (int i = 0; i < incidentCount; i++)
                eventIds.Add("event-" + (i + 1));

            return new GradeTraceAggregate("session", 1, "room",
                chaseEntries, fruitlessResolutions, captures,
                new[] { new GuardGradeBreakdown("guard-1", chaseEntries,
                    fruitlessResolutions, captures) }, eventIds);
        }

        private static RoomCompletionBoundary CompletedBoundary(float residual)
        {
            return Boundary(
                false, true, true, true,
                new Dictionary<string, float> { { "guard-1", residual } },
                new Dictionary<string, float> { { "guard-1", 1f } });
        }

        private static RoomCompletionBoundary CaptureBoundary()
        {
            return Boundary(
                true, false, true, false,
                new Dictionary<string, float>(),
                new Dictionary<string, float>());
        }

        private static RoomCompletionBoundary IncompleteTraceBoundary()
        {
            return Boundary(
                false, true, false, true,
                new Dictionary<string, float> { { "guard-1", 0f } },
                new Dictionary<string, float> { { "guard-1", 1f } });
        }

        private static RoomCompletionBoundary MissingResidualBoundary()
        {
            return Boundary(
                false, true, true, false,
                new Dictionary<string, float>(),
                new Dictionary<string, float> { { "guard-1", 1f } });
        }

        private static RoomCompletionBoundary Boundary(
            bool isCapture, bool isCompletedRoom, bool hasMandatoryTrace,
            bool hasFinalResidualSnapshot,
            IReadOnlyDictionary<string, float> residuals,
            IReadOnlyDictionary<string, float> exposureWeights)
        {
            return new RoomCompletionBoundary(
                "session", 1, "room", isCapture, isCompletedRoom,
                hasMandatoryTrace, hasFinalResidualSnapshot,
                residuals, exposureWeights);
        }

        private static void AssertEquivalent(
            RoomGradeFinalized expected, RoomGradeFinalized actual)
        {
            Assert.That(actual.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(actual.AttemptEpoch, Is.EqualTo(expected.AttemptEpoch));
            Assert.That(actual.RoomId, Is.EqualTo(expected.RoomId));
            Assert.That(actual.GradeVersion, Is.EqualTo(expected.GradeVersion));
            Assert.That(actual.CompletionStatus,
                Is.EqualTo(expected.CompletionStatus));
            Assert.That(actual.IncidentPenalty,
                Is.EqualTo(expected.IncidentPenalty));
            Assert.That(actual.ResidualPenalty,
                Is.EqualTo(expected.ResidualPenalty));
            Assert.That(actual.HasResidualPenalty,
                Is.EqualTo(expected.HasResidualPenalty));
            Assert.That(actual.QualityScore, Is.EqualTo(expected.QualityScore));
            Assert.That(actual.Grade, Is.EqualTo(expected.Grade));
            Assert.That(actual.ContributingEventIds,
                Is.EqualTo(expected.ContributingEventIds));
            Assert.That(actual.PerGuardBreakdown,
                Is.EqualTo(expected.PerGuardBreakdown));
        }

        private sealed class NullKeyReadOnlyDictionary
            : IReadOnlyDictionary<string, float>
        {
            public int Count { get { return 1; } }
            public IEnumerable<string> Keys
            {
                get { return new string[] { null }; }
            }
            public IEnumerable<float> Values
            {
                get { return new[] { 0f }; }
            }
            public float this[string key]
            {
                get { return 0f; }
            }

            public bool ContainsKey(string key)
            {
                return key == null;
            }

            public bool TryGetValue(string key, out float value)
            {
                value = 0f;
                return key == null;
            }

            public IEnumerator<KeyValuePair<string, float>> GetEnumerator()
            {
                yield return new KeyValuePair<string, float>(null, 0f);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class CountMismatchReadOnlyDictionary
            : IReadOnlyDictionary<string, float>
        {
            private readonly Dictionary<string, float> _values;

            public CountMismatchReadOnlyDictionary(
                Dictionary<string, float> values)
            {
                _values = values;
            }

            public int Count { get { return _values.Count + 1; } }
            public IEnumerable<string> Keys { get { return _values.Keys; } }
            public IEnumerable<float> Values { get { return _values.Values; } }
            public float this[string key] { get { return _values[key]; } }

            public bool ContainsKey(string key)
            {
                return _values.ContainsKey(key);
            }

            public bool TryGetValue(string key, out float value)
            {
                return _values.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<string, float>> GetEnumerator()
            {
                return _values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class ThrowingCountReadOnlyDictionary
            : IReadOnlyDictionary<string, float>
        {
            private readonly Dictionary<string, float> _values;

            public ThrowingCountReadOnlyDictionary(
                Dictionary<string, float> values)
            {
                _values = values;
            }

            public int Count
            {
                get { throw new InvalidOperationException("hostile count"); }
            }

            public IEnumerable<string> Keys { get { return _values.Keys; } }
            public IEnumerable<float> Values { get { return _values.Values; } }
            public float this[string key] { get { return _values[key]; } }

            public bool ContainsKey(string key)
            {
                return _values.ContainsKey(key);
            }

            public bool TryGetValue(string key, out float value)
            {
                return _values.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<string, float>> GetEnumerator()
            {
                return _values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
