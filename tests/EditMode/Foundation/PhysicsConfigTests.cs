using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.EditMode.Foundation
{
    public sealed class PhysicsConfigTests
    {
        [Test]
        public void test_physics_config_resolves_all_seven_layers_and_assembles_e20_mask()
        {
            // Arrange: Simulated layer resolver mapping standard layers to distinct indices
            var mockLayerTable = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { PhysicsCollisionConfig.LayerPlayer, 8 },
                { PhysicsCollisionConfig.LayerGuard, 9 },
                { PhysicsCollisionConfig.LayerWorld, 10 },
                { PhysicsCollisionConfig.LayerVisionOccluder, 11 },
                { PhysicsCollisionConfig.LayerSoundOccluder, 12 },
                { PhysicsCollisionConfig.LayerHideSpotTrigger, 13 },
                { PhysicsCollisionConfig.LayerPickupTrigger, 14 }
            };

            var config = new PhysicsCollisionConfig();

            // Act
            config.Initialize(layerName => mockLayerTable.TryGetValue(layerName, out int index) ? index : -1);

            // Assert
            Assert.That(config.IsInitialized, Is.True);
            Assert.That(config.WorldLayer, Is.EqualTo(10));
            Assert.That(config.VisionOccluderLayer, Is.EqualTo(11));
            Assert.That(config.SoundOccluderLayer, Is.EqualTo(12));

            int expectedE20Mask = (1 << 10) | (1 << 11) | (1 << 12);
            Assert.That(config.E20Mask, Is.EqualTo(expectedE20Mask));
            Assert.That(config.HideSpotContainmentMask, Is.EqualTo(1 << 13));
            Assert.That(config.PickupMask, Is.EqualTo(1 << 14));
        }

        [Test]
        public void test_physics_config_missing_layer_throws_fail_closed_exception()
        {
            // Arrange: Missing "SoundOccluder" layer returns -1
            var mockLayerTable = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { PhysicsCollisionConfig.LayerPlayer, 8 },
                { PhysicsCollisionConfig.LayerGuard, 9 },
                { PhysicsCollisionConfig.LayerWorld, 10 },
                { PhysicsCollisionConfig.LayerVisionOccluder, 11 },
                // SoundOccluder is missing
                { PhysicsCollisionConfig.LayerHideSpotTrigger, 13 },
                { PhysicsCollisionConfig.LayerPickupTrigger, 14 }
            };

            var config = new PhysicsCollisionConfig();

            // Act & Assert
            var ex = Assert.Throws<PhysicsLayerMissingException>(() =>
            {
                config.Initialize(layerName => mockLayerTable.TryGetValue(layerName, out int index) ? index : -1);
            });

            Assert.That(ex.MissingLayerName, Is.EqualTo(PhysicsCollisionConfig.LayerSoundOccluder));
        }

        [Test]
        public void test_physics_config_e20_mask_strictly_excludes_player_guard_and_triggers()
        {
            // Arrange
            var mockLayerTable = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { PhysicsCollisionConfig.LayerPlayer, 8 },
                { PhysicsCollisionConfig.LayerGuard, 9 },
                { PhysicsCollisionConfig.LayerWorld, 10 },
                { PhysicsCollisionConfig.LayerVisionOccluder, 11 },
                { PhysicsCollisionConfig.LayerSoundOccluder, 12 },
                { PhysicsCollisionConfig.LayerHideSpotTrigger, 13 },
                { PhysicsCollisionConfig.LayerPickupTrigger, 14 }
            };

            var config = new PhysicsCollisionConfig();
            config.Initialize(layerName => mockLayerTable.TryGetValue(layerName, out int index) ? index : -1);

            // Act & Assert
            int playerMask = 1 << config.PlayerLayer;
            int guardMask = 1 << config.GuardLayer;
            int hideSpotTriggerMask = 1 << config.HideSpotTriggerLayer;
            int pickupTriggerMask = 1 << config.PickupTriggerLayer;

            Assert.That((config.E20Mask & playerMask), Is.EqualTo(0), "E20Mask must not hit Player layer");
            Assert.That((config.E20Mask & guardMask), Is.EqualTo(0), "E20Mask must not hit Guard layer");
            Assert.That((config.E20Mask & hideSpotTriggerMask), Is.EqualTo(0), "E20Mask must not hit HideSpotTrigger layer");
            Assert.That((config.E20Mask & pickupTriggerMask), Is.EqualTo(0), "E20Mask must not hit PickupTrigger layer");
        }

        [Test]
        public void test_physics_config_global_invariants_asserted()
        {
            // Arrange: Force incorrect settings
            UnityEngine.Physics.autoSyncTransforms = true;
            UnityEngine.Physics.queriesHitBackfaces = true;

            // Act: Assert invariants
            PhysicsCollisionConfig.AssertGlobalPhysicsInvariants();

            // Assert
            Assert.That(UnityEngine.Physics.autoSyncTransforms, Is.False, "autoSyncTransforms must be false");
            Assert.That(UnityEngine.Physics.queriesHitBackfaces, Is.False, "queriesHitBackfaces must be false");
        }

        [Test]
        public void test_wall_thickness_validator_rejects_sub_10cm_box_collider()
        {
            // Arrange
            var go = new GameObject("ThinWall");
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.05f, 2.0f, 2.0f); // 5cm thickness (< 10cm)

            try
            {
                // Act
                bool isValid = WallThicknessValidator.ValidateCollider(box, out string reason);

                // Assert
                Assert.That(isValid, Is.False);
                Assert.That(reason, Does.Contain("below minimum thickness"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void test_wall_thickness_validator_accepts_valid_box_collider()
        {
            // Arrange
            var go = new GameObject("ThickWall");
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.15f, 2.0f, 2.0f); // 15cm thickness (>= 10cm)

            try
            {
                // Act
                bool isValid = WallThicknessValidator.ValidateCollider(box, out string reason);

                // Assert
                Assert.That(isValid, Is.True);
                Assert.That(reason, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void test_wall_thickness_validator_ignores_trigger_volumes()
        {
            // Arrange: A thin trigger volume (e.g. HideSpot entry zone)
            var go = new GameObject("HideTrigger");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.02f, 1.0f, 1.0f); // 2cm thin, but isTrigger=true

            try
            {
                // Act
                bool isValid = WallThicknessValidator.ValidateCollider(box, out string reason);

                // Assert: Triggers are exempt from solid wall thickness rules
                Assert.That(isValid, Is.True);
                Assert.That(reason, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void test_physics_query_service_rate_limits_transform_sync_to_five_hertz()
        {
            // Arrange
            var service = new PhysicsQueryService();

            // Act & Assert
            // 1st call at t = 1.00s -> syncs
            bool sync1 = service.TrySyncTransformsBatch(1.00f);
            Assert.That(sync1, Is.True);

            // 2nd call at t = 1.05s (delta = 0.05s < 0.20s) -> rate-limited
            bool sync2 = service.TrySyncTransformsBatch(1.05f);
            Assert.That(sync2, Is.False);

            // 3rd call at t = 1.15s (delta = 0.15s < 0.20s) -> rate-limited
            bool sync3 = service.TrySyncTransformsBatch(1.15f);
            Assert.That(sync3, Is.False);

            // 4th call at t = 1.21s (delta = 0.21s >= 0.20s) -> syncs
            bool sync4 = service.TrySyncTransformsBatch(1.21f);
            Assert.That(sync4, Is.True);
        }
    }
}
