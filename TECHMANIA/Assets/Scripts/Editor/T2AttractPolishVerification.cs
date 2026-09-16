using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class T2AttractPolishVerification
{
    public static void Run()
    {
        List<string> failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                failures));
        Debug.Log("[T2 Test] Attract polish contracts passed.");
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string assets = Application.dataPath;
        string ui = Path.Combine(assets, "UI");
        string tree = File.ReadAllText(Path.Combine(ui, "MainTree.uxml"));
        string songCard = File.ReadAllText(Path.Combine(ui, "Song Card.uxml"));
        string utility = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Utility.txt"));
        string game = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Game Screen.txt"));
        string selectSong = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Select Song Screen.txt"));
        string attractSettingsPath = Path.Combine(ui, "Scripts",
            "Attract Settings Menu.txt");
        string attractSettings = File.Exists(attractSettingsPath)
            ? File.ReadAllText(attractSettingsPath)
            : string.Empty;
        string settingsHotkey = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Settings Hotkey.txt"));
        string profile = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Profile Screen.txt"));
        string animation = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Animation.txt"));
        string attract = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Arcade Attract Screen.txt"));
        string tutorial = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Arcade Tutorial Screen.txt"));
        string prepare = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Prepare Screen.txt"));
        string title = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Title Screen.txt"));
        string selectMode = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Select Mode Screen.txt"));
        string mainScript = File.ReadAllText(Path.Combine(ui,
            "MainScript.txt"));
        string guide = File.ReadAllText(Path.Combine(assets, "Scripts",
            "Components", "Main Scene", "Game", "StarGuideOverlay.cs"));
        string chain = File.ReadAllText(Path.Combine(assets, "Scripts",
            "Components", "Main Scene", "Game", "NoteElements Subclasses",
            "ChainElementsBase.cs"));
        string noteManager = File.ReadAllText(Path.Combine(assets, "Scripts",
            "Components", "Main Scene", "Game", "NoteManager.cs"));

        Check(AppearsBeforeInFunction(attract, "AdvanceFromDemoPlay = function()",
                  "CoverAfterCompletion(generation)", "StopOverlayAnimation()") &&
              AppearsBeforeInFunction(attract, "EndLoop = function()",
                  "CoverAfterCompletion(generation)", "StopOverlayAnimation()"),
            "Demo Play hides its VCE header before the black curtain fully covers gameplay.",
            failures);

        Check(game.Contains("local headerMode = isAttractDemo and \"star\"") &&
              game.Contains("headerMode .. \"/bg.png\"") &&
              !game.Contains("Assets/UI/Sprites/DemoPlay/Background.png") &&
              attract.Contains("arcadeAttractScreen.tabLPlate.style.opacity = StyleFloat(1)") &&
              attract.Contains("arcadeAttractScreen.tabRPlate.style.opacity = StyleFloat(1)"),
            "Demo Play is not reusing the normal Star header beneath the VCE side plates.",
            failures);

        Check(game.Contains(
                  "topBarBackground.style.top = StyleLength(" +
                  "isStarMode and not isAttractDemo and -2 or 0)"),
            "Star Mixing does not lift only its main-game bg_background underlay by 2px.",
            failures);

        Check(AppearsBeforeInFunction(attract, "PlayIdleLoop = function()",
                  "arcadeAttractScreen.tabLGlow.style.opacity = StyleFloat(glowA)",
                  "arcadeAttractScreen.tabRGlow.style.opacity = StyleFloat(glowA)"),
            "Demo Play does not animate the right VCE glow and white wedge like the left side.",
            failures);

        Check(tree.Contains("name=\"attract-tab-r-plate\"") &&
              tree.Contains("left: 858px; top: 0; width: 422px; height: 68px;") &&
              tree.Contains("name=\"attract-tab-r-base\"") &&
              tree.Contains("left: 896px; top: 11px; width: 384px; height: 56px;") &&
              !tree.Contains("attract-tab-r-outer-cap") &&
              !File.Exists(Path.Combine(ui, "Sprites", "DemoPlay", "vce",
                  "tab_r_outer_cap.png")),
            "Demo Play right tab deviates from demoplay_back/button_effect VCE geometry.",
            failures);

        Check(chain.Contains("GuideChainHead") &&
              chain.Contains("AssignGuideChainHeadToLinkedNodes") &&
              noteManager.Contains("AssignGuideChainHeadToLinkedNodes") &&
              guide.Contains("GuideOwner") &&
              guide.Contains("node.GuideChainHead"),
            "Star GUIDE cannot reclaim a connected chain from a surviving node after its head resolves.",
            failures);

        Check(utility.Contains("local size = 420") &&
              utility.Contains("local lengthPerTexture = 1 / 30") &&
              utility.Contains("container.worldBound") &&
              utility.Contains("container.contentRect") &&
              utility.Contains("element.style.width = StyleLength(size)") &&
              utility.Contains("element.style.height = StyleLength(size)"),
            "Touch waves are not using scaled-container coordinates with the requested larger, slower 420px/30fps motion.",
            failures);

        // musicinfo_s.vce layers 23-26: dummy.png anchors at pos (263/231/
        // 199/167, 25) with quad (-22.5,-22.5)..(22.5,22.5), i.e. top-left
        // 240.5/208.5/176.5/144.5 at top 2.5 in the same absolute-vce
        // convention the card's stars-container already uses.
        Check(songCard.Contains("left: 240.5px; top: 2.5px;") &&
              songCard.Contains("left: 208.5px; top: 2.5px;") &&
              songCard.Contains("left: 176.5px; top: 2.5px;") &&
              songCard.Contains("left: 144.5px; top: 2.5px;"),
            "Song-card trophies do not sit on the arcade musicinfo_s anchors.",
            failures);

        VerifyFeverCrop(Path.Combine(ui, "Sprites", "Main Game",
            "Middle Fever Bar", "fever_only.png"), failures);
        Check(tree.Contains("name=\"fever-text\"") &&
              tree.Contains("Middle%20Fever%20Bar/fever_only.png") &&
              tree.Contains("name=\"fever-bonus\"") &&
              tree.Contains("width: 256px; height: 32px; margin-right: -10px;"),
            "Standalone FEVER crop or FEVER BONUS spacing is not VCE-aligned.",
            failures);

        Check(tree.Contains("name=\"your-star-point\"") &&
              tree.Contains("width: 114px; height: 18px;"),
            "Star result YOUR STAR POINT is not restored to its authored 114x18 size.",
            failures);

        Check(tree.Contains("name=\"attract-settings-menu-bg\"") &&
              tree.Contains("name=\"attract-arcade-mode\"") &&
              tree.Contains("name=\"attract-human-playtester\"") &&
              !tree.Contains("name=\"arcade-mode\" class=\"item\"") &&
              !tree.Contains("name=\"human-playtester\" class=\"item\""),
            "Arcade Mode and Human Playtester have not moved into the Attract F2 popup.",
            failures);
        Check(attractSettings.Contains("attractSettingsMenu = {") &&
              attractSettings.Contains("ArcadeMode") &&
              attractSettings.Contains("HumanPlaytester") &&
              attractSettings.Contains("StarSongSort") &&
              attractSettings.Contains("PopSongSort") &&
              mainScript.Contains("Assets/UI/Scripts/Attract Settings Menu.txt") &&
              mainScript.Contains("Assets/UI/Scripts/Track Sorting.txt") &&
              settingsHotkey.Contains("unity.enum.keyCode.F2") &&
              settingsHotkey.Contains("attractSettingsMenu.Toggle()"),
            "The title-screen F2 Attract settings controller is missing or not loaded.",
            failures);
        Check(!selectSong.Contains("menu.Q(\"arcade-mode\")") &&
              !selectSong.Contains("menu.Q(\"human-playtester\")"),
            "Song Select Options still owns Arcade Mode or Human Playtester.",
            failures);

        Check(profile.Contains("Assets/UI/SFX/Login/group_click.ogg") &&
              profile.Contains("OpenFromSongSelect") &&
              profile.Contains("ReturnToSongSelect") &&
              Count(profile, "screenCurtain.CoverInCoroutine") >= 2 &&
              Count(profile, "screenCurtain.RevealInCoroutine") >= 2 &&
              !profile.Contains("animation.FadeOutInCoroutine"),
            "Profile entry/exit does not use the Login click sound with black-curtain transitions.",
            failures);

        Check(tree.Contains("name=\"curtain\"") &&
              animation.Contains("screenCurtain = {") &&
              animation.Contains("CoverInCoroutine") &&
              animation.Contains("RevealInCoroutine") &&
              Count(attract, "screenCurtain.CoverInCoroutine") >= 3 &&
              !attract.Contains("animation.FadeOutInCoroutine") &&
              tutorial.Contains("screenCurtain.CoverInCoroutine") &&
              selectMode.Contains("screenCurtain.CoverInCoroutine"),
            "Attract, profile, or tutorial transitions still fade whole scenes instead of using the shared black curtain.",
            failures);

        Check(attract.Contains("arcadeSession.IsEnabled()") &&
              attract.Contains("arcadeSession.active.attractGameplay"),
            "Attract gameplay can still arm outside an active Arcade-mode snapshot.",
            failures);
        Check(tutorial.Contains("screenCurtain.Reset()") &&
              !tutorial.Contains("screenCurtain.Reveal(1)") &&
              selectMode.Contains("screenCurtain.Reset()") &&
              !selectMode.Contains("screenCurtain.Reveal(1)"),
            "Tutorial or Song Select still fades in instead of appearing immediately after loading.",
            failures);
        Check(attract.Contains("completionCoverSeconds = 1.0") &&
              attract.Contains("CoverAfterCompletion = function(generation)") &&
              attract.Contains("tm.gameSetup.onStageClear = function()") &&
              attract.Contains("tm.gameSetup.onStageFailed = function()") &&
              attract.Contains("arcadeAttractScreen.AdvanceFromDemoPlay()") &&
              attract.Contains("arcadeAttractScreen.EndLoop()") &&
              !attract.Contains("StartPreEndCover") &&
              !attract.Contains("CoverBeforeTransition"),
            "Attract gameplay is not using completion callbacks before its guarded black-curtain cover.",
            failures);
        Check(title.Contains("Show = function(onReady)") &&
              title.Contains("if (onReady != nil) then onReady() end") &&
              title.Contains("screenCurtain.CoverInCoroutine(1)") &&
              !title.Contains("animation.FadeOutInCoroutine(titleScreen.screen") &&
              !attract.Contains("screenCurtain.RevealInCoroutine") &&
              !attract.Contains("screenCurtain.Reveal(") &&
              attract.Contains("titleScreen.Show(function()"),
            "Title or Attract still fades screen elements/scenes instead of exposing a ready scene immediately.",
            failures);
        Check(tree.Contains("name=\"arcade-star-song-sort\"") &&
              tree.Contains("name=\"arcade-pop-song-sort\""),
            "The Attract F2 popup is missing per-mode track-order dropdowns.",
            failures);

        return failures;
    }

    private static void VerifyFeverCrop(string path,
        ICollection<string> failures)
    {
        if (!File.Exists(path))
        {
            failures.Add("The authentic standalone FEVER crop is missing.");
            return;
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
            Check(texture.width == 128 && texture.height == 32,
                "Standalone FEVER crop is not the VCE 128x32 left half.", failures);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static bool AppearsBeforeInFunction(string source,
        string functionMarker, string first, string second)
    {
        int function = source.IndexOf(functionMarker, StringComparison.Ordinal);
        if (function < 0) return false;
        int functionEnd = source.IndexOf("\n    end,", function,
            StringComparison.Ordinal);
        if (functionEnd < 0) return false;
        int firstIndex = source.IndexOf(first, function, StringComparison.Ordinal);
        int secondIndex = source.IndexOf(second, function, StringComparison.Ordinal);
        return firstIndex >= function && firstIndex < functionEnd &&
               secondIndex >= function && secondIndex < functionEnd &&
               firstIndex < secondIndex;
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

    private static void Check(bool condition, string message,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
