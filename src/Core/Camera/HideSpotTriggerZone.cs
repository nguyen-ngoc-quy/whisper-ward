using System;
using UnityEngine;

namespace WhisperWard.Core.Camera
{
    /// <summary>
    /// Trigger volume component mounted on hide spot trigger volumes (Layer 10: HideSpotTrigger).
    /// Detects player entry/exit and coordinates viewport transitions via CameraOrbitDriver.
    /// Governed by ADR-0008, GDD #20, and AC-SCENE-12.
    /// </summary>
    /// <example>
    /// <code>
    /// var zone = triggerObject.AddComponent&lt;HideSpotTriggerZone&gt;();
    /// zone.Configure(doorwayTransform.position, doorwayTransform.forward);
    /// </code>
    /// </example>
    [RequireComponent(typeof(Collider))]
    public sealed class HideSpotTriggerZone : MonoBehaviour
    {
        [Header("Aperture Portal")]
        [Tooltip("Outward viewing direction from the hide spot interior through the doorway/portal.")]
        [SerializeField] private Vector3 _outwardFacing = Vector3.forward;

        [Tooltip("Target position of the hide spot peephole or doorway aperture.")]
        [SerializeField] private Vector3 _apertureOffset = Vector3.zero;

        [Header("Camera Rig Reference")]
        [SerializeField] private CameraOrbitDriver _cameraDriver;

        private Collider _collider;

        /// <summary>
        /// Gets the outward viewing normal vector.
        /// </summary>
        public Vector3 OutwardFacing => _outwardFacing;

        /// <summary>
        /// Gets the evaluated world aperture target position.
        /// </summary>
        public Vector3 AperturePosition => transform.position + _apertureOffset;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            if (_cameraDriver == null)
            {
                _cameraDriver = FindFirstObjectByType<CameraOrbitDriver>();
            }
        }

        /// <summary>
        /// Configures the aperture portal orientation and driver reference.
        /// </summary>
        /// <param name="outwardFacing">Outward normal direction through the doorway.</param>
        /// <param name="apertureOffset">Offset from trigger center to viewing peephole.</param>
        /// <param name="driver">Optional reference to CameraOrbitDriver.</param>
        /// <example>
        /// <code>
        /// triggerZone.Configure(Vector3.forward, Vector3.up * 0.8f, cameraDriver);
        /// </code>
        /// </example>
        public void Configure(Vector3 outwardFacing, Vector3 apertureOffset, CameraOrbitDriver driver = null)
        {
            _outwardFacing = outwardFacing.sqrMagnitude > 1e-8f ? outwardFacing.normalized : Vector3.forward;
            _apertureOffset = apertureOffset;
            if (driver != null) _cameraDriver = driver;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (_cameraDriver == null)
            {
                _cameraDriver = FindFirstObjectByType<CameraOrbitDriver>();
            }

            if (_cameraDriver != null)
            {
                _cameraDriver.SwitchToHideSpotView(AperturePosition, _outwardFacing);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (_cameraDriver != null)
            {
                _cameraDriver.SwitchToOrbitView();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 pos = transform.position + _apertureOffset;
            Gizmos.DrawWireSphere(pos, 0.20f);
            Gizmos.DrawRay(pos, _outwardFacing.normalized * 1.5f);
        }
    }
}
