using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Environment;

namespace WhisperWard.Tests.Unit.Gameplay
{
    /// <summary>
    /// Unit test suite for Hide Spot Sanctuary and Witnessed Trap interaction volume.
    /// Strictly verifies acceptance criteria for Story HIDE-01 (AC-HIDE-01..04, GDD #5 AC2, AC3, AC14, AC16).
    /// </summary>
    [TestFixture]
    public sealed class HideSpotInteractionTests
    {
        private GameObject _spotGo;
        private HideSpot _hideSpot;
        private GameObject _playerGo;

        [SetUp]
        public void SetUp()
        {
            _spotGo = new GameObject("TestHideSpot");
            _hideSpot = _spotGo.AddComponent<HideSpot>();
            _hideSpot.Configure(
                spotId: "locker_test_01",
                interiorPosition: new Vector3(10f, 0f, 10f),
                frontAnchor: new Vector3(10f, 0f, 9f),
                holdVector: new Vector3(0f, 0f, -1f),
                dwellVerifyTime: 1.50f,
                guardRadius: 0.40f);

            _playerGo = new GameObject("TestPlayer");
            _playerGo.transform.position = new Vector3(10f, 0f, 8f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_playerGo);
            }

            if (_spotGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_spotGo);
            }
        }

        [Test]
        public void test_hidespot_initial_state_is_empty_and_unoccupied()
        {
            // Assert
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));
            Assert.That(_hideSpot.CurrentOccupant, Is.Null);
            Assert.That(_hideSpot.IsWitnessedEntry, Is.False);
            Assert.That(_hideSpot.DwellTimer, Is.EqualTo(0f));
        }

        [Test]
        public void test_hidespot_configure_initializes_geometry_and_parameters()
        {
            // Arrange & Act
            Vector3 interior = new Vector3(5f, 0f, 5f);
            Vector3 front = new Vector3(5f, 0f, 4f);
            Vector3 holdVec = new Vector3(0f, 0f, -1f);
            _hideSpot.Configure("vent_01", interior, front, holdVec, 1.5f, 0.4f);

            // Assert
            Assert.That(_hideSpot.SpotId, Is.EqualTo("vent_01"));
            Assert.That(_hideSpot.InteriorPosition, Is.EqualTo(interior));
            Assert.That(_hideSpot.FrontAnchor, Is.EqualTo(front));
            Assert.That(_hideSpot.HoldVector, Is.EqualTo(holdVec));
            Assert.That(_hideSpot.GuardHoldPosition, Is.EqualTo(front + holdVec * 0.40f));
        }

        [Test]
        public void test_hidespot_standoff_distance_formula_within_catch_range()
        {
            // Arrange (GDD Formula D1): EuclidXZ(guard_hold, interior_position) <= 5.10m <= 5.50m
            bool resolvable = _hideSpot.ValidateResolvability(out float planarDistance);

            // Assert: Default test setup has interior at (10, 0, 10), front at (10, 0, 9), hold at (10, 0, 8.6)
            // Distance = 1.40m <= 5.10m
            Assert.That(resolvable, Is.True);
            Assert.That(planarDistance, Is.LessThanOrEqualTo(5.10f));
            Assert.That(planarDistance, Is.EqualTo(1.40f).Within(1e-3f));
        }

        [Test]
        public void test_hidespot_entry_sets_occupied_and_locks_translation()
        {
            // Arrange (AC-HIDE-01)
            bool eventFired = false;
            _hideSpot.OnOccupied += (spot, occ) => eventFired = true;

            // Act: Unwitnessed entry
            bool success = _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.0f);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Occupied));
            Assert.That(_hideSpot.CurrentOccupant, Is.EqualTo(_playerGo.transform));
            Assert.That(_hideSpot.IsWitnessedEntry, Is.False);
            Assert.That(_playerGo.transform.position, Is.EqualTo(_hideSpot.InteriorPosition));
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void test_hidespot_double_entry_fails_when_already_occupied()
        {
            // Arrange: First player enters
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.0f);

            var secondPlayer = new GameObject("SecondPlayer");
            try
            {
                // Act: Second player tries to enter
                bool success = _hideSpot.TryEnter(secondPlayer.transform, isWitnessed: false, currentTime: 2.0f);

                // Assert: Rejected
                Assert.That(success, Is.False);
                Assert.That(_hideSpot.CurrentOccupant, Is.EqualTo(_playerGo.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondPlayer);
            }
        }

        [Test]
        public void test_hidespot_exit_restores_empty_state_and_positions_at_front_anchor()
        {
            // Arrange (AC-HIDE-04): Enter first
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.0f);
            bool vacatedFired = false;
            _hideSpot.OnVacated += (spot, occ) => vacatedFired = true;

            // Act: Exit after hysteresis interval (1.0s + 0.1s = 1.1s > 0.06s)
            bool exitSuccess = _hideSpot.TryExit(out Vector3 exitPos, currentTime: 1.10f);

            // Assert
            Assert.That(exitSuccess, Is.True);
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));
            Assert.That(_hideSpot.CurrentOccupant, Is.Null);
            Assert.That(exitPos, Is.EqualTo(_hideSpot.FrontAnchor));
            Assert.That(_playerGo.transform.position, Is.EqualTo(_hideSpot.FrontAnchor));
            Assert.That(vacatedFired, Is.True);
        }

        [Test]
        public void test_hidespot_exit_when_empty_fails_gracefully()
        {
            // Act
            bool exitSuccess = _hideSpot.TryExit(out Vector3 exitPos, currentTime: 1.0f);

            // Assert
            Assert.That(exitSuccess, Is.False);
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));
        }

        [Test]
        public void test_hidespot_boundary_hysteresis_prevents_flapping_within_60ms()
        {
            // Arrange (AC-HIDE-04 & GDD #5 AC14): Hysteresis window is 0.06s (60ms)
            bool enter1 = _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.000f);
            Assert.That(enter1, Is.True);

            // Act 1: Attempt to exit at t = 1.030s (dt = 0.030s < 0.060s) -> Must fail!
            bool quickExit = _hideSpot.TryExit(out _, currentTime: 1.030f);
            Assert.That(quickExit, Is.False, "Exit within 60ms window must be rejected by boundary hysteresis.");
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Occupied));

            // Act 2: Attempt exit at t = 1.070s (dt = 0.070s >= 0.060s) -> Must succeed!
            bool validExit = _hideSpot.TryExit(out _, currentTime: 1.070f);
            Assert.That(validExit, Is.True);
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));

            // Act 3: Attempt immediate re-entry at t = 1.090s (dt = 0.020s < 0.060s) -> Must fail!
            bool quickReentry = _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.090f);
            Assert.That(quickReentry, Is.False, "Re-entry within 60ms must be rejected by boundary hysteresis.");
            Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));
        }

        [Test]
        public void test_hidespot_witnessed_dwell_progresses_and_triggers_capture_at_1_5s()
        {
            // Arrange (AC-HIDE-03 & GDD #5 AC3, AC16): Witnessed entry under Chase
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: true, currentTime: 1.0f);
            Assert.That(_hideSpot.IsWitnessedEntry, Is.True);

            var guardGo = new GameObject("TestGuard");
            bool captureFired = false;
            _hideSpot.OnWitnessedCaptureTriggered += (spot, occ, guard) => captureFired = true;

            try
            {
                // Act 1: Guard not yet arrived at hold stance -> dwell does not advance
                bool capturedEarly = _hideSpot.TickWitnessedDwell(0.5f, guardGo.transform, guardArrivedAtHold: false);
                Assert.That(capturedEarly, Is.False);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(0f));

                // Act 2: Guard arrived at hold stance -> dwell advances
                _hideSpot.TickWitnessedDwell(0.5f, guardGo.transform, guardArrivedAtHold: true);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(0.5f).Within(1e-4f));

                _hideSpot.TickWitnessedDwell(0.5f, guardGo.transform, guardArrivedAtHold: true);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(1.0f).Within(1e-4f));
                Assert.That(captureFired, Is.False);

                // Act 3: Dwell reaches 1.5s -> Capture executed!
                bool captured = _hideSpot.TickWitnessedDwell(0.5f, guardGo.transform, guardArrivedAtHold: true);
                Assert.That(captured, Is.True);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(1.5f).Within(1e-4f));
                Assert.That(captureFired, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guardGo);
            }
        }

        [Test]
        public void test_hidespot_witnessed_dwell_resets_if_player_exits_before_1_5s()
        {
            // Arrange: Witnessed entry
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: true, currentTime: 1.0f);
            var guardGo = new GameObject("TestGuard");

            try
            {
                // Guard dwells for 1.0s (less than 1.5s)
                _hideSpot.TickWitnessedDwell(1.0f, guardGo.transform, guardArrivedAtHold: true);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(1.0f).Within(1e-4f));

                // Player exits before 1.5s expiration (t = 2.0s > 1.0s + 0.06s)
                bool exited = _hideSpot.TryExit(out _, currentTime: 2.0f);
                Assert.That(exited, Is.True);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(0f));
                Assert.That(_hideSpot.State, Is.EqualTo(HideSpotState.Empty));

                // Further ticking produces no capture
                bool captured = _hideSpot.TickWitnessedDwell(0.6f, guardGo.transform, guardArrivedAtHold: true);
                Assert.That(captured, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guardGo);
            }
        }

        [Test]
        public void test_hidespot_unwitnessed_dwell_does_not_advance_dwell_timer()
        {
            // Arrange: Unwitnessed entry (Sanctuary)
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.0f);
            var guardGo = new GameObject("TestGuard");

            try
            {
                // Act: Ticking dwell on sanctuary spot
                bool captured = _hideSpot.TickWitnessedDwell(1.6f, guardGo.transform, guardArrivedAtHold: true);

                // Assert: Never advances, never captures
                Assert.That(captured, Is.False);
                Assert.That(_hideSpot.DwellTimer, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guardGo);
            }
        }
    }
}
