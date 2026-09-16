using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class T2GameplayVisualRegressionVerification
{
    private const string T2Root =
        @"C:\Users\Jen\Downloads\Technika Projects\t2\resource\MainGame";

    public static void Run()
    {
        List<string> failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                failures));
        Debug.Log("[T2 Test] Gameplay visual regressions passed.");
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string uiRoot = Path.Combine(Application.dataPath, "UI");
        string mainTree = File.ReadAllText(Path.Combine(uiRoot,
            "MainTree.uxml"));
        // MainStyle.uss is CRLF on disk while the multi-line needles below are
        // written with \n, so without this normalization those two checks could
        // never match and silently reported failures.
        string mainStyle = File.ReadAllText(Path.Combine(uiRoot,
            "MainStyle.uss")).Replace("\r\n", "\n");
        string gameScreen = File.ReadAllText(Path.Combine(uiRoot, "Scripts",
            "Game Screen.txt")).Replace("\r\n", "\n");
        string hudCompiler = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts", "Editor", "T2ArcadeHudAssetCompiler.cs"))
            .Replace("\r\n", "\n");
        string guide = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts", "Components", "Main Scene", "Game",
            "StarGuideOverlay.cs"));
        string noteList = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts", "Data Structures", "NoteList.cs"));

        // musicinfo_s.vce layers 23-26 are dummy.png anchors, quad
        // (-22.5,-22.5)..(22.5,22.5) => a 45x45 slot. The earlier 32x34 pin
        // was the arcade size pre-divided by the 1080p panel scale, so the
        // badges rendered 1.4x too small and with the wrong aspect.
        Check(mainStyle.Contains(
            "#select-song-screen .song-card .trophy {\n    width: 45px;\n    height: 45px;"),
            "Song-card trophies do not use the arcade 45x45 anchor size.",
            failures);

        string authenticFever = Path.Combine(T2Root, "panel", "Fever",
            "FeverNumber", "fever.png");
        string themeFever = Path.Combine(uiRoot, "Sprites", "Main Game",
            "Middle Fever Bar", "fever.png");
        Check(File.Exists(themeFever) && File.Exists(authenticFever) &&
            Hash(themeFever) == Hash(authenticFever),
            "Middle FEVER BONUS is not byte-identical to the T2 source asset.",
            failures);
        Check(mainTree.Contains(
            "Middle%20Fever%20Bar/fever.png") &&
            !mainTree.Contains("fever_bonus_t2_red.png") &&
            mainTree.Contains("name=\"plus\" picking-mode=\"Ignore\" style=\"width: 32px; height: 32px;") &&
            mainStyle.Contains("width: 32px;\n    height: 32px;") &&
            gameScreen.Contains("Middle Fever Bar/num_fever_"),
            "Middle FEVER BONUS does not use the authentic 256x32/32px VCE composition.",
            failures);
        for (int digit = 0; digit < 10; digit++)
        {
            string fileName = $"num_fever_{digit}.png";
            Check(Hash(Path.Combine(T2Root, "panel", "Fever", "FeverNumber",
                       fileName)) ==
                  Hash(Path.Combine(uiRoot, "Sprites", "Main Game",
                       "Middle Fever Bar", fileName)),
                $"Middle FEVER BONUS digit {digit} differs from T2 VCE source.",
                failures);
        }

        string starLightFolder = Path.Combine(uiRoot, "Sprites", "Main Game",
            "star", "Arcade HUD");
        Texture2D starBarBase = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/UI/Sprites/Main Game/star/Arcade HUD/bar_base.png");
        Check(starBarBase != null && starBarBase.width == 434 &&
            starBarBase.height == 20,
            "Missing authentic 434x20 Star groove-meter base.", failures);
        for (int frame = 0; frame < 10; frame++)
        {
            string path = Path.Combine(starLightFolder,
                $"back_light_{frame:00}.png");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/UI/Sprites/Main Game/star/Arcade HUD/back_light_{frame:00}.png");
            Texture2D rainbow = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/UI/Sprites/Main Game/star/Arcade HUD/bar_anim_{frame:00}.png");
            Check(File.Exists(path) && texture != null && texture.width == 114 &&
                texture.height == 94,
                $"Missing authentic 114x94 Star back-light frame {frame:00}.",
                failures);
            Check(rainbow != null && rainbow.width == 434 &&
                rainbow.height == 20,
                $"Missing authentic 434x20 Star rainbow frame {frame:00}.",
                failures);
        }
        // The four rear lights are measured, not inferred. In the live D3D9
        // capture (ReverseEngineering/live_maingame_capture_20260815/
        // d3d9-live-20260815-091743.jsonl, gameplay frames 22674..23082) the
        // arcade issues exactly four 114x94 quads bound to a 1140x94 texture --
        // back_light.png is the only 1140x94 asset in the tree, and it resolves
        // by (sourceSize 28045, sampleHash b9bf57f0) to
        // MainGame/panel/star_ingame_info/back_light.png -- at
        // L430.5/530.5/630.5/730.5, T-0.5, B93.5. This capture emits a logical
        // pixel rect L..R as L-0.5..R-0.5, so those are x 431/531/631/731 at
        // y 0..94 on a 100px pitch, and the band container at x=431 makes the
        // locals 0/100/200/300. Two earlier readings were wrong and both showed
        // as "the lights sit too far left": 18/118/218/318 (from ingame_info.vce's
        // meter quad) is 8px short, and 25/125/225/325 was 1px short. The top -8
        // lift that went with them is gone too -- the arcade's quads start at
        // y 0 exactly. All four quads always sample the same atlas cell in every
        // captured frame, which is why one shared textureIndex drives the loop,
        // and the cell advances 149 steps in 6888 ms = 21.6 fps, i.e. loopLength
        // 6/13 s. Both modes' sheets are 1140x94 and the split frames are
        // byte-identical slices, so pop and star share this geometry.
        //
        // The band is NOT clipped. The arcade draws the lights (draws 5-8) after
        // the cut plate (draw 3, ingamebar_cut crop (0,157) 561x59 at (361,7))
        // and before the 1280x72 header plate (draws 9-10, the same atlas at
        // (0,0) 1024x72 and (1024,0) 256x72), so the plate covers them by
        // z-order alone; the scissor stays 0,0,1280,768 on every beam draw.
        // back_light.png's ink runs to row 87 -- 58/255 at row 67, 39/255 at row
        // 72, ~4/255 at row 84 -- while both bg.png and bg_background.png stop
        // around row 66, so the arcade shows a soft fade spilling onto the
        // playfield. Clipping top-bar-bg or the mask replaces that fade with a
        // flat bright edge, and it also truncated the additive full-meter glow,
        // which the capture draws unclipped at L368.5 T-15.5 R912.5 B88.5 ->
        // logical (369, -15) 544x104, i.e. down to row 89.
        Check(gameScreen.Contains("ArcadeHeaderMeterAtlas = function()") &&
            gameScreen.Contains("local loopLength = 6 / 13") &&
            gameScreen.Contains("meterFrames[textureIndex]") &&
            gameScreen.Contains("lightFrames[textureIndex]") &&
            !gameScreen.Contains("HeaderBackLights = function()") &&
            !gameScreen.Contains("GrooveMeterForeground = function()") &&
            Count(mainTree, "width: 114px; height: 94px;") == 4 &&
            mainTree.Contains("name=\"top-bar-bg\" picking-mode=\"Ignore\" style=\"position: absolute; left: 0; top: 0; right: 0; height: 72px; overflow: visible;") &&
            mainTree.Contains("name=\"header-beat-light-mask\" picking-mode=\"Ignore\" style=\"position: absolute; left: 431px; top: 0; width: 414px; height: 94px; overflow: visible;\"") &&
            // 431 is the arcade's own first beam x, so the container's locals are
            // the pitch itself: 0/100/200/300 spans 431..845, the exact band the
            // capture draws. The band is deliberately NOT flush with the groove
            // meter's 423 anchor -- the arcade offsets the beams 8px right of the
            // meter plate, and matching 423 is what made them look pushed left.
            mainTree.Contains("left: 0; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 100px; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 200px; top: 0; width: 114px; height: 94px;") &&
            mainTree.Contains("left: 300px; top: 0; width: 114px; height: 94px;") &&
            !mainTree.Contains("top: -8px; width: 114px; height: 94px;"),
            "T2 rear lights and rainbow are not synchronized to the live arcade atlas loop.",
            failures);

        Check(gameScreen.Contains("PlayerStar = \"PlayerStar\"") &&
            gameScreen.Contains("PlayerPop = \"PlayerPop\"") &&
            gameScreen.Contains("TutorialStar = \"TutorialStar\"") &&
            gameScreen.Contains("AttractStar = \"AttractStar\"") &&
            gameScreen.Contains("AttractPop = \"AttractPop\"") &&
            gameScreen.Contains("ApplyPresentationVisibility = function(includeGameplayLayer)"),
            "MainGame presentation rules are not centralized into explicit player/tutorial/attract states.",
            failures);

        Check(mainTree.Contains(
            "name=\"full-meter-effect\" picking-mode=\"Ignore\" style=\"position: absolute; left: -54px; top: -40px; width: 544px; height: 104px;") &&
            gameScreen.Contains("GrooveMeterFullEffect = function()") &&
            gameScreen.Contains("{ time = 0.25, values = {1, 1, 1, 1} }") &&
            gameScreen.Contains("{ time = 0.75, values = {1, 1, 1, 1} }"),
            "Full groove-meter light does not retain the authentic 544x104 one-second additive pulse.",
            failures);

        Check(gameScreen.Contains("grooveMeterFill.style.top = StyleLength(0)") &&
            gameScreen.Contains("grooveMeterFill.style.height = StyleLength(20)") &&
            gameScreen.Contains("remainingHpPortion * 100") &&
            hudCompiler.Contains("frame * 20, 434, 20,\n                    false") &&
            mainTree.Contains("name=\"game-container\" picking-mode=\"Ignore\" class=\"fully-expand\" style=\"top: 67px;") &&
            mainTree.Contains("left: 0; top: 0; width: 434px; height: 20px;"),
            "Pop/Star rainbow orientation or gameplay/header seam differs from the arcade geometry.",
            failures);

        string doubledMiddleFeverPath = Path.Combine(uiRoot, "Sprites",
            "Main Game", "Middle Fever Bar", "gauge_fever_doubled.png");
        string doubledMiddleFeverMeta = doubledMiddleFeverPath + ".meta";
        Texture2D doubledMiddleFever = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/UI/Sprites/Main Game/Middle Fever Bar/gauge_fever_doubled.png");
        Check(File.Exists(doubledMiddleFeverPath) &&
            doubledMiddleFever != null && doubledMiddleFever.width == 1024 &&
            doubledMiddleFever.height == 32 &&
            mainTree.Contains("Middle%20Fever%20Bar/gauge_fever_doubled.png") &&
            File.Exists(doubledMiddleFeverMeta) &&
            !File.ReadAllText(doubledMiddleFeverMeta).Contains(
                "assetBundleName: t3"),
            "Middle fever building gauge is not the mirrored 1024x32 T3 composition.",
            failures);

        Check(noteList.Contains("ForEachActive(Action<INoteHolder> action)") &&
            guide.Contains("CollectCandidates") &&
            guide.Contains("FindFreeHand") &&
            !guide.Contains("hands[lane]") &&
            Count(mainTree, "name=\"star-guide-hand-") == 8,
            "Star GUIDE still binds one hand per lane and can starve dense gestures.",
            failures);

        // The stage disc circle now lives entirely in .allclear-v2-disc-image so
        // one rule governs all three cards. Inline styles beat USS in UI
        // Toolkit, so a per-card scale mode here would silently restore the
        // stretched full-bleed square this check was written to catch.
        int fittedDiscs = Count(mainTree,
            "name=\"image\" picking-mode=\"Ignore\" class=\"allclear-v2-disc-image\"/>");
        Check(fittedDiscs == 3,
            "All three All Clear covers do not defer their scale mode to the disc-circle rule.",
            failures);

        return failures;
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream));
    }

    private static void Check(bool condition, string message,
        List<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
