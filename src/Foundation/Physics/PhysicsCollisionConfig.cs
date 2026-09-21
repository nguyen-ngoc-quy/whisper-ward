using System;
using UnityEngine;

namespace WhisperWard.Foundation.Physics
{
    /// <summary>
    /// Custom fail-closed exception thrown when required physics layers are missing from Unity TagManager.
    /// Conforms to ADR-0002.
    /// </summary>
    public sealed class PhysicsLayerMissingException : Exception
    {
        public string MissingLayerName { get; }

        public PhysicsLayerMissingException(string layerName)
            : base($"[PhysicsCollisionConfig] Required physics layer '{layerName}' is not defined in TagManager (LayerMask.NameToLayer returned -1). Fail-closed.")
        {
            MissingLayerName = layerName;
        }
    }

    /// <summary>
    /// Authority for physics layer masks, global invariant assertion, and E20 collision configuration.
    /// Conforms to ADR-0002 and TR-FOUND-005.
    /// </summary>
    public sealed class PhysicsCollisionConfig
    {
        public const string LayerPlayer = "Player";
        public const string LayerGuard = "Guard";
        public const string LayerWorld = "World";
        public const string LayerVisionOccluder = "VisionOccluder";
        public const string LayerSoundOccluder = "SoundOccluder";
        public const string LayerHideSpotTrigger = "HideSpotTrigger";
        public const string LayerPickupTrigger = "PickupTrigger";

        public const float MinimumWallThickness = 0.10f;
        public const float EmbeddedOriginCheckRadius = 0.05f;

        public int PlayerLayer { get; private set; }
        public int GuardLayer { get; private set; }
        public int WorldLayer { get; private set; }
        public int VisionOccluderLayer { get; private set; }
        public int SoundOccluderLayer { get; private set; }
        public int HideSpotTriggerLayer { get; private set; }
        public int PickupTriggerLayer { get; private set; }

        public int E20Mask { get; private set; }
        public int HideSpotContainmentMask { get; private set; }
        public int PickupMask { get; private set; }

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Resolves and asserts all 7 required layers and builds fail-closed layer masks.
        /// Accepts a resolver delegate for pure deterministic unit testing.
        /// </summary>
        public void Initialize(Func<string, int> layerResolver = null)
        {
            var resolver = layerResolver ?? LayerMask.NameToLayer;

            PlayerLayer = ResolveRequiredLayer(resolver, LayerPlayer);
            GuardLayer = ResolveRequiredLayer(resolver, LayerGuard);
            WorldLayer = ResolveRequiredLayer(resolver, LayerWorld);
            VisionOccluderLayer = ResolveRequiredLayer(resolver, LayerVisionOccluder);
            SoundOccluderLayer = ResolveRequiredLayer(resolver, LayerSoundOccluder);
            HideSpotTriggerLayer = ResolveRequiredLayer(resolver, LayerHideSpotTrigger);
            PickupTriggerLayer = ResolveRequiredLayer(resolver, LayerPickupTrigger);

            // Assemble E20 Solid Occlusion Mask
            E20Mask = (1 << WorldLayer) | (1 << VisionOccluderLayer) | (1 << SoundOccluderLayer);

            // Validate that E20 Mask does NOT contain forbidden player, guard, or trigger bits
            int forbiddenBits = (1 << PlayerLayer) | (1 << GuardLayer) | (1 << HideSpotTriggerLayer) | (1 << PickupTriggerLayer);
            if ((E20Mask & forbiddenBits) != 0)
            {
                throw new InvalidOperationException($"[PhysicsCollisionConfig] E20Mask contains forbidden layer bits (Player, Guard, or Triggers). Mask: {E20Mask}");
            }

            // Assemble dedicated trigger profiles
            HideSpotContainmentMask = 1 << HideSpotTriggerLayer;
            PickupMask = 1 << PickupTriggerLayer;

            IsInitialized = true;
        }

        /// <summary>
        /// Asserts global PhysX configuration invariants defined in ADR-0002.
        /// </summary>
        public static void AssertGlobalPhysicsInvariants()
        {
            UnityEngine.Physics.autoSyncTransforms = false;
            UnityEngine.Physics.queriesHitBackfaces = false;
        }

        private static int ResolveRequiredLayer(Func<string, int> resolver, string layerName)
        {
            int index = resolver(layerName);
            if (index < 0 || index > 31)
            {
                throw new PhysicsLayerMissingException(layerName);
            }
            return index;
        }
    }
}
