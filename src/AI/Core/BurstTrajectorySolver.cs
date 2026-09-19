using System;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable input for the F4 unequal-height Burst trajectory oracle. Heights
    /// are world-space Y values; the solver does not apply a runtime height
    /// correction or a content range-band decision.
    /// </summary>
    public struct BurstTrajectoryInput
    {
        /// <summary>Creates one trajectory input without silently clamping values.</summary>
        public BurstTrajectoryInput(float initialSpeed, float angleDegrees,
            float releaseHeight, float landingHeight, float gravity)
        {
            InitialSpeed = initialSpeed;
            AngleDegrees = angleDegrees;
            ReleaseHeight = releaseHeight;
            LandingHeight = landingHeight;
            Gravity = gravity;
        }

        /// <summary>Initial speed magnitude in metres per second.</summary>
        public float InitialSpeed { get; }

        /// <summary>Launch angle in degrees, in the authored (0, 45] domain.</summary>
        public float AngleDegrees { get; }

        /// <summary>World-space release height in metres.</summary>
        public float ReleaseHeight { get; }

        /// <summary>World-space landing height in metres.</summary>
        public float LandingHeight { get; }

        /// <summary>Positive gravity magnitude in metres per second squared.</summary>
        public float Gravity { get; }
    }

    /// <summary>Terminal classification returned by the pure F4 solver.</summary>
    public enum BurstTrajectoryStatus
    {
        Valid,
        InvalidDomain,
        NegativeDiscriminant,
        NoStrictlyPositiveRoot
    }

    /// <summary>
    /// Complete F4 calculation record. A valid result accepts a repeated root when
    /// the discriminant is zero and that root is strictly positive.
    /// </summary>
    public struct BurstTrajectoryResult
    {
        private BurstTrajectoryResult(BurstTrajectoryStatus status,
            float deltaY, float discriminant, float lowerRoot,
            float upperRoot, float selectedRoot, float horizontalRange)
        {
            Status = status;
            DeltaY = deltaY;
            Discriminant = discriminant;
            LowerRoot = lowerRoot;
            UpperRoot = upperRoot;
            SelectedRoot = selectedRoot;
            HorizontalRange = horizontalRange;
        }

        /// <summary>Creates an invalid result while preserving calculated values.</summary>
        public static BurstTrajectoryResult Create(BurstTrajectoryStatus status,
            float deltaY, float discriminant, float lowerRoot, float upperRoot,
            float selectedRoot, float horizontalRange)
        {
            return new BurstTrajectoryResult(status, deltaY, discriminant,
                lowerRoot, upperRoot, selectedRoot, horizontalRange);
        }

        /// <summary>Whether the input produced a strictly positive landing root.</summary>
        public bool IsValid { get { return Status == BurstTrajectoryStatus.Valid; } }

        /// <summary>Solver terminal classification.</summary>
        public BurstTrajectoryStatus Status { get; }

        /// <summary>World-space height delta, releaseHeight minus landingHeight.</summary>
        public float DeltaY { get; }

        /// <summary>Ballistic discriminant in metres squared per second squared.</summary>
        public float Discriminant { get; }

        /// <summary>Lower quadratic root in seconds, or zero when unavailable.</summary>
        public float LowerRoot { get; }

        /// <summary>Upper quadratic root in seconds, or zero when unavailable.</summary>
        public float UpperRoot { get; }

        /// <summary>Minimum strictly positive root in seconds when valid.</summary>
        public float SelectedRoot { get; }

        /// <summary>Horizontal range at the selected root in metres.</summary>
        public float HorizontalRange { get; }
    }

    /// <summary>Injectable contract for the pure Burst trajectory calculation.</summary>
    public interface IBurstTrajectorySolver
    {
        /// <summary>Solves F4 without applying a content range-band correction.</summary>
        BurstTrajectoryResult Solve(BurstTrajectoryInput input);
    }

    /// <summary>
    /// Pure F4 solver shared by runtime, preview, replay, and LevelFixture adapters.
    /// It accepts a positive repeated root at D = 0 and rejects only invalid
    /// domains, negative discriminants, and roots that are not strictly positive.
    /// </summary>
    public sealed class BurstTrajectorySolver : IBurstTrajectorySolver
    {
        /// <summary>Solves the unequal-height F4 trajectory deterministically.</summary>
        public BurstTrajectoryResult Solve(BurstTrajectoryInput input)
        {
            if (!IsFinitePositive(input.InitialSpeed)
                || !IsFinite(input.AngleDegrees)
                || input.AngleDegrees <= 0f
                || input.AngleDegrees > 45f
                || !IsFinite(input.ReleaseHeight)
                || !IsFinite(input.LandingHeight)
                || !IsFinitePositive(input.Gravity))
            {
                return BurstTrajectoryResult.Create(
                    BurstTrajectoryStatus.InvalidDomain,
                    0f, 0f, 0f, 0f, 0f, 0f);
            }

            double angleRadians = input.AngleDegrees * Math.PI / 180.0;
            double speed = input.InitialSpeed;
            double gravity = input.Gravity;
            double deltaY = (double)input.ReleaseHeight - input.LandingHeight;
            double horizontalSpeed = speed * Math.Cos(angleRadians);
            double verticalSpeed = speed * Math.Sin(angleRadians);
            double discriminant = verticalSpeed * verticalSpeed
                + 2.0 * gravity * deltaY;

            if (double.IsNaN(discriminant) || double.IsInfinity(discriminant))
            {
                return BurstTrajectoryResult.Create(
                    BurstTrajectoryStatus.InvalidDomain,
                    ToFloat(deltaY), 0f, 0f, 0f, 0f, 0f);
            }

            if (discriminant < 0.0)
            {
                return BurstTrajectoryResult.Create(
                    BurstTrajectoryStatus.NegativeDiscriminant,
                    ToFloat(deltaY), ToFloat(discriminant), 0f, 0f, 0f, 0f);
            }

            double rootDiscriminant = Math.Sqrt(discriminant);
            double lowerRoot = (verticalSpeed - rootDiscriminant) / gravity;
            double upperRoot = (verticalSpeed + rootDiscriminant) / gravity;
            double selectedRoot = SelectMinimumStrictlyPositive(lowerRoot, upperRoot);
            if (selectedRoot <= 0.0 || double.IsNaN(selectedRoot)
                || double.IsInfinity(selectedRoot))
            {
                return BurstTrajectoryResult.Create(
                    BurstTrajectoryStatus.NoStrictlyPositiveRoot,
                    ToFloat(deltaY), ToFloat(discriminant), ToFloat(lowerRoot),
                    ToFloat(upperRoot), 0f, 0f);
            }

            double horizontalRange = horizontalSpeed * selectedRoot;
            if (horizontalRange <= 0.0 || double.IsNaN(horizontalRange)
                || double.IsInfinity(horizontalRange))
            {
                return BurstTrajectoryResult.Create(
                    BurstTrajectoryStatus.InvalidDomain,
                    ToFloat(deltaY), ToFloat(discriminant), ToFloat(lowerRoot),
                    ToFloat(upperRoot), ToFloat(selectedRoot),
                    ToFloat(horizontalRange));
            }

            return BurstTrajectoryResult.Create(
                BurstTrajectoryStatus.Valid,
                ToFloat(deltaY), ToFloat(discriminant), ToFloat(lowerRoot),
                ToFloat(upperRoot), ToFloat(selectedRoot),
                ToFloat(horizontalRange));
        }

        private static double SelectMinimumStrictlyPositive(double lowerRoot,
            double upperRoot)
        {
            bool lowerPositive = lowerRoot > 0.0;
            bool upperPositive = upperRoot > 0.0;
            if (lowerPositive && upperPositive)
                return Math.Min(lowerRoot, upperRoot);
            if (lowerPositive) return lowerRoot;
            if (upperPositive) return upperRoot;
            return 0.0;
        }

        private static float ToFloat(double value)
        {
            if (value > float.MaxValue) return float.MaxValue;
            if (value < -float.MaxValue) return -float.MaxValue;
            return (float)value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }
    }
}
