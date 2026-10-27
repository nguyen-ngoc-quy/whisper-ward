using System;
using UnityEngine;

namespace WhisperWard.Core.Rendering
{
    /// <summary>
    /// PBR Material parameters and color palettes for the Cold Watch subterranean facility.
    /// Provides physically based rendering properties (Albedo, Metallic, Smoothness, Emission)
    /// conforming to high-end URP forward+ aesthetics.
    /// </summary>
    [Serializable]
    public class FacilityVisualPaletteConfig
    {
        [Header("Floor PBR Material")]
        public Color FloorBaseColor = new Color(0.12f, 0.14f, 0.16f, 1.0f); // #1E232A Dark Slate
        [Range(0f, 1f)] public float FloorMetallic = 0.40f;
        [Range(0f, 1f)] public float FloorSmoothness = 0.65f;

        [Header("Wall PBR Material")]
        public Color WallBaseColor = new Color(0.09f, 0.10f, 0.12f, 1.0f); // #16191F Tactical Navy
        [Range(0f, 1f)] public float WallMetallic = 0.10f;
        [Range(0f, 1f)] public float WallSmoothness = 0.20f;

        [Header("Hazard Trim (Perimeter & Gates)")]
        public Color HazardYellow = new Color(1.0f, 0.80f, 0.0f, 1.0f); // #FFCC00
        public Color HazardBlack = new Color(0.08f, 0.08f, 0.08f, 1.0f);

        [Header("Character Visors & Indicators")]
        public Color PlayerVisorColor = new Color(0.0f, 0.90f, 1.0f, 1.0f); // #00E5FF Stealth Cyan
        public float VisorEmissionIntensity = 2.50f;

        public Color GuardPatrolColor = new Color(0.0f, 1.0f, 0.40f, 1.0f); // #00FF66
        public Color GuardInvestigateColor = new Color(1.0f, 0.72f, 0.0f, 1.0f); // #FFB800
        public Color GuardChaseColor = new Color(1.0f, 0.10f, 0.20f, 1.0f); // #FF1A33
    }
}
