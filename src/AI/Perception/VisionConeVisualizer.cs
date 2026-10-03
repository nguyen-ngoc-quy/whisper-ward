using System;
using UnityEngine;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Procedural 3D Mesh Visualizer for Guard NPC Vision Cones.
    /// Generates a segmented fan mesh projected forward and clamped against Layer 20 (World) obstacles.
    /// Dynamically shifts colors between Calm Cyan, Warning Amber, and Alert Red
    /// based on the Guard's SuspicionAccumulator state.
    /// Conforms to GDD Perception R2 (Rendered-cone bias), Story GUARD-03 (AC-GUARD-13..14).
    /// </summary>
    /// <example>
    /// <code>
    /// var visualizer = guardObject.AddComponent&lt;VisionConeVisualizer&gt;();
    /// visualizer.Configure(sensor, accumulator, segments: 36);
    /// </code>
    /// </example>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionConeVisualizer : MonoBehaviour
    {
        [Header("Cone Geometry")]
        [Tooltip("Number of radial angular segments for the arc fan (default: 36).")]
        [SerializeField] private int _segments = 36;

        [Tooltip("Vertical height offset relative to guard transform for the mesh plane (default: 0.10m above ground).")]
        [SerializeField] private float _groundOffset = 0.10f;

        [Header("Color Palette (Cold Watch VIA)")]
        [Tooltip("Calm / Patrol color (Moonlit Cyan).")]
        [SerializeField] private Color _calmColor = new Color(0.22f, 0.63f, 0.72f, 0.25f); // #38A0B8

        [Tooltip("Warning / Suspicious charging color (Warm Amber).")]
        [SerializeField] private Color _warningColor = new Color(0.94f, 0.72f, 0.13f, 0.35f); // #F0B820

        [Tooltip("Hostile / Chase alert color (Alert Red).")]
        [SerializeField] private Color _alertColor = new Color(0.88f, 0.19f, 0.19f, 0.45f); // #E03030

        [Header("References")]
        [SerializeField] private VisionConeSensor _sensor;
        [SerializeField] private SuspicionAccumulator _accumulator;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorPropId = Shader.PropertyToID("_BaseColor");

        private Vector3[] _vertices;
        private int[] _triangles;
        private Vector2[] _uvs;
        private IPhysicsQueryService _physicsQuery;

        public int Segments => _segments;
        public float GroundOffset => _groundOffset;
        public Color CalmColor => _calmColor;
        public Color WarningColor => _warningColor;
        public Color AlertColor => _alertColor;
        public Color CurrentColor { get; private set; }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();

            if (_sensor == null)
            {
                _sensor = GetComponentInParent<VisionConeSensor>();
            }
            if (_accumulator == null)
            {
                _accumulator = GetComponentInParent<SuspicionAccumulator>();
            }

            InitializeMeshBuffers(_segments);
        }

        private void LateUpdate()
        {
            UpdateVisuals();
        }

        /// <summary>
        /// Explicit dependency injection and configuration for runtime and testing.
        /// </summary>
        public void Configure(
            VisionConeSensor sensor,
            SuspicionAccumulator accumulator,
            IPhysicsQueryService physicsQuery = null,
            int segments = 36,
            float groundOffset = 0.10f)
        {
            _sensor = sensor;
            _accumulator = accumulator;
            _physicsQuery = physicsQuery;
            _segments = Mathf.Clamp(segments, 4, 120);
            _groundOffset = groundOffset;

            InitializeMeshBuffers(_segments);
        }

        /// <summary>
        /// Pre-allocates mesh arrays to ensure zero runtime GC heap allocations during animation.
        /// </summary>
        public void InitializeMeshBuffers(int segments)
        {
            _segments = Mathf.Clamp(segments, 4, 120);
            // 1 origin vertex + (segments + 1) perimeter vertices
            int vertexCount = _segments + 2;
            int triangleCount = _segments * 3;

            _vertices = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _triangles = new int[triangleCount];

            // Build static triangle index buffer
            for (int i = 0; i < _segments; i++)
            {
                int triIndex = i * 3;
                _triangles[triIndex] = 0;
                _triangles[triIndex + 1] = i + 1;
                _triangles[triIndex + 2] = i + 2;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "VisionConeMesh" };
                _mesh.MarkDynamic();
            }

            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = _mesh;
            }
        }

        /// <summary>
        /// Rebuilds cone geometry clamped to obstacles and updates material styling.
        /// Zero allocations.
        /// </summary>
        public void UpdateVisuals()
        {
            float range = _sensor != null ? _sensor.VisionRange : 12.0f;
            float fov = _sensor != null ? _sensor.VisionFOV : 100.0f;
            LayerMask mask = _sensor != null ? _sensor.OcclusionMask : (1 << 20);

            GenerateVertices(range, fov, mask);
            UpdateMaterialColor();
        }

        /// <summary>
        /// Computes vertices in local space, raycasting each perimeter vertex against obstacles.
        /// </summary>
        public void GenerateVertices(float range, float fov, LayerMask mask)
        {
            if (_vertices == null || _vertices.Length != _segments + 2)
            {
                InitializeMeshBuffers(_segments);
            }

            // Origin point (local space)
            Vector3 originLocal = new Vector3(0f, _groundOffset, 0f);
            _vertices[0] = originLocal;
            _uvs[0] = new Vector2(0.5f, 0f);

            float halfFov = fov * 0.5f;
            float angleStep = fov / _segments;
            Vector3 originWorld = transform.position + Vector3.up * _groundOffset;

            for (int i = 0; i <= _segments; i++)
            {
                float currentAngle = -halfFov + i * angleStep;
                Quaternion rot = Quaternion.Euler(0f, currentAngle, 0f);
                Vector3 dirLocal = rot * Vector3.forward;
                Vector3 dirWorld = transform.TransformDirection(dirLocal);

                float vertexDistance = range;

                // Check obstacle occlusion
                if (_physicsQuery != null)
                {
                    if (_physicsQuery.CheckOcclusionLine(originWorld, originWorld + dirWorld * range, mask, out RaycastHit hit))
                    {
                        vertexDistance = Mathf.Max(0.1f, hit.distance);
                    }
                }
                else
                {
                    if (Physics.Raycast(originWorld, dirWorld, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
                    {
                        vertexDistance = Mathf.Max(0.1f, hit.distance);
                    }
                }

                _vertices[i + 1] = originLocal + dirLocal * vertexDistance;
                _uvs[i + 1] = new Vector2((float)i / _segments, 1.0f);
            }

            if (_mesh != null)
            {
                _mesh.Clear(false);
                _mesh.vertices = _vertices;
                _mesh.uv = _uvs;
                _mesh.triangles = _triangles;
                _mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// Updates shader material color property smoothly based on suspicion accumulator.
        /// </summary>
        public void UpdateMaterialColor()
        {
            float a = _accumulator != null ? _accumulator.Accumulator : 0f;
            bool inChase = _accumulator != null && _accumulator.IsInChase;

            if (inChase || a >= 1.0f)
            {
                CurrentColor = _alertColor;
            }
            else if (a > 0.001f)
            {
                // Smooth blend from calm to warning, then warning to alert
                if (a < 0.5f)
                {
                    float t = a / 0.5f;
                    CurrentColor = Color.Lerp(_calmColor, _warningColor, t);
                }
                else
                {
                    float t = (a - 0.5f) / 0.5f;
                    CurrentColor = Color.Lerp(_warningColor, _alertColor, t);
                }
            }
            else
            {
                CurrentColor = _calmColor;
            }

            if (_meshRenderer != null && _propBlock != null)
            {
                _meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(ColorPropId, CurrentColor);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
        }

        public Vector3[] GetGeneratedVertices()
        {
            return _vertices;
        }
    }
}
