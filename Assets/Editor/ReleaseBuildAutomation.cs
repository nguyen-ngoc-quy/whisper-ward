#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WhisperWard.Editor
{
    /// <summary>
    /// Automated build pipeline for Whisper Ward conforming to Tuần 8 Release Packaging.
    /// Provides batchmode-executable build routines for:
    /// - Windows Standalone x64
    /// - WebGL Portfolio Demo
    /// Configures code stripping, scene inclusion, and compression profiles.
    /// </summary>
    /// <example>
    /// <code>
    /// Unity -quit -batchmode -projectPath . -executeMethod WhisperWard.Editor.ReleaseBuildAutomation.BuildWindowsStandalone
    /// </code>
    /// </example>
    public static class ReleaseBuildAutomation
    {
        public const string DefaultScenePath = "Assets/Scenes/CorePlayground.unity";
        public const string WindowsDefaultOutputDir = "Builds/Windows";
        public const string WindowsDefaultExeName = "WhisperWard.exe";
        public const string WebGLDefaultOutputDir = "Builds/WebGL";

        /// <summary>
        /// Validates scene inclusion list.
        /// </summary>
        public static bool ValidateSceneList(string[] scenes, out string error)
        {
            if (scenes == null || scenes.Length == 0)
            {
                error = "Build scene list is empty. Must include at least the core level scene.";
                return false;
            }

            bool containsCoreScene = false;
            foreach (string scene in scenes)
            {
                if (string.Equals(scene, DefaultScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    containsCoreScene = true;
                    break;
                }
            }

            if (!containsCoreScene)
            {
                error = $"Scene list does not contain primary game scene: {DefaultScenePath}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Creates BuildPlayerOptions for Windows x64 release build.
        /// </summary>
        public static BuildPlayerOptions GetWindowsBuildOptions(string customExePath = null)
        {
            string exePath = customExePath ?? Path.Combine(WindowsDefaultOutputDir, WindowsDefaultExeName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { DefaultScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None // Full release build
            };

            return options;
        }

        /// <summary>
        /// Creates BuildPlayerOptions for WebGL demo release build.
        /// </summary>
        public static BuildPlayerOptions GetWebGLBuildOptions(string customOutputDir = null)
        {
            string outputDir = customOutputDir ?? WebGLDefaultOutputDir;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { DefaultScenePath },
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            return options;
        }

        [MenuItem("Whisper Ward/Build/Windows Standalone x64")]
        public static void BuildWindowsStandalone()
        {
            BuildPlayerOptions options = GetWindowsBuildOptions();
            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName));

            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Minimal);

            Debug.Log($"[ReleaseBuild] Starting Windows build to {options.locationPathName}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            LogBuildResult(report, "Windows Standalone");
        }

        [MenuItem("Whisper Ward/Build/WebGL Portfolio Demo")]
        public static void BuildWebGL()
        {
            BuildPlayerOptions options = GetWebGLBuildOptions();
            Directory.CreateDirectory(options.locationPathName);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            Debug.Log($"[ReleaseBuild] Starting WebGL build to {options.locationPathName}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            LogBuildResult(report, "WebGL");
        }

        private static void LogBuildResult(BuildReport report, string platformName)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ReleaseBuild] {platformName} build succeeded in {report.summary.totalTime.TotalSeconds:F1}s! Output size: {report.summary.totalSize / (1024 * 1024):F1} MB.");
            }
            else
            {
                Debug.LogError($"[ReleaseBuild] {platformName} build failed with {report.summary.totalErrors} errors!");
            }
        }
    }
}
#endif
