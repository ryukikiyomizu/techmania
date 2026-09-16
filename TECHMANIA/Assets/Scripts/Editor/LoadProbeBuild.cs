using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class LoadProbeBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("TECHMANIA_PROBE_BUILD_DIR");
        if (string.IsNullOrEmpty(output))
            throw new InvalidOperationException("TECHMANIA_PROBE_BUILD_DIR is required.");
        Directory.CreateDirectory(output);
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
            .Select(s => s.path).ToArray();
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(output, "TECHMANIA.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { "TECHMANIA_HUMAN_PLAYTESTER" }
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Probe build failed: " + report.summary.result +
                ", errors=" + report.summary.totalErrors);
    }
}
