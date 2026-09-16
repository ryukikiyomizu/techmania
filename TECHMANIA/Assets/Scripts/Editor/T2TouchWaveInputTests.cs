using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

public static class T2TouchWaveInputTests
{
    private const string UtilityScript = "Assets/UI/Scripts/Utility.txt";
    private const string TouchWaveScript = "Assets/UI/Scripts/Touch Wave Input.txt";
    private const string TitleScript = "Assets/UI/Scripts/Title Screen.txt";
    private const string ModeScript = "Assets/UI/Scripts/Select Mode Screen.txt";
    private const string GameScript = "Assets/UI/Scripts/Game Screen.txt";
    private const string ResultScript = "Assets/UI/Scripts/Result Screen.txt";
    private const string MainScript = "Assets/UI/MainScript.txt";
    private const string MainTree = "Assets/UI/MainTree.uxml";

    public static void Run()
    {
        var failures = Evaluate();
        if (failures.Length > 0)
        {
            foreach (var failure in failures)
            {
                Debug.LogError("[T2 Touch Wave Test] " + failure);
            }
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[T2 Touch Wave Test] Touch-wave input contract passed.");
        EditorApplication.Exit(0);
    }

    [MenuItem("Window/TECHMANIA Theme/Test T2 touch waves")]
    public static void RunInEditor()
    {
        var failures = Evaluate();
        if (failures.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        }
        Debug.Log("[T2 Touch Wave Test] Touch-wave input contract passed.");
    }

    public static string[] Evaluate()
    {
        var failures = new List<string>();
        var utilityPath = FullPath(UtilityScript);
        var helperPath = FullPath(TouchWaveScript);

        Check(File.Exists(helperPath),
            "The shared Touch Wave Input helper is missing.", failures);

        CheckScreenWiring(TitleScript, "titleScreen.screen", "title-touch-trails", failures);
        CheckScreenWiring(ModeScript, "selectModeScreen.screen", "mode-touch-trails", failures);
        CheckExcludedScreen(GameScript, failures);
        CheckExcludedScreen(ResultScript, failures);
        Check(File.ReadAllText(FullPath(MainScript)).Contains(
                "tm.ExecuteScriptFromTheme(\"Assets/UI/Scripts/Touch Wave Input.txt\")"),
            "MainScript does not load the shared touch-wave helper.", failures);
        CheckAdditivePools(failures);
        CheckEventPositionCallSites(failures);

        try
        {
            var utility = File.ReadAllText(utilityPath);
            var marker = utility.IndexOf("touchTrail = {", StringComparison.Ordinal);
            Check(marker >= 0, "Utility.txt no longer defines touchTrail.", failures);
            if (marker >= 0)
            {
                var script = new Script(CoreModules.Preset_SoftSandbox);
                script.DoString(@"
tm = {
    io = {
        LoadTextureFromTheme = function(path) return path end
    },
    StartCoroutine = function(_) return 1 end
}
unity = { time = { time = 4, deltaTime = 1 / 60 } }
StyleLength = function(value) return value end
StyleFloat = function(value) return value end
StyleColor = function(r, g, b, a) return { r, g, b, a } end
eventType = {
    PointerDown = ""PointerDown"",
    PointerMove = ""PointerMove"",
    PointerUp = ""PointerUp"",
    PointerCancel = ""PointerCancel"",
    PointerLeave = ""PointerLeave""
}

children = {}
for i = 1, 8 do
    children[i] = { style = {} }
end
container = {
    name = ""test-touch-trails"",
    childCount = 8,
    worldBound = { xMin = 100, yMin = 50, width = 640, height = 384 },
    contentRect = { xMin = 0, yMin = 0, width = 1280, height = 768 },
    Children = function() return children end
}
registered = {}
screen = {
    Q = function(name)
        if name == ""test-touch-trails"" then return container end
        return nil
    end,
    RegisterCallback = function(kind, callback)
        registered[kind] = callback
    end
}
event = {
    position = { x = 260, y = 170 },
    localPosition = { x = 999, y = 999 }
}
");
                script.DoString(utility.Substring(marker), null, UtilityScript);

                // Prove the existing utility still rotates its fixed pool and
                // maintains active/up state independently of the screen glue.
                var utilityResult = script.DoString(@"
touchTrail.LoadWaterdropTextures()
touchTrail.PlayWaterdrop(container, event)
local firstRotated = touchTrail.tokens[""test-touch-trails-1""] == 1
-- The stub's worldBound is 640x384 at (100, 50) while contentRect is the
-- authored 1280x768, so panel (260, 170) scales to local (320, 240). A 420px
-- wave centred there lands at left 110, top 30. Pinning the post-conversion
-- values is the point of this check: 132/42 was raw (260-128, 170-128), which
-- passed even when the panel-to-local conversion was missing entirely.
local firstCentered = children[1].style.left == 110 and
    children[1].style.top == 30
local firstSized = children[1].style.width == 420 and
    children[1].style.height == 420
touchTrail.PlayWaterdrop(container, event)
local secondRotated = touchTrail.tokens[""test-touch-trails-2""] == 1
touchTrail.PointerDown(container, event)
local activeAfterDown = touchTrail.active[""test-touch-trails""] == true
touchTrail.PointerUp(container)
local inactiveAfterUp = touchTrail.active[""test-touch-trails""] == false
return #touchTrail.waterdropTextures, firstRotated, secondRotated,
    activeAfterDown, inactiveAfterUp, firstCentered, firstSized
");
                Check(utilityResult.Tuple[1].Boolean && utilityResult.Tuple[2].Boolean,
                    "touchTrail no longer rotates through its preallocated pool.", failures);
                Check(utilityResult.Tuple[3].Boolean && utilityResult.Tuple[4].Boolean,
                    "touchTrail pointer down/up no longer maintains active state.", failures);
                Check(utilityResult.Tuple[5].Boolean,
                    "Touch waves are not centered on the scaled panel-space pointer position.",
                    failures);
                Check(utilityResult.Tuple[6].Boolean,
                    "Touch waves do not apply their requested 420px runtime size to the pooled element.",
                    failures);

                if (File.Exists(helperPath))
                {
                    script.DoString(File.ReadAllText(helperPath), null, TouchWaveScript);
                    var helperResult = script.DoString(@"
touchWaveInput.Register(screen, ""test-touch-trails"")
local firstPath = touchTrail.waterdropTextures[1]
local frameZeroSkipped = #touchTrail.waterdropTextures == 25 and
    string.find(firstPath, ""wave_00001.png"", 1, true) ~= nil
local brightPool = true
for i = 1, #children do
    local tint = children[i].style.unityBackgroundImageTintColor
    brightPool = brightPool and tint ~= nil and tint[1] == 1 and tint[2] == 1 and
        tint[3] == 1 and tint[4] == 1
end
local callbacksPresent = registered.PointerDown ~= nil and
    registered.PointerMove ~= nil and registered.PointerUp ~= nil and
    registered.PointerCancel ~= nil
registered.PointerDown(nil, event)
local activeAfterRegisteredDown = touchTrail.active[""test-touch-trails""] == true
local cursorAfterDown = touchTrail.cursor
local tokenAfterDown = touchTrail.tokens[""test-touch-trails-3""]
unity.time.time = unity.time.time + touchTrail.trailInterval + 1
registered.PointerMove(nil, event)
local movementDidNotSpawn = touchTrail.cursor == cursorAfterDown and
    touchTrail.tokens[""test-touch-trails-3""] == tokenAfterDown
registered.PointerCancel(nil, event)
local inactiveAfterCancel = touchTrail.active[""test-touch-trails""] == false
return frameZeroSkipped, brightPool, callbacksPresent,
    activeAfterRegisteredDown, inactiveAfterCancel, movementDidNotSpawn
");
                    Check(helperResult.Tuple[0].Boolean,
                        "The opaque-black wave_00000 pad frame is not skipped.", failures);
                    Check(helperResult.Tuple[1].Boolean,
                        "The touch-wave pool is not additive with an opaque white tint.", failures);
                    Check(helperResult.Tuple[2].Boolean,
                        "The full down/move/up/cancel pointer lifecycle is not registered.", failures);
                    Check(helperResult.Tuple[3].Boolean && helperResult.Tuple[4].Boolean,
                        "Registered down/cancel callbacks do not update touchTrail state.", failures);
                    Check(helperResult.Tuple[5].Boolean,
                        "PointerMove spawns a waterdrop while the user is sliding.", failures);
                }
                else
                {
                    Check(utilityResult.Tuple[0].Number == 25,
                        "The opaque-black wave_00000 pad frame is not skipped.", failures);
                }
            }
        }
        catch (Exception exception)
        {
            failures.Add("Touch-wave behavior could not execute: " +
                exception.GetBaseException().Message);
        }

        return failures.ToArray();
    }

    private static void CheckScreenWiring(
        string path, string screenExpression, string poolName,
        ICollection<string> failures)
    {
        var source = File.ReadAllText(FullPath(path));
        Check(source.Contains($"touchWaveInput.Register({screenExpression}, \"{poolName}\")"),
            $"{Path.GetFileName(path)} does not register its touch-wave pool.", failures);
    }

    private static void CheckExcludedScreen(
        string path, ICollection<string> failures)
    {
        var source = File.ReadAllText(FullPath(path));
        Check(!source.Contains("touchWaveInput.Register("),
            $"{Path.GetFileName(path)} must not enable menu touch waves.", failures);
    }

    private static void CheckAdditivePools(ICollection<string> failures)
    {
        string source = File.ReadAllText(FullPath(MainTree));
        foreach (string prefix in new[] { "title-touch-trail", "mode-touch-trail" })
        {
            for (int i = 1; i <= 8; i++)
            {
                string marker = $"name=\"{prefix}-{i}\"";
                int start = source.IndexOf(marker, StringComparison.Ordinal);
                int end = start < 0 ? -1 : source.IndexOf("/>", start,
                    StringComparison.Ordinal);
                string element = start >= 0 && end > start
                    ? source.Substring(start, end - start)
                    : string.Empty;
                Check(element.Contains("-unity-material:") &&
                      element.Contains("2197d4bbffbaecd488653e0b8c2c301e"),
                    $"{prefix}-{i} does not use the additive T2 wave material.",
                    failures);
            }
        }
    }

    // touchTrail.GetEventPosition(container, event) degrades silently when it is
    // called with only the event: Lua binds the event to "container", every
    // property read fails, and the function hands back its (640, 384) centre
    // fallback instead of the pointer. Nothing throws, so a dropped first
    // argument can only be caught by looking at the call sites.
    private static void CheckEventPositionCallSites(ICollection<string> failures)
    {
        string scriptRoot = Path.Combine(Directory.GetCurrentDirectory(),
            "Assets", "UI");
        foreach (string file in Directory.GetFiles(scriptRoot, "*.txt",
                     SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Check(!source.Contains("touchTrail.GetEventPosition(event"),
                $"{Path.GetFileName(file)} calls touchTrail.GetEventPosition " +
                "without a container, which silently returns the screen centre.",
                failures);
        }
    }

    private static string FullPath(string assetPath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
    }

    private static void Check(
        bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
