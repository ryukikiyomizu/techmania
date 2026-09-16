using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HumanPlaytesterBuild
{
    [MenuItem("Window/TECHMANIA Theme/Build human playtester")]
    public static void BuildTestPlayer()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled build scenes.");

        string projectRoot = Directory.GetParent(
            Application.dataPath).FullName;
        string testBuildFolder = Path.GetFullPath(Path.Combine(
            projectRoot, "..", "..", "..", "_TestBuild3"));
        Directory.CreateDirectory(testBuildFolder);
        string outputExe = Path.Combine(testBuildFolder, "TECHMANIA.exe");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputExe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CleanBuildCache,
            extraScriptingDefines = new[]
            {
                "TECHMANIA_HUMAN_PLAYTESTER"
            }
        };

        Debug.Log($"Building human playtester to {outputExe}.");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception(
                $"Human playtester build failed: result={summary.result}, " +
                $"errors={summary.totalErrors}, warnings={summary.totalWarnings}.");
        }

        Debug.Log($"Human playtester build succeeded: output={summary.outputPath}, " +
            $"size={summary.totalSize}, errors={summary.totalErrors}, " +
            $"warnings={summary.totalWarnings}.");
    }
}
