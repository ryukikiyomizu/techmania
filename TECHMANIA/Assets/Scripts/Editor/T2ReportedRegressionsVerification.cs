using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class T2ReportedRegressionsVerification
{
    private const string FailedVideoFolder = "Assets/UI/Videos";

    public static void Run()
    {
        var failures = Evaluate();
        if (failures.Count > 0)
        {
            foreach (var failure in failures)
                Debug.LogError("[T2 Reported Regression Test] " + failure);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[T2 Reported Regression Test] Reported regression contracts passed.");
        EditorApplication.Exit(0);
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string ui = Application.dataPath + "/UI";
        string style = File.ReadAllText(Path.Combine(ui, "MainStyle.uss"));
        string tree = File.ReadAllText(Path.Combine(ui, "MainTree.uxml"));
        string allClear = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Arcade All Clear V2 Screen.txt")).Replace("\r\n", "\n");
        string tutorial = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Arcade Tutorial Screen.txt")).Replace("\r\n", "\n");
        string selectMode = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Select Mode Screen.txt")).Replace("\r\n", "\n");
        string result = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Result Screen.txt")).Replace("\r\n", "\n");
        string game = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Game Screen.txt")).Replace("\r\n", "\n");
        string themeOptions = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Theme Options.txt"));
        string prepare = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Prepare Screen.txt")).Replace("\r\n", "\n");
        string selectSong = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Select Song Screen.txt")).Replace("\r\n", "\n");
        string humanPlaytester = File.ReadAllText(Path.Combine(Application.dataPath,
            "Scripts", "Components", "Main Scene", "Game", "Human Playtester",
            "HumanPlayScheduler.cs"));

        // Anchor on the opening brace: the derivation comments above these rules
        // name the neighbouring selectors in prose, and a bare selector marker
        // would start the span inside a comment and end it at the WRONG rule's
        // closing brace.
        string discStyle = Block(style, ".allclear-v2-disc-image {", "}");
        string fallbackStyle = Block(style, ".allclear-v2-disc-fallback {", "}");
        // Two conventions, two quads. `image` takes disc-convention art whose
        // own alpha already draws the circle (0.7784 of the canvas across the
        // 610 resource/DiscImg files), so it gets Total_Result_Star.vce L2's
        // full 142x142 quad and lands at 110.5 on its own; sizing that box to
        // 110.5 applies the ratio twice and the cover renders at 86px inside a
        // ring of bare chrome. `fallback` takes full-bleed square jackets, so it
        // keeps the 110.5 crop centred on stage_disc_ring's ink centre --
        // card-local (74.5, 74.5), disc-local (70.5, 71.5) since
        // .allclear-v2-disc sits at card-local (4, 3) -- 1.25px under the ring's
        // 108px aperture. The three inline scale-to-fit overrides stay gone.
        Check(discStyle.Contains("left: 0;") && discStyle.Contains("top: 0;") &&
              discStyle.Contains("width: 142px;") && discStyle.Contains("height: 142px;") &&
              discStyle.Contains("overflow: hidden;") &&
              discStyle.Contains("border-top-left-radius: 71px;") &&
              discStyle.Contains("border-bottom-right-radius: 71px;") &&
              discStyle.Contains("-unity-background-scale-mode: scale-and-crop;") &&
              fallbackStyle.Contains("left: 15.25px;") && fallbackStyle.Contains("top: 16.25px;") &&
              fallbackStyle.Contains("width: 110.5px;") && fallbackStyle.Contains("height: 110.5px;") &&
              fallbackStyle.Contains("border-top-left-radius: 55.25px;") &&
              fallbackStyle.Contains("border-bottom-right-radius: 55.25px;") &&
              fallbackStyle.Contains("-unity-background-scale-mode: scale-and-crop;") &&
              Count(tree, "class=\"allclear-v2-disc-image\"/>") == 3 &&
              !tree.Contains("class=\"allclear-v2-disc-image\" style="),
            "Total Results artwork does not match the arcade stage-disc quads.",
            failures);

        string validateResult = Block(allClear, "ValidateResult = function", "ValidateCredit = function");
        string loadStageImage = Block(allClear, "LoadStageImage = function", "ApplyModeAssets = function");
        Check(!validateResult.Contains("miniEyecatchPath") &&
              !validateResult.Contains("eyecatchImage") &&
              loadStageImage.Contains("path = result.eyecatchImage") &&
              loadStageImage.Contains("source = \"eyecatch\"") &&
              !loadStageImage.Contains("allclear-v2-disc-thumbnail") &&
              !style.Contains(".allclear-v2-disc-thumbnail"),
            "Total Results does not fall back to the song thumbnail when disc art is absent.", failures);
        Check(!validateResult.Contains("if (not hasThemeDisc and not hasDisc) then return false end") &&
              loadStageImage.Contains("if (path == nil or path == \"\") then"),
            "A stage without optional disc art still rejects the entire All Clear screen.", failures);

        Check(style.Contains("#attract-settings-menu Label") &&
              style.Contains("#attract-settings-menu DropdownField Label") &&
              style.Contains("#attract-settings-menu Toggle Label"),
            "Arcade Mode settings do not have the white label/control rules.", failures);

        string tutorialShow = Block(tutorial, "Show = function", "Initialize = function");
        Check(tutorialShow.Contains("screenCurtain.CoverInCoroutine(0)") &&
              tutorialShow.IndexOf("screenCurtain.CoverInCoroutine(0)", StringComparison.Ordinal) <
              tutorialShow.IndexOf("gameScreen.Show()", StringComparison.Ordinal),
            "Star Mixing tutorial shows gameplay before covering the loading transition black.", failures);

        string starTutorialTransition = Block(selectMode,
            "if (arcadeSession.ShouldShowStarTutorial", "else", 0);
        int coverTutorial = starTutorialTransition.IndexOf(
            "screenCurtain.CoverInCoroutine(1)", StringComparison.Ordinal);
        int hideMode = starTutorialTransition.IndexOf(
            "selectModeScreen.Hide()", StringComparison.Ordinal);
        int showTutorial = starTutorialTransition.IndexOf(
            "arcadeTutorialScreen.ShowAfterConfirmationFade", StringComparison.Ordinal);
        Check(coverTutorial >= 0 && hideMode > coverTutorial &&
              showTutorial > hideMode,
            "Star Mixing tutorial exposes the retained final frame of star_select.mp4.",
            failures);

        string rankVisual = Block(result, "SetRankVisual = function", "ShowResult = function");
        Check(rankVisual.Contains("if (rank == \"S\") then") &&
              rankVisual.Contains("elseif (rank == \"A\") then") &&
              !rankVisual.Contains("rank == \"S++\" or rank == \"S+\" or rank == \"S\"") &&
              !rankVisual.Contains("rank == \"A++\" or rank == \"A+\" or rank == \"A\""),
            "S+ / S++ result ranks still start the animated base-rank layer behind the authored asset.", failures);

        Check(result.Contains("if (scoreKeeper.stageFailed) then\n                starPoints = 0") &&
              result.Contains("ForceFailedStarZero = function") &&
              result.Contains("result_star_failed_intro.mp4") &&
              result.Contains("result_star_failed.mp4") &&
              result.Contains("result_pop_failed_intro.mp4") &&
              result.Contains("result_pop_failed.mp4"),
            "Failed results do not select the authoritative failure media and Star 0 state.", failures);
        Check(game.Contains("Assets/UI/Videos/you_failed.mp4"),
            "Track failure does not use the authored MP4.", failures);
        // The trophy anchor is the arcade dummy quad at 1:1: 64x64 at (428, 74)
        // inside the stage card. Scoped through Block so the sizes cannot be
        // satisfied by an unrelated rule that happens to share the numbers.
        string trophyAnchorStyle = Block(style, ".allclear-v2-trophy-anchor", "}");
        Check(result.Contains("local trophyVisualSize = 128") &&
              result.Contains("local lowerMedalSlotTop = 575") &&
              trophyAnchorStyle.Contains("left: 428px;") &&
              trophyAnchorStyle.Contains("top: 74px;") &&
              trophyAnchorStyle.Contains("width: 64px;") &&
              trophyAnchorStyle.Contains("height: 64px;"),
            "Result trophy sizing or the All Clear trophy anchor regressed.", failures);

        // Trophy canvases are load-bearing geometry, not padding. The arcade
        // ships two complete tier sets (CLIENT.EXE composes the names from a
        // directory prefix): resource/trophy/ is 80x80 with ink (11,10)-(67,71)
        // and is what CSongSelectState binds, resource/trophy/result/ is
        // 128x128 with ink (35,34)-(91,93) and is what CSingleStarPopResultState
        // binds. Every quad drawing them is a fixed size that scales the whole
        // canvas, so a re-canvassed PNG silently resizes the badge: cropping
        // Select Song's bronze_trophy_or.png to its ink (63x68) once drew the
        // NM bronze at 37.7px of ink on the 45px card slot where its siblings
        // draw 32.1px. Assert the native PNG dimensions, and reject the
        // .original.png residue that the crop left behind.
        foreach (var trophySet in new[]
        {
            new { Folder = "Select Song/Trophies", Size = 80 },
            new { Folder = "Result/Trophies", Size = 128 }
        })
        {
            string folder = Path.Combine(Application.dataPath, "UI", "Sprites",
                trophySet.Folder.Replace('/', Path.DirectorySeparatorChar));
            string[] pngs = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.png")
                : new string[0];
            Check(pngs.Length == 16,
                trophySet.Folder + " should hold 16 trophy PNGs (4 tiers x 4 "
                + "difficulties), found " + pngs.Length + ".", failures);
            foreach (string png in pngs)
            {
                Vector2Int size = PngSize(png);
                Check(size.x == trophySet.Size && size.y == trophySet.Size,
                    Path.GetFileName(png) + " in " + trophySet.Folder + " is "
                    + size.x + "x" + size.y + ", not the arcade's "
                    + trophySet.Size + "x" + trophySet.Size + " canvas.",
                    failures);
            }
        }

        foreach (string file in new[]
        {
            "result_pop_failed_intro.mp4",
            "result_pop_failed.mp4",
            "result_star_failed_intro.mp4",
            "result_star_failed.mp4"
        })
        {
            Check(File.Exists(Path.Combine(Application.dataPath, "UI", "Videos", file)),
                "Missing converted authoritative failed-result video " + file + ".", failures);
        }

        Check(themeOptions.Contains("GetThemeOptions(\"TECHNIKA 2\")"),
            "Theme options are still stored under the stale TECHNIKA 3 namespace.", failures);
        Check(humanPlaytester.Contains("ThemeName = \"TECHNIKA 2\""),
            "Human Playtester still reads the stale TECHNIKA 3 option namespace.", failures);

        // The question card owns a page of its own at index 0, behind the first
        // songs page and reachable only by the left arrow from page 1, so the
        // songs pages carry nine tracks again.
        Check(prepare.Contains("randomPageIndex = 0") &&
              prepare.Contains("GetPageContents = function(pageIndex)") &&
              prepare.Contains("if (pageIndex == trackList.randomPageIndex) then") &&
              !prepare.Contains("table.insert(page, 1, carry)"),
            "The random card is still injected into every songs page.", failures);
        Check(selectSong.Contains("currentPage = -1,") &&
              selectSong.Contains(
                  "if (newPage < trackList.randomPageIndex) then return end") &&
              selectSong.Contains(
                  "local pageContents = trackList.GetPageContents(newPage)") &&
              selectSong.Contains(
                  "if (selectSongScreen.currentPage <= trackList.randomPageIndex) then return end"),
            "Song Select does not treat the random page as the page behind page 1.",
            failures);
        Check(selectSong.Contains(
                  "if (pageNumber == trackList.randomPageIndex) then\n" +
                  "            selectSongPageDots.bg.display = false"),
            "The page dot strip still counts the random page.", failures);

        return failures;
    }

    private static string Block(string source, string startMarker, string endMarker,
        int occurrence = 0)
    {
        int start = -1;
        int offset = 0;
        for (int i = 0; i <= occurrence; i++)
        {
            start = source.IndexOf(startMarker, offset, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            offset = start + startMarker.Length;
        }

        int end = source.IndexOf(endMarker, offset, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    // PNG IHDR: 8-byte signature, 4-byte length, "IHDR", then width and height
    // as big-endian uint32. Read from the file so importer settings (NPOT
    // scaling, max size, compression) cannot mask the authored canvas.
    private static Vector2Int PngSize(string path)
    {
        byte[] header = new byte[24];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Read(header, 0, header.Length) < header.Length)
                return new Vector2Int(-1, -1);
        }
        Func<int, int> beInt = offset =>
            (header[offset] << 24) | (header[offset + 1] << 16) |
            (header[offset + 2] << 8) | header[offset + 3];
        return new Vector2Int(beInt(16), beInt(20));
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset,
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
