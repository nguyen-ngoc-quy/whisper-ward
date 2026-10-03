using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Navigation;

namespace WhisperWard.Tests.Integration.Scene
{
    /// <summary>
    /// Integration test suite for Story SCENE-04 (NavMesh Surface Baking & Simple Patrol Guard NPC).
    /// Verifies patrol parameters, waypoint arrival detection, 2.0s dwell pause, and endless round-robin route looping.
    /// Governed by ADR-0007, GDD #12, AC-SCENE-14, and AC-SCENE-15.
    /// </summary>
    [TestFixture]
    public sealed class SimplePatrolDriverIntegrationTest
    {
        private GameObject _guardGo;
        private NavMeshAgent _agent;
        private SimplePatrolDriver _driver;
        private List<GameObject> _waypointGos;
        private List<Transform> _waypoints;

        [SetUp]
        public void SetUp()
        {
            // 1. Create Guard NPC GameObject
            _guardGo = new GameObject("Test_Guard_PatrolNPC");
            _guardGo.tag = "Respawn";
            _guardGo.layer = 7; // Layer 7: Guard

            _agent = _guardGo.AddComponent<NavMeshAgent>();
            _agent.radius = 0.40f;
            _agent.height = 1.80f;

            // 2. Create Waypoints along Main Corridor (South: Z=-3m, North: Z=5m)
            _waypointGos = new List<GameObject>();
            _waypoints = new List<Transform>();

            GameObject wpA = new GameObject("WP_A");
            wpA.transform.position = new Vector3(0f, 0f, -3f);
            _waypointGos.Add(wpA);
            _waypoints.Add(wpA.transform);

            GameObject wpB = new GameObject("WP_B");
            wpB.transform.position = new Vector3(0f, 0f, 5f);
            _waypointGos.Add(wpB);
            _waypoints.Add(wpB.transform);

            // 3. Mount and configure SimplePatrolDriver
            _driver = _guardGo.AddComponent<SimplePatrolDriver>();
            _driver.Configure(_agent, _waypoints, dwellDuration: 2.0f, speed: 2.30f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            if (_waypointGos != null)
            {
                foreach (var go in _waypointGos)
                {
                    if (go != null) Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void test_simple_patrol_driver_initial_state_and_speed_match_ac_scene_14_and_15()
        {
            // Assert: verify initial patrol parameters conform to AC-SCENE-14 and AC-SCENE-15
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.MovingToWaypoint));
            Assert.That(_driver.PatrolSpeed, Is.EqualTo(2.30f));
            Assert.That(_driver.CurrentWaypointIndex, Is.EqualTo(0));
            Assert.That(_driver.CurrentDestination, Is.EqualTo(_waypoints[0]));
        }

        [Test]
        public void test_simple_patrol_driver_arrival_transitions_to_dwelling_with_two_second_pause()
        {
            // Arrange: Position guard at Destination WP_A (within 0.30m arrival tolerance)
            _guardGo.transform.position = _waypoints[0].position;

            // Act: Tick driver simulation
            _driver.TickDriver(0.016f);

            // Assert: Guard must transition to Dwelling state with exactly 2.0s dwell timer (AC-SCENE-15)
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.DwellingAtWaypoint));
            Assert.That(_driver.DwellTimer, Is.EqualTo(2.0f).Within(0.02f));
        }

        [Test]
        public void test_simple_patrol_driver_dwelling_countdown_advances_to_next_waypoint()
        {
            // Arrange: Force guard into dwelling state at WP_A
            _guardGo.transform.position = _waypoints[0].position;
            _driver.TickDriver(0.016f);
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.DwellingAtWaypoint));

            // Act: Advance time past 2.0s dwell duration (e.g. 2.1s)
            _driver.TickDriver(2.10f);

            // Assert: Guard must finish dwell, advance index to 1 (WP_B), and transition to MovingToWaypoint
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.MovingToWaypoint));
            Assert.That(_driver.CurrentWaypointIndex, Is.EqualTo(1));
            Assert.That(_driver.CurrentDestination, Is.EqualTo(_waypoints[1]));
        }

        [Test]
        public void test_simple_patrol_driver_round_robin_loop_wraps_around_endlessly()
        {
            // Arrange: Guard currently moving to WP_B (index 1)
            _driver.AdvanceToNextWaypoint();
            Assert.That(_driver.CurrentWaypointIndex, Is.EqualTo(1));

            // Move to WP_B position
            _guardGo.transform.position = _waypoints[1].position;
            _driver.TickDriver(0.016f);
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.DwellingAtWaypoint));

            // Act: Elapse dwell time at WP_B
            _driver.TickDriver(2.10f);

            // Assert: Waypoint index must wrap around back to 0 (endless loop, AC-SCENE-15)
            Assert.That(_driver.CurrentWaypointIndex, Is.EqualTo(0));
            Assert.That(_driver.CurrentDestination, Is.EqualTo(_waypoints[0]));
            Assert.That(_driver.CurrentState, Is.EqualTo(SimplePatrolDriver.PatrolState.MovingToWaypoint));
        }
    }
}
