using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MainGameArcadePresentationVerification
{
    public static void Run()
    {
        string uiRoot = Path.Combine(Application.dataPath, "UI");
        string mainTree = File.ReadAllText(Path.Combine(uiRoot, "MainTree.uxml"));
        string gameScreen = File.ReadAllText(Path.Combine(uiRoot, "Scripts",
            "Game Screen.txt")).Replace("\r\n", "\n");
        string hudCompiler = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts", "Editor", "T2ArcadeHudAssetCompiler.cs"))
            .Replace("\r\n", "\n");

        Require(gameScreen.Contains("PlayerStar = \"PlayerStar\"") &&
            gameScreen.Contains("PlayerPop = \"PlayerPop\"") &&
            gameScreen.Contains("TutorialStar = \"TutorialStar\"") &&
            gameScreen.Contains("AttractStar = \"AttractStar\"") &&
            gameScreen.Contains("AttractPop = \"AttractPop\"") &&
            gameScreen.Contains(
                "ApplyPresentationVisibility = function(includeGameplayLayer)"),
            "Player, tutorial, and attract presentation states are not centralized.");

        // Local lefts 0/100/200/300 inside the x=431 band put the lights at the
        // absolute x 431/531/631/731 the live D3D9 capture measures for the
        // arcade's four 114x94 back_light quads, at y 0 rather than the -8 lift
        // that made them read as pushed left and high; the no-material clause is
        // also measured, the arcade binds them with srcBlend 5 / destBlend 6
        // (normal alpha), not additively. See
        // T2GameplayVisualRegressionVerification for the capture reference.
        Require(gameScreen.Contains("ArcadeHeaderMeterAtlas = function()") &&
            gameScreen.Contains("local loopLength = 6 / 13") &&
            gameScreen.Contains("meterFrames[textureIndex]") &&
            gameScreen.Contains("lightFrames[textureIndex]") &&
            !gameScreen.Contains("HeaderBackLights = function()") &&
            !gameScreen.Contains("GrooveMeterForeground = function()") &&
            Count(mainTree, "width: 114px; height: 94px;") == 4 &&
            mainTree.Contains("left: 0; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 100px; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 200px; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 300px; top: 0; width: 114px; height: 94px;") &&
            !Tag(mainTree, "header-beat-light-1").Contains("-unity-material") &&
            !Tag(mainTree, "header-beat-light-2").Contains("-unity-material") &&
            !Tag(mainTree, "header-beat-light-3").Contains("-unity-material") &&
            !Tag(mainTree, "header-beat-light-4").Contains("-unity-material"),
            "Header lights and rainbow are not on the synchronized arcade atlas loop.");

        foreach (string mode in new[] { "star", "pop" })
        {
            RequireTexture(
                $"Assets/UI/Sprites/Main Game/{mode}/Arcade HUD/bar_base.png",
                434, 20, $"{mode} groove-meter base");
            for (int frame = 0; frame < 10; frame++)
            {
                RequireTexture(
                    $"Assets/UI/Sprites/Main Game/{mode}/Arcade HUD/bar_anim_{frame:00}.png",
                    434, 20, $"{mode} rainbow frame {frame:00}");
                RequireTexture(
                    $"Assets/UI/Sprites/Main Game/{mode}/Arcade HUD/back_light_{frame:00}.png",
                    114, 94, $"{mode} rear-light frame {frame:00}");
            }
        }

        Require(mainTree.Contains(
                "name=\"groove-meter\" picking-mode=\"Ignore\" style=\"position: absolute; width: 434px; height: 20px; left: 423px; top: 25px;\"") &&
            gameScreen.Contains("grooveMeterFill.style.top = StyleLength(0)") &&
            gameScreen.Contains("grooveMeterFill.style.height = StyleLength(20)") &&
            gameScreen.Contains("remainingHpPortion * 100") &&
            gameScreen.Contains("unity.mathf.Lerp(2.8, 100, remainingHpPortion)") &&
            Tag(mainTree, "bg").Contains("width: 434px; height: 20px;") &&
            Tag(mainTree, "meter").Contains("width: 434px; height: 20px;") &&
            !Tag(mainTree, "meter").Contains("-unity-material") &&
            hudCompiler.Contains("frame * 20, 434, 20,\n                    false") &&
            mainTree.Contains("name=\"game-container\" picking-mode=\"Ignore\" class=\"fully-expand\" style=\"top: 67px;") &&
            mainTree.Contains(
                "name=\"full-meter-effect\" picking-mode=\"Ignore\" style=\"position: absolute; left: -54px; top: -40px; width: 544px; height: 104px;") &&
            Tag(mainTree, "full-meter-effect").Contains("-unity-material") &&
            gameScreen.Contains("GrooveMeterFullEffect = function()") &&
            gameScreen.Contains("{ time = 0.25, values = {1, 1, 1, 1} }") &&
            gameScreen.Contains("{ time = 0.75, values = {1, 1, 1, 1} }"),
            "Rainbow orientation, header seam, or full-meter glow no longer matches the verified arcade geometry.");

        Require(!gameScreen.Contains("tm.game.timer.beat"),
            "Header lights are still coupled to song BPM instead of the measured atlas loop.");

        Debug.Log("[MainGame Arcade Test] Presentation, header lights, rainbow fill, and full-meter glow passed.");
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(needle, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static string Tag(string document, string name)
    {
        string marker = $"name=\"{name}\"";
        int start = document.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;
        int end = document.IndexOf('>', start);
        return end < 0 ? document.Substring(start) :
            document.Substring(start, end - start + 1);
    }

    private static void RequireTexture(string assetPath, int width, int height,
        string label)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Require(texture != null && texture.width == width &&
            texture.height == height,
            $"Missing authentic {width}x{height} {label}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
