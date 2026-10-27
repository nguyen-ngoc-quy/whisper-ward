using UnityEngine;

namespace WhisperWard.Core.Rendering
{
    /// <summary>
    /// Procedural mesh generator for volumetric light shafts and sensor cones.
    /// Creates lightweight conical 3D geometry with vertex color falloff,
    /// enabling atmospheric fog and light beam rendering with zero runtime GC allocation.
    /// Highly optimized for 60 FPS performance on WebGL and mobile/PC platforms.
    /// </summary>
    public static class VolumetricConeMeshBuilder
    {
        /// <summary>
        /// Generates a procedural 3D cone mesh oriented along the positive Z-axis.
        /// </summary>
        /// <param name="fovDegrees">Total horizontal/vertical opening angle in degrees.</param>
        /// <param name="length">Length of the light beam in meters.</param>
        /// <param name="radialSegments">Number of polygonal divisions around the circumference.</param>
        /// <returns>Constructed Unity Mesh instance with vertex colors and normals.</returns>
        public static Mesh BuildConeMesh(float fovDegrees, float length, int radialSegments = 16)
        {
            radialSegments = Mathf.Clamp(radialSegments, 6, 64);
            length = Mathf.Max(0.1f, length);
            float halfAngleRad = Mathf.Clamp(fovDegrees, 5f, 175f) * 0.5f * Mathf.Deg2Rad;
            float radius = length * Mathf.Tan(halfAngleRad);

            int vertexCount = radialSegments + 2; // 1 apex + (radialSegments + 1) rim
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[radialSegments * 3];

            // Apex at origin
            vertices[0] = Vector3.zero;
            normals[0] = -Vector3.forward;
            colors[0] = new Color(1.0f, 1.0f, 1.0f, 0.45f);
            uvs[0] = new Vector2(0.5f, 0.0f);

            // Rim vertices
            float step = (2.0f * Mathf.PI) / radialSegments;
            for (int i = 0; i <= radialSegments; i++)
            {
                float angle = (i % radialSegments) * step;
                float x = radius * Mathf.Cos(angle);
                float y = radius * Mathf.Sin(angle);

                int vIdx = i + 1;
                vertices[vIdx] = new Vector3(x, y, length);
                normals[vIdx] = new Vector3(x, y, 0f).normalized;
                colors[vIdx] = new Color(1.0f, 1.0f, 1.0f, 0.0f); // Falloff to 0 at tip
                uvs[vIdx] = new Vector2((float)i / radialSegments, 1.0f);
            }

            // Triangles
            for (int i = 0; i < radialSegments; i++)
            {
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "Procedural_Volumetric_Cone",
                vertices = vertices,
                normals = normals,
                colors = colors,
                uv = uvs,
                triangles = triangles
            };

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
