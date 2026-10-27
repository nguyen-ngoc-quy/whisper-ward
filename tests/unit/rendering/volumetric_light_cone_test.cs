using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Rendering;

namespace WhisperWard.Tests.Unit.Rendering
{
    [TestFixture]
    public class VolumetricLightConeTest
    {
        [Test]
        public void test_volumetric_cone_mesh_builder_generates_valid_vertex_and_triangle_counts()
        {
            // Arrange
            float fov = 60.0f;
            float length = 10.0f;
            int segments = 16;

            // Act
            Mesh mesh = VolumetricConeMeshBuilder.BuildConeMesh(fov, length, segments);

            // Assert
            Assert.IsNotNull(mesh);
            // 1 apex + (segments + 1) rim vertices
            Assert.AreEqual(segments + 2, mesh.vertexCount);
            // segments triangles = segments * 3 indices
            Assert.AreEqual(segments * 3, mesh.triangles.Length);
        }

        [Test]
        public void test_volumetric_cone_mesh_calculates_correct_apex_and_rim_radius()
        {
            // Arrange
            float fov = 90.0f; // tan(45 deg) = 1.0
            float length = 5.0f;
            int segments = 8;

            // Act
            Mesh mesh = VolumetricConeMeshBuilder.BuildConeMesh(fov, length, segments);
            Vector3[] vertices = mesh.vertices;

            // Assert
            // Apex at origin
            Assert.AreEqual(Vector3.zero, vertices[0]);

            // Rim vertex radius should equal length * tan(fov / 2) = 5.0 * 1.0 = 5.0
            float expectedRadius = 5.0f;
            Vector3 firstRimVertex = vertices[1];
            Assert.AreEqual(length, firstRimVertex.z, 0.001f);
            float actualRadius = Mathf.Sqrt(firstRimVertex.x * firstRimVertex.x + firstRimVertex.y * firstRimVertex.y);
            Assert.AreEqual(expectedRadius, actualRadius, 0.01f);
        }

        [Test]
        public void test_facility_visual_palette_config_has_valid_pbr_ranges()
        {
            // Arrange & Act
            var palette = new FacilityVisualPaletteConfig();

            // Assert
            Assert.IsTrue(palette.FloorMetallic >= 0f && palette.FloorMetallic <= 1f);
            Assert.IsTrue(palette.FloorSmoothness >= 0f && palette.FloorSmoothness <= 1f);
            Assert.IsTrue(palette.WallSmoothness >= 0f && palette.WallSmoothness <= 1f);
            Assert.IsTrue(palette.VisorEmissionIntensity > 1.0f);
        }
    }
}
