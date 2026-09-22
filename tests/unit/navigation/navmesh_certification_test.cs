using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Navigation;

namespace WhisperWard.Tests.Unit.Navigation
{
    /// <summary>
    /// Automated NUnit EditMode unit test suite for NavMeshCertificationValidator.
    /// Verifies corridor width clearances (AC-NAV-08, AC-NAV-18), zero off-mesh links (AC-NAV-19),
    /// bake layer mask exclusions (AC-NAV-20), voxel resolution invariant (AC-NAV-07),
    /// and vertical slope/step ceilings (AC-NAV-21).
    /// Governed by ADR-0007, ADR-0002, and story-003-corridor-clearance-certification.md.
    /// </summary>
    [TestFixture]
    public class NavMeshCertificationTest
    {
        private NavMeshCertificationValidator _validator;

        [SetUp]
        public void SetUp()
        {
            // Set up validator with synthetic layer masks
            // Layer World = 1 << 0 (1), Player = 1 << 1 (2), Guard = 1 << 2 (4), Trigger = 1 << 3 (8)
            int worldMask = 1 << 0;
            int forbiddenMask = (1 << 1) | (1 << 2) | (1 << 3);
            _validator = new NavMeshCertificationValidator(worldMask, forbiddenMask);
        }

        #region AC-NAV-08 / AC-NAV-18: Corridor Width Invariant Validation

        [Test]
        public void Test_NavMeshValidator_CorridorWidthBelowMinimum_FailsWithChokePointPayload()
        {
            // Arrange: Static colliders at width 1.10m (< 1.20m min)
            // Left wall at 0.55m, right wall at 0.55m -> total = 1.10m
            Vector3 samplePoint = new Vector3(10f, 0f, 25f);
            Vector3 tangent = Vector3.forward;

            _validator.SetTestHooks((Vector3 origin, Vector3 direction, out float distance, float maxDist, int mask) =>
            {
                distance = 0.55f; // Both sides hit at 0.55m
                return true;
            });

            // Act
            bool isValid = _validator.ValidateCorridorClearance(
                samplePoint,
                tangent,
                NavMeshCertificationValidator.DefaultMinCorridorWidth, // 1.20m
                out float measuredWidth,
                out CorridorChokePoint chokePoint);

            // Assert: Must fail validation
            Assert.IsFalse(isValid, "Corridor width of 1.10m must fail clearance validation.");
            Assert.AreEqual(1.10f, measuredWidth, 0.001f, "Measured width must equal 1.10m.");
            Assert.AreEqual(samplePoint, chokePoint.Position, "Choke point position must match sample coordinates.");
            Assert.AreEqual(1.10f, chokePoint.MeasuredWidth, 0.001f);
            Assert.AreEqual(1.20f, chokePoint.RequiredWidth, 0.001f);
        }

        [TestCase(1.20f, 0.60f, true)]  // Minimum allowable width: exactly 1.20m
        [TestCase(1.50f, 0.75f, true)]  // Standard main patrol width: 1.50m
        [TestCase(2.00f, 1.00f, true)]  // Spacious corridor: 2.00m
        public void Test_NavMeshValidator_CorridorWidthAtOrAboveMinimum_PassesClearanceValidation(
            float totalWidth,
            float halfWidth,
            bool expectedValid)
        {
            // Arrange
            Vector3 samplePoint = Vector3.zero;
            Vector3 tangent = Vector3.forward;

            _validator.SetTestHooks((Vector3 origin, Vector3 direction, out float distance, float maxDist, int mask) =>
            {
                distance = halfWidth;
                return true;
            });

            // Act
            bool isValid = _validator.ValidateCorridorClearance(
                samplePoint,
                tangent,
                NavMeshCertificationValidator.DefaultMinCorridorWidth,
                out float measuredWidth,
                out CorridorChokePoint chokePoint);

            // Assert
            Assert.AreEqual(expectedValid, isValid);
            Assert.AreEqual(totalWidth, measuredWidth, 0.001f);
        }

        #endregion

        #region AC-NAV-19: Zero Off-Mesh Links Verification

        [Test]
        public void Test_NavMeshValidator_OffMeshLinksPresent_FailsWithErrOffMeshLinksPresent()
        {
            // Arrange: 1 off-mesh link present
            int linkCount = 1;
            bool generateLinks = false;

            // Act
            bool isValid = _validator.ValidateOffMeshLinks(linkCount, generateLinks, out string errorCode);

            // Assert
            Assert.IsFalse(isValid, "Asset with off-mesh links must fail build validation.");
            Assert.AreEqual(NavMeshCertificationValidator.ErrOffMeshLinksPresent, errorCode);
        }

        [Test]
        public void Test_NavMeshValidator_GenerateLinksEnabled_FailsWithErrOffMeshLinksEnabled()
        {
            // Arrange: 0 links present, but generateLinks flag is true
            int linkCount = 0;
            bool generateLinks = true;

            // Act
            bool isValid = _validator.ValidateOffMeshLinks(linkCount, generateLinks, out string errorCode);

            // Assert
            Assert.IsFalse(isValid, "generateLinks = true must fail build validation.");
            Assert.AreEqual(NavMeshCertificationValidator.ErrOffMeshLinksEnabled, errorCode);
        }

