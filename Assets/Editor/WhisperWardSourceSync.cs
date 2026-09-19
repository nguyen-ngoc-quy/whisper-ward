using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Unity's generated compile tree synchronized with the canonical AI
/// source under src/AI without editing Unity-generated project files.
/// </summary>
[InitializeOnLoad]
public static class WhisperWardSourceSync
{
    private const string GeneratedRoot = "WhisperWardAI";
    private const string RuntimeAssemblyName = "WhisperWard.AI";
    private const string TestsAssemblyName = "WhisperWard.AI.Tests";
    private static bool _syncing;

    static WhisperWardSourceSync()
    {
        EditorApplication.delayCall += Sync;
    }

    [MenuItem("Whisper Ward/Sync Canonical AI Source")]
    public static void Sync()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceRoot = Path.Combine(projectRoot, "src", "AI");
            string generatedRoot = Path.Combine(Application.dataPath, GeneratedRoot);
            string runtimeRoot = Path.Combine(generatedRoot, "Runtime");
            string testsRoot = Path.Combine(generatedRoot, "Tests");

            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError("Whisper Ward source sync failed: missing " + sourceRoot);
                return;
            }

            bool changed = SyncRuntime(sourceRoot, runtimeRoot);
            changed |= SyncTests(Path.Combine(sourceRoot, "Testing"), testsRoot);
            changed |= WriteIfChanged(
                Path.Combine(runtimeRoot, RuntimeAssemblyName + ".asmdef"),
                RuntimeAssemblyDefinition);
            changed |= WriteIfChanged(
                Path.Combine(testsRoot, TestsAssemblyName + ".asmdef"),
                TestsAssemblyDefinition);

            if (changed)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Whisper Ward canonical AI source synchronized from src/AI.");
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private static bool SyncRuntime(string sourceRoot, string runtimeRoot)
    {
        bool changed = SyncFiles(sourceRoot, runtimeRoot, "Testing");
        string testingRoot = Path.Combine(sourceRoot, "Testing");
        changed |= CopyIfChanged(Path.Combine(testingRoot, "DecisionTap.cs"),
            Path.Combine(runtimeRoot, "DecisionTap.cs"));
        changed |= CopyIfChanged(Path.Combine(testingRoot, "PerceptionDriver.cs"),
            Path.Combine(runtimeRoot, "PerceptionDriver.cs"));
        return changed;
    }

    private static bool SyncTests(string sourceRoot, string testsRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            Debug.LogError("Whisper Ward source sync failed: missing " + sourceRoot);
            return false;
        }

        bool changed = SyncFiles(sourceRoot, testsRoot, null);
        changed |= DeleteIfPresent(Path.Combine(testsRoot, "DecisionTap.cs"));
        changed |= DeleteIfPresent(Path.Combine(testsRoot, "PerceptionDriver.cs"));
        return changed;
    }

    private static bool DeleteIfPresent(string path)
    {
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private static bool SyncFiles(string sourceRoot, string destinationRoot,
        string excludedDirectoryName)
    {
        Directory.CreateDirectory(destinationRoot);
        bool changed = false;

        string[] sourceFiles = Directory.GetFiles(sourceRoot, "*.cs",
            SearchOption.AllDirectories);
        foreach (string sourceFile in sourceFiles)
        {
            string relative = Path.GetRelativePath(sourceRoot, sourceFile);
            if (excludedDirectoryName != null
                && relative.StartsWith(excludedDirectoryName + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destinationFile = Path.Combine(destinationRoot, relative);
            changed |= CopyIfChanged(sourceFile, destinationFile);
        }

        string[] destinationFiles = Directory.GetFiles(destinationRoot, "*.cs",
            SearchOption.AllDirectories);
        foreach (string destinationFile in destinationFiles)
        {
            string relative = Path.GetRelativePath(destinationRoot, destinationFile);
            if (!File.Exists(Path.Combine(sourceRoot, relative)))
            {
                File.Delete(destinationFile);
                changed = true;
            }
        }

        return changed;
    }

    private static bool CopyIfChanged(string sourceFile, string destinationFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
        if (File.Exists(destinationFile)
            && File.ReadAllText(sourceFile) == File.ReadAllText(destinationFile))
        {
            return false;
        }

        File.Copy(sourceFile, destinationFile, true);
        return true;
    }

    private static bool WriteIfChanged(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path) && File.ReadAllText(path) == content)
            return false;

        File.WriteAllText(path, content);
        return true;
    }

    private const string RuntimeAssemblyDefinition = @"{
  ""name"": ""WhisperWard.AI"",
  ""rootNamespace"": ""WhisperWard.AI"",
  ""references"": [],
  ""includePlatforms"": [],
  ""excludePlatforms"": [],
  ""allowUnsafeCode"": false,
  ""overrideReferences"": false,
  ""precompiledReferences"": [],
  ""autoReferenced"": true,
  ""defineConstraints"": [],
  ""versionDefines"": [],
  ""noEngineReferences"": false
}";

    private const string TestsAssemblyDefinition = @"{
  ""name"": ""WhisperWard.AI.Tests"",
  ""rootNamespace"": ""WhisperWard.AI.Testing"",
  ""references"": [
    ""WhisperWard.AI""
  ],
  ""includePlatforms"": [
    ""Editor""
  ],
  ""excludePlatforms"": [],
  ""allowUnsafeCode"": false,
  ""overrideReferences"": false,
  ""precompiledReferences"": [],
  ""optionalUnityReferences"": [
    ""TestAssemblies""
  ],
  ""autoReferenced"": false,
  ""defineConstraints"": [],
  ""versionDefines"": [],
  ""noEngineReferences"": false,
  ""testAssemblies"": true
}";
}
