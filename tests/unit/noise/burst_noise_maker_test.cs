using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Player;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Unit.Noise
{
    [TestFixture]
    public class BurstNoiseMakerTest
    {
        private GameObject _projectileObject;
        private BurstDistractionNoiseMaker _noiseMaker;
        private MockPhysicsQueryService _mockPhysics;

        private class MockPhysicsQueryService : IPhysicsQueryService
        {
            public bool HasHit { get; set; } = false;
            public Vector3 HitPoint { get; set; } = Vector3.zero;

            public bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)
            {
                if (HasHit)
                {
                    hit = default;
                    return true;
                }

                hit = default;
                return false;
            }

            public bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, out RaycastHit hit)
            {
                hit = default;
                return false;
            }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out RaycastHit hit)
            {
                hit = default;
                return false;
            }

            public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask)
            {
                return 0;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _projectileObject = new GameObject("BurstNoiseMaker_Test");
            _noiseMaker = _projectileObject.AddComponent<BurstDistractionNoiseMaker>();
            _noiseMaker.Configure(
                initialSpeed: 12.00f,
                launchAngle: 30.00f,
                releaseHeight: 1.50f,
                gravity: 9.81f,
                acousticRadius: 10.00f,
                movingSpeedThreshold: 1.00f);

            _mockPhysics = new MockPhysicsQueryService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_projectileObject != null)
            {
                Object.DestroyImmediate(_projectileObject);
            }
        }

        [Test]
        public void test_burst_noise_maker_initial_state_is_placed_with_gdd_parameters()
        {
            // Assert (AC-NOISE-05)
            Assert.AreEqual(BurstItemState.Placed, _noiseMaker.State);
            Assert.AreEqual(12.00f, _noiseMaker.InitialSpeed, 0.01f);
            Assert.AreEqual(30.00f, _noiseMaker.LaunchAngle, 0.01f);
            Assert.AreEqual(1.50f, _noiseMaker.ReleaseHeight, 0.01f);
            Assert.AreEqual(9.81f, _noiseMaker.Gravity, 0.01f);
            Assert.AreEqual(10.00f, _noiseMaker.AcousticRadius, 0.01f);
            Assert.AreEqual(1.00f, _noiseMaker.MovingSpeedThreshold, 0.01f);
        }

        [Test]
        public void test_burst_noise_maker_pickup_transitions_to_carried()
        {
            // Act
            bool pickedUp = _noiseMaker.Pickup();

            // Assert
            Assert.IsTrue(pickedUp);
            Assert.AreEqual(BurstItemState.Carried, _noiseMaker.State);

            // Cannot pick up again if already carried
            Assert.IsFalse(_noiseMaker.Pickup());
        }

        [Test]
        public void test_burst_noise_maker_throw_rejected_when_not_carried()
        {
            // Arrange: State is Placed
            Vector3 throwerFeet = Vector3.zero;
            Vector3 throwDir = Vector3.forward;

            // Act
            bool success = _noiseMaker.TryThrow(throwerFeet, throwDir, throwerPlanarSpeed: 0f, out string reason);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual("ITEM_NOT_CARRIED", reason);
            Assert.AreEqual(BurstItemState.Placed, _noiseMaker.State);
        }

        [Test]
        public void test_burst_noise_maker_throw_rejected_when_moving_above_threshold()
        {
            // Arrange: Carried, but player planar speed >= 1.0m/s (AC-NOISE-06)
            _noiseMaker.Pickup();
            Vector3 throwerFeet = Vector3.zero;
            Vector3 throwDir = Vector3.forward;

            bool rejectedEventFired = false;
            _noiseMaker.OnThrowRejectedMoving += () => rejectedEventFired = true;

            // Act 1: 1.0 m/s (boundary condition)
            bool thrownAtBoundary = _noiseMaker.TryThrow(throwerFeet, throwDir, throwerPlanarSpeed: 1.00f, out string reason1);

            // Assert 1
            Assert.IsFalse(thrownAtBoundary);
            Assert.AreEqual("THROW_WHILE_MOVING_REJECTED", reason1);
            Assert.IsTrue(rejectedEventFired);
            Assert.AreEqual(BurstItemState.Carried, _noiseMaker.State);

            // Act 2: Running speed 5.0 m/s
            rejectedEventFired = false;
            bool thrownRunning = _noiseMaker.TryThrow(throwerFeet, throwDir, throwerPlanarSpeed: 5.00f, out string reason2);

            // Assert 2
            Assert.IsFalse(thrownRunning);
            Assert.AreEqual("THROW_WHILE_MOVING_REJECTED", reason2);
            Assert.IsTrue(rejectedEventFired);
            Assert.AreEqual(BurstItemState.Carried, _noiseMaker.State);
        }

        [Test]
        public void test_burst_noise_maker_throw_accepted_when_stationary()
        {
            // Arrange (AC-NOISE-05, AC-NOISE-06)
            _noiseMaker.Pickup();
            Vector3 throwerFeet = new Vector3(10f, 0f, 20f);
            Vector3 throwDir = Vector3.forward; // (0, 0, 1)

            bool launchEventFired = false;
            _noiseMaker.OnBurstLaunched += () => launchEventFired = true;

            // Act: Planar speed 0.2 m/s (< 1.0 m/s)
            bool success = _noiseMaker.TryThrow(throwerFeet, throwDir, throwerPlanarSpeed: 0.20f, out string reason);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNull(reason);
            Assert.IsTrue(launchEventFired);
            Assert.AreEqual(BurstItemState.InFlight, _noiseMaker.State);

            // Check release height
            Assert.AreEqual(new Vector3(10f, 1.50f, 20f), _noiseMaker.CurrentFlightPosition);

            // Check ballistic velocity components:
            // Vx = V0 * cos(30 deg) = 12.0 * 0.8660254 = 10.3923 m/s
            // Vy = V0 * sin(30 deg) = 12.0 * 0.5 = 6.00 m/s
            float expectedVx = 12.00f * Mathf.Cos(30.0f * Mathf.Deg2Rad);
            float expectedVy = 12.00f * Mathf.Sin(30.0f * Mathf.Deg2Rad);

            Assert.AreEqual(0f, _noiseMaker.CurrentFlightVelocity.x, 0.01f);
            Assert.AreEqual(expectedVy, _noiseMaker.CurrentFlightVelocity.y, 0.01f);
            Assert.AreEqual(expectedVx, _noiseMaker.CurrentFlightVelocity.z, 0.01f);
        }

        [Test]
        public void test_burst_noise_maker_flight_simulation_moves_ballistically()
        {
            // Arrange
            _noiseMaker.SetCarried();
            _noiseMaker.TryThrow(Vector3.zero, Vector3.forward, 0f, out _);

            Vector3 startPos = _noiseMaker.CurrentFlightPosition;
            float initialVy = _noiseMaker.CurrentFlightVelocity.y;

            // Act: Step 0.1s
            _noiseMaker.TickFlight(0.1f);

            // Assert
            Assert.AreEqual(0.1f, _noiseMaker.FlightTime, 0.001f);
            // Vy after 0.1s = initialVy - 9.81 * 0.1
            float expectedVy = initialVy - 9.81f * 0.1f;
            Assert.AreEqual(expectedVy, _noiseMaker.CurrentFlightVelocity.y, 0.01f);
            // Z should increase
            Assert.Greater(_noiseMaker.CurrentFlightPosition.z, startPos.z);
            Assert.AreEqual(BurstItemState.InFlight, _noiseMaker.State);
        }

        [Test]
        public void test_burst_noise_maker_impact_on_ground_emits_10m_acoustic_pulse_and_consumes()
        {
            // Arrange (AC-NOISE-07)
            _noiseMaker.SetCarried();
            _noiseMaker.TryThrow(Vector3.zero, Vector3.forward, 0f, out _);

            bool instanceLanded = false;
            Vector3 instancePoint = Vector3.zero;
            float instanceRadius = 0f;

            bool globalEmitted = false;
            Vector3 globalPoint = Vector3.zero;
            float globalRadius = 0f;

            _noiseMaker.OnBurstLanded += (pt, rad) =>
            {
                instanceLanded = true;
                instancePoint = pt;
                instanceRadius = rad;
            };

            BurstDistractionNoiseMaker.OnAnyBurstNoiseEmitted += (pt, rad) =>
            {
                globalEmitted = true;
                globalPoint = pt;
                globalRadius = rad;
            };

            // Act: Step flight until projectile hits ground (y <= 0)
            // Estimated flight time ~1.43s
            for (int i = 0; i < 200; i++)
            {
                if (_noiseMaker.State != BurstItemState.InFlight) break;
                _noiseMaker.TickFlight(0.02f);
            }

            // Assert
            Assert.AreEqual(BurstItemState.Consumed, _noiseMaker.State, "State must transition to Consumed after impact.");
            Assert.IsTrue(instanceLanded, "Instance OnBurstLanded event must fire.");
            Assert.IsTrue(globalEmitted, "Global OnAnyBurstNoiseEmitted event must fire.");
            Assert.AreEqual(10.00f, instanceRadius, 0.01f, "Impact acoustic radius must be 10.0m.");
            Assert.AreEqual(10.00f, globalRadius, 0.01f, "Global acoustic radius must be 10.0m.");
            Assert.AreEqual(0.0f, instancePoint.y, 0.05f, "Ground impact altitude must be 0m.");
            Assert.Greater(instancePoint.z, 10.0f, "Landing distance should be > 10m forward.");
        }

        [Test]
        public void test_burst_noise_maker_analytical_solver_matches_simulation()
        {
            // Arrange: Release at (0, 1.5, 0), V0 = 12.0 m/s, angle = 30 deg, g = 9.81
            Vector3 releasePos = new Vector3(0, 1.50f, 0);
            Vector3 planarDir = Vector3.forward;

            // Act: Analytical solve
            bool solved = BurstDistractionNoiseMaker.SolveAnalyticalLanding(
                releasePos,
                planarDir,
                initialSpeed: 12.00f,
                launchAngleDeg: 30.00f,
                gravity: 9.81f,
                targetGroundY: 0f,
                out Vector3 analyticalLanding,
                out float analyticalTime);

            // Assert analytical
            Assert.IsTrue(solved);
            Assert.Greater(analyticalTime, 1.30f);
            Assert.Less(analyticalTime, 1.60f);
            Assert.AreEqual(0f, analyticalLanding.y, 0.001f);

            // Compare with numerical step simulation
            _noiseMaker.SetCarried();
            _noiseMaker.TryThrow(Vector3.zero, planarDir, 0f, out _);

            float dt = 0.001f; // fine-grained step
            while (_noiseMaker.State == BurstItemState.InFlight)
            {
                _noiseMaker.TickFlight(dt);
            }

            // Verify landing Z matches within 0.15m tolerance
            Assert.AreEqual(analyticalLanding.z, _noiseMaker.CurrentFlightPosition.z, 0.15f,
                "Numerical simulation landing must match analytical range within integration tolerance.");
        }

        [Test]
        public void test_burst_noise_maker_collision_with_solid_wall_stops_early()
        {
            // Arrange
            _noiseMaker.SetCarried();
            _noiseMaker.TryThrow(Vector3.zero, Vector3.forward, 0f, out _);

            _mockPhysics.HasHit = true;

            // Act: Step flight with mock collision detected
            _noiseMaker.TickFlight(0.05f, _mockPhysics);

            // Assert
            Assert.AreEqual(BurstItemState.Consumed, _noiseMaker.State);
            Assert.AreEqual(Vector3.zero, _noiseMaker.CurrentFlightVelocity);
        }
    }
}