        [Test]
        public void Test_NavMeshValidator_ZeroOffMeshLinksAndDisabled_PassesValidation()
        {
            // Arrange
            int linkCount = 0;
            bool generateLinks = false;

            // Act
            bool isValid = _validator.ValidateOffMeshLinks(linkCount, generateLinks, out string errorCode);

            // Assert
            Assert.IsTrue(isValid, "Zero links and disabled generation must pass validation.");
            Assert.IsNull(errorCode);
        }

        #endregion

        #region AC-NAV-20: Physics Bake Layer Mask Exclusion

        [Test]
        public void Test_NavMeshValidator_LayerMaskContainsDynamicPlayerLayer_FailsWithErrDynamicLayersInBake()
        {
            // Arrange: Bake mask includes World (1) and Player (2) -> (1 | 2) = 3
            int bakeMask = (1 << 0) | (1 << 1);
            int forbiddenMask = (1 << 1) | (1 << 2) | (1 << 3);

            // Act
            bool isValid = _validator.ValidateBakeLayerMask(bakeMask, forbiddenMask, out string errorCode);

            // Assert
            Assert.IsFalse(isValid, "Bake mask containing Player layer must be rejected.");
            Assert.AreEqual(NavMeshCertificationValidator.ErrDynamicLayersInBake, errorCode);
        }

        [Test]
        public void Test_NavMeshValidator_LayerMaskContainsOnlyStaticWorld_PassesValidation()
        {
            // Arrange: Clean bake mask with only World layer (1)
            int bakeMask = 1 << 0;
            int forbiddenMask = (1 << 1) | (1 << 2) | (1 << 3);

            // Act
            bool isValid = _validator.ValidateBakeLayerMask(bakeMask, forbiddenMask, out string errorCode);

            // Assert
            Assert.IsTrue(isValid, "Bake mask containing only World geometry must pass.");
            Assert.IsNull(errorCode);
        }

        #endregion

        #region AC-NAV-07: NavMesh Voxel Resolution Invariant

        [TestCase(0.150f, false, NavMeshCertificationValidator.ErrBakeVoxelTooCoarse)] // Too coarse
        [TestCase(0.200f, false, NavMeshCertificationValidator.ErrBakeVoxelTooCoarse)] // Default Unity coarse voxel
        [TestCase(0.1333f, true, null)]                                                // Maximum allowable (r_guard / 3)
        [TestCase(0.100f, true, null)]                                                 // High precision 0.10m
        public void Test_NavMeshValidator_VoxelSizeEvaluation_ValidatesAgainstOneThirdGuardRadius(
            float voxelSize,
            bool expectedValid,
            string expectedErrorCode)
        {
            // Act
            bool isValid = _validator.ValidateVoxelSize(
                voxelSize,
                NavMeshCertificationValidator.DefaultMaxVoxelSize,
                out string errorCode);

            // Assert
            Assert.AreEqual(expectedValid, isValid);
            Assert.AreEqual(expectedErrorCode, errorCode);
        }

        #endregion

        #region AC-NAV-21: Slope & Step Height Bounds

        [Test]
        public void Test_NavMeshValidator_SlopeOrStepHeightExceeded_FailsWithRespectiveErrorCodes()
        {
            // Arrange: Slope 50 deg (> 45 deg) and climb step 0.40m (> 0.30m)
            float slope = 50.0f;
            float climb = 0.40f;

            // Act
            bool isValid = _validator.ValidateSlopeAndClimb(slope, climb, out List<string> errors);

            // Assert
            Assert.IsFalse(isValid);
            Assert.Contains(NavMeshCertificationValidator.ErrMaxSlopeExceeded, errors);
            Assert.Contains(NavMeshCertificationValidator.ErrStepHeightExceeded, errors);
        }

        [Test]
        public void Test_NavMeshValidator_ValidSlopeAndStepHeight_PassesValidation()
        {
            // Arrange: Slope 45 deg, climb step 0.30m
            float slope = 45.0f;
            float climb = 0.30f;

            // Act
            bool isValid = _validator.ValidateSlopeAndClimb(slope, climb, out List<string> errors);

            // Assert
            Assert.IsTrue(isValid);
            Assert.IsEmpty(errors);
        }

        #endregion

        #region Full Bake Config Validation

        [Test]
        public void Test_NavMeshValidator_CompleteValidBakeConfig_PassesAllChecks()
        {
            // Arrange
            NavMeshBakeConfig validConfig = new NavMeshBakeConfig(
                voxelSize: 0.10f,
                agentMaxSlope: 45.0f,
                agentClimb: 0.30f,
                generateLinks: false,
                layerMask: 1 << 0); // World only

            // Act
            bool isValid = _validator.ValidateBakeConfig(validConfig, out List<string> errors);

            // Assert
            Assert.IsTrue(isValid, "Standard valid bake config must pass all validation checks.");
            Assert.IsEmpty(errors);
        }

        #endregion
    }
}
