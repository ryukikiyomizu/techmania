using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public static class ResultScreenTimerContract
{
    private const string PopResultVideo = "Assets/UI/Videos/result_pop.mp4";
    private const string MainTree = "Assets/UI/MainTree.uxml";
    private const string ArcadeSessionScript = "Assets/UI/Scripts/Arcade Session.txt";

    public static void Run()
    {
        var failures = Evaluate();
        if (failures.Length > 0)
        {
            foreach (var failure in failures)
            {
                Debug.LogError("[T2 Result Timer Contract] " + failure);
            }
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("T2 Result Screen timer behavioral checks passed.");
        EditorApplication.Exit(0);
    }

    [MenuItem("Window/TECHMANIA Theme/Test Result Screen timer")]
    public static void RunInEditor()
    {
        var failures = Evaluate();
        if (failures.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        }
        Debug.Log("T2 Result Screen timer behavioral checks passed.");
    }

    public static string[] Evaluate()
    {
        var failures = new List<string>();

        var resultVideo = AssetDatabase.LoadAssetAtPath<VideoClip>(PopResultVideo);
        Check(resultVideo != null, "The Pop Result movie is missing.", failures);
        if (resultVideo != null)
        {
            Check(Math.Abs(resultVideo.length - 32.0) < 0.01,
                $"The Pop Result movie must be exactly 32 seconds; found {resultVideo.length:F6}.",
                failures);
        }

        var tree = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), MainTree));
        var timerElement = tree.Descendants().FirstOrDefault(element =>
            (string)element.Attribute("name") == "arcade-result-screen-timer");
        Check(timerElement != null, "The Result Screen timer Arcade option is missing.", failures);
        if (timerElement != null)
        {
            Check((string)timerElement.Attribute("label") == "Result Screen timer (32 seconds)",
                "The Result Screen timer does not communicate its locked 32-second duration.",
                failures);
            Check((string)timerElement.Parent?.Attribute("name") == "arcade-options-group",
                "The Result Screen timer is not grouped directly below Arcade Mode.",
                failures);
            var firstOption = timerElement.Parent?.Elements().FirstOrDefault();
            Check((string)firstOption?.Attribute("name") == "arcade-result-screen-timer",
                "The Result Screen timer is not the first option below Arcade Mode.",
                failures);
        }

        var arcadeSessionSource = File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), ArcadeSessionScript));
        try
        {
            var enabled = InvokeResultTimerScenario(arcadeSessionSource, true, true);
            Check(enabled.Tuple[0].Boolean,
                "The enabled timer is absent from the Arcade startup snapshot.", failures);
            Check(Math.Abs(enabled.Tuple[1].Number - 32.0) < 0.0001,
                "The Arcade Result timer duration is not locked to 32 seconds.", failures);
            Check(!enabled.Tuple[2].Boolean,
                "The Arcade Result timer exits before 32 seconds.", failures);
            Check(enabled.Tuple[3].Boolean,
                "The Arcade Result timer does not exit at 32 seconds.", failures);

            var timerOff = InvokeResultTimerScenario(arcadeSessionSource, true, false);
            Check(!timerOff.Tuple[3].Boolean,
                "A disabled Result timer still exits the Result screen.", failures);

            var arcadeOff = InvokeResultTimerScenario(arcadeSessionSource, false, true);
            Check(!arcadeOff.Tuple[3].Boolean,
                "The Result timer runs outside Arcade mode.", failures);
        }
        catch (Exception exception)
        {
            failures.Add("Arcade Result timer behavior is missing or invalid: " +
                exception.GetBaseException().Message);
        }

        return failures.ToArray();
    }

    private static DynValue InvokeResultTimerScenario(
        string arcadeSessionSource,
        bool arcadeEnabled,
        bool timerEnabled)
    {
        var script = new Script(CoreModules.Preset_SoftSandbox);
        var prelude = $@"
themeOptions = {{
    GetBoolWithDefault = function(key, default)
        if key == ""ArcadeMode"" then return {LuaBool(arcadeEnabled)} end
        if key == ""ArcadeResultScreenTimer"" then return {LuaBool(timerEnabled)} end
        return default
    end,
    GetString = function(_) return """" end
}}
";
        script.DoString(prelude);
        script.DoString(arcadeSessionSource, null, "Arcade Session.txt");
        return script.DoString(@"
arcadeSession.CaptureStartupSnapshot()
return arcadeSession.active.resultScreenTimer,
    arcadeSession.ResultScreenDurationSeconds(),
    arcadeSession.ShouldAutoExitResult(31.999),
    arcadeSession.ShouldAutoExitResult(32.0)
");
    }

    private static string LuaBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static void Check(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
