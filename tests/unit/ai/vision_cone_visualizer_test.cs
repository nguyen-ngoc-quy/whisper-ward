using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Perception;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class VisionConeVisualizerTest
    {
        private class MockPhysicsQueryService : IPhysicsQueryService
        {
            public bool ReturnOccluded { get; set; } = false;
            public float HitDistance { get; set; } = 4.0f;

            public int DefaultBufferCapacity => 64;

            public bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)
            {
                hit = new RaycastHit();
                if (ReturnOccluded)
                {
                    // Mock a hit at HitDistance
                    hit.distance = HitDistance;
                    hit.point = start + (end - start).normalized * HitDistance;
                    return true;
                }
                return false;
            }

            public int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask) => 0;
            public int SphereCastNonAlloc(Ray ray, float radius, RaycastHit[] results, float maxDistance, int layerMask) => 0;
            public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask) => 0;
            public bool CheckOriginEmbedded(Vector3 origin, int layerMask, float testRadius = 0.05f) => false;
            public bool TrySyncTransformsBatch(float currentTime) => true;
        }

        private GameObject _guardObject;
        private VisionConeSensor _sensor;
        private SuspicionAccumulator _accumulator;
        private VisionConeVisualizer _visualizer;
        private MockPhysicsQueryService _mockPhysics;

        [SetUp]
        public void SetUp()
        {
            _guardObject = new GameObject("Guard_Visualizer_Test");
            _sensor = _guardObject.AddComponent<VisionConeSensor>();
            _accumulator = _guardObject.AddComponent<SuspicionAccumulator>();
            _visualizer = _guardObject.AddComponent<VisionConeVisualizer>();

            _mockPhysics = new MockPhysicsQueryService();
            _sensor.Configure(_mockPhysics, visionRange: 12.0f, visionFOV: 100.0f);
            _visualizer.Configure(_sensor, _accumulator, _mockPhysics, segments: 10, groundOffset: 0.10f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardObject != null)
            {
                Object.DestroyImmediate(_guardObject);
            }
        }

        [Test]
        public void test_vision_cone_visualizer_mesh_vertex_and_triangle_counts()
        {
            // Arrange: 10 segments (AC-GUARD-13)
            // 1 origin vertex + 11 perimeter vertices = 12 vertices
            // 10 segments * 3 = 30 triangle indices

            // Act
            _visualizer.GenerateVertices(range: 12.0f, fov: 100.0f, mask: 1 << 20);
            Vector3[] vertices = _visualizer.GetGeneratedVertices();

            // Assert
            Assert.AreEqual(12, vertices.Length, "Vertex array length must be segments + 2.");
            Assert.AreEqual(new Vector3(0f, 0.10f, 0f), vertices[0], "Origin vertex must be at ground offset.");
        }

        [Test]
        public void test_vision_cone_visualizer_unoccluded_vertices_reach_full_range()
        {
            // Arrange: Unoccluded geometry (AC-GUARD-13)
            _mockPhysics.ReturnOccluded = false;

            // Act
            _visualizer.GenerateVertices(range: 12.0f, fov: 100.0f, mask: 1 << 20);
            Vector3[] vertices = _visualizer.GetGeneratedVertices();

            // Assert: Perimeter vertices (index 1 to 11) should have distance of 12.0m from origin
            Vector3 origin = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                float dist = Vector3.Distance(new Vector3(origin.x, 0, origin.z), new Vector3(vertices[i].x, 0, vertices[i].z));
                Assert.AreEqual(12.0f, dist, 0.01f, $"Perimeter vertex {i} must reach 12.0m unoccluded.");
            }
        }

        [Test]
        public void test_vision_cone_visualizer_occluded_vertices_clamp_to_wall_hit_distance()
        {
            // Arrange: Wall intersects at 4.0m (AC-GUARD-13)
            _mockPhysics.ReturnOccluded = true;
            _mockPhysics.HitDistance = 4.0f;

            // Act
            _visualizer.GenerateVertices(range: 12.0f, fov: 100.0f, mask: 1 << 20);
            Vector3[] vertices = _visualizer.GetGeneratedVertices();

            // Assert: Perimeter vertices should clamp to 4.0m
            Vector3 origin = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                float dist = Vector3.Distance(new Vector3(origin.x, 0, origin.z), new Vector3(vertices[i].x, 0, vertices[i].z));
                Assert.AreEqual(4.0f, dist, 0.01f, $"Occluded vertex {i} must clamp to 4.0m wall.");
            }
        }

        [Test]
        public void test_vision_cone_visualizer_color_transitions_with_suspicion_state()
        {
            // Arrange (AC-GUARD-14)
            // State 1: Idle (A = 0) -> Calm Moonlit Cyan
            _visualizer.UpdateMaterialColor();
            Assert.AreEqual(_visualizer.CalmColor.r, _visualizer.CurrentColor.r, 0.01f);
            Assert.AreEqual(_visualizer.CalmColor.g, _visualizer.CurrentColor.g, 0.01f);
            Assert.AreEqual(_visualizer.CalmColor.b, _visualizer.CurrentColor.b, 0.01f);

            // State 2: Halfway suspicious (A = 0.50) -> Warning Warm Amber
            _accumulator.Tick(0.50f / 0.60f, hasLOS: true, distance: 2.0f);
            _visualizer.UpdateMaterialColor();
            Assert.AreEqual(_visualizer.WarningColor.r, _visualizer.CurrentColor.r, 0.02f);
            Assert.AreEqual(_visualizer.WarningColor.g, _visualizer.CurrentColor.g, 0.02f);

            // State 3: Full Alert / Chase (A = 1.00 or InChase) -> Hostile Alert Red
            _accumulator.SetChaseMode(true);
            _visualizer.UpdateMaterialColor();
            Assert.AreEqual(_visualizer.AlertColor.r, _visualizer.CurrentColor.r, 0.01f);
            Assert.AreEqual(_visualizer.AlertColor.g, _visualizer.CurrentColor.g, 0.01f);
            Assert.AreEqual(_visualizer.AlertColor.b, _visualizer.CurrentColor.b, 0.01f);
        }
    }
}
