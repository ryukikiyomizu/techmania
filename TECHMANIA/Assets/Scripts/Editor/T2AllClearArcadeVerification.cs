using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

// Pins the arcade ALL CLEARED (Total Result) presentation and the F2 preview
// harness that makes it checkable without playing three songs.
//
// The text contracts hold the measured badge geometry and the wiring that has
// to stay in place; an anchor nudged in USS, a preview guard dropped from the
// V2 screen or a panel control left unbound would otherwise only surface on a
// cabinet. The rest is arithmetic: the rank curve, the star-point formula and
// the harness's fixture math are replicated here and run against the presets
// parsed out of the Lua source, so a preset cannot claim a finale it no longer
// resolves to -- and the replication is itself pinned to the curve parsed back
// out of Arcade Session.txt, so the two can only move together.
public static class T2AllClearArcadeVerification
{
    public static void Run()
    {
        RunFromProject(Directory.GetCurrentDirectory());
    }

    public static void RunFromProject(string projectPath)
    {
        string screen = ReadUi(projectPath, "Scripts", "Arcade All Clear V2 Screen.txt");
        string assets = ReadUi(projectPath, "Scripts", "Arcade All Clear V2 Assets.txt");
        string preview = ReadUi(projectPath, "Scripts", "Arcade All Clear Preview.txt");
        string session = ReadUi(projectPath, "Scripts", "Arcade Session.txt");
        string attract = ReadUi(projectPath, "Scripts", "Attract Settings Menu.txt");
        string boot = ReadUi(projectPath, null, "MainScript.txt");
        string uxml = ReadUi(projectPath, null, "MainTree.uxml");
        string uss = ReadUi(projectPath, null, "MainStyle.uss");

        VerifyBadgeGeometry(uss, screen);
        VerifyDiscGeometry(uss, uxml);
        VerifyDifficultyRing(uss, uxml, screen, assets, projectPath);
        VerifyStarDigitSockets(screen);
        VerifyPreviewGuards(screen);
        VerifyHarness(preview);
        VerifyPanel(attract, uxml, boot);
        int presets = VerifyFixtures(preview, session);

        Console.WriteLine("[T2 Test] Arcade All Clear badge geometry, preview " +
            "guards and " + presets + " harness presets passed.");
    }

    private static string ReadUi(string projectPath, string folder, string file)
    {
        string path = folder == null
            ? Path.Combine(projectPath, "Assets", "UI", file)
            : Path.Combine(projectPath, "Assets", "UI", folder, file);
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    // The two stage-badge sockets. Total_Result_Star.vce (both the Pop and the
    // Star copy) draws L27 at a constant 64x64 from card-local (428, 74) and
    // L28/L29 at a constant 100x100 from (352, 56); position is the only
    // animated channel, and the anchors' own translate tracks already carry it.
    private static void VerifyBadgeGeometry(string uss, string screen)
    {
        RequireRect(Block(uss, ".allclear-v2-achievement-anchor {",
            "The achievement anchor"), 352, 56, 100, 100, "achievement");
        RequireRect(Block(uss, ".allclear-v2-trophy-anchor {",
            "The trophy anchor"), 428, 74, 64, 64, "trophy");

        Require(screen.Contains("ApplyBadgeFrame = function(index, kind, localFrame)"),
            "The stage badges have no constant-quad player; ApplyBadgeFrame is gone.");
        Require(screen.Contains("arcadeAllClearV2Screen.FillAnchor(element)"),
            "ApplyBadgeFrame must fill its anchor exactly rather than animate a quad.");
        Require(screen.Contains("ApplyBadgeFrame(index, \"trophy\", trophyFrame)") &&
            Regex.IsMatch(screen, @"ApplyBadgeFrame\(index, effectName, effectFrame\)"),
            "The timeline scrub no longer routes the stage badges through ApplyBadgeFrame.");
        Require(!Regex.IsMatch(screen, @"ApplyEffectFrame\(index, ""trophy""") &&
            !Regex.IsMatch(screen, @"ApplyEffectFrame\(index, effectName"),
            "The stage badges are being animated by the icon-effect player again; " +
            "trophy_eff / perpect_eff / allcombo_eff belong to the single-song screens.");

        Require(screen.Contains("card.Q(\"allcombo-front\").backgroundImage = t.allComboIcon"),
            "The all-combo card icon is not wired to its own texture.");
        Require(screen.Contains(
                "arcadeAllClearV2Screen.effectTextures.allComboIcon = tm.io.LoadTextureFromTheme(shared.allComboIcon)"),
            "allComboIcon is never loaded, so the all-combo badge would draw empty.");
        Require(screen.Contains("UnusedBadgeEffectNames = {"),
            "The plates the arcade cards never draw are no longer blanked.");
    }

    // Total_Result_Star.vce parks the stage disc art as L2 at card-local (4, 3)
    // 142x142 and the chrome ring as L3 at (5, 5) 140x140. The art texture is a
    // square canvas holding a circle: over the 610 files in resource/DiscImg the
    // alpha>=128 edge is 0.7784 of the canvas side. That ratio is a property of
    // the TEXTURE, so disc-convention art gets the full authored 142 quad and
    // reaches 142 * 0.7784 = 110.5 by itself, while a full-bleed square jacket
    // has to be cropped to 110.5 by us. Both mistakes are pinned here: sizing
    // `image` to 110.5 applies the ratio twice and shrinks the artwork to 86px
    // (a ring of bare chrome shows), and stretching the square `fallback` over
    // the whole 142 socket puts corners over the chrome. stage_disc_ring.png's
    // ink is (9, 9)-(129, 129) with solid chrome from r 54, so its aperture is
    // 108 across, centred card-local (74.5, 74.5) = disc-local (70.5, 71.5).
    private static void VerifyDiscGeometry(string uss, string uxml)
    {
        string image = Block(uss, ".allclear-v2-disc-image {",
            "The stage disc rule .allclear-v2-disc-image {");
        foreach (string property in new[]
        {
            "left: 0;",
            "top: 0;",
            "width: 142px;",
            "height: 142px;",
            "border-top-left-radius: 71px;",
            "border-top-right-radius: 71px;",
            "border-bottom-left-radius: 71px;",
            "border-bottom-right-radius: 71px;",
            "-unity-background-scale-mode: scale-and-crop;"
        })
        {
            Require(image.Contains(property), ".allclear-v2-disc-image is not " +
                "Total_Result_Star.vce L2's 142x142 quad; missing " + property);
        }

        string fallback = Block(uss, ".allclear-v2-disc-fallback {",
            "The stage disc rule .allclear-v2-disc-fallback {");
        foreach (string property in new[]
        {
            "left: 15.25px;",
            "top: 16.25px;",
            "width: 110.5px;",
            "height: 110.5px;",
            "border-top-left-radius: 55.25px;",
            "border-top-right-radius: 55.25px;",
            "border-bottom-left-radius: 55.25px;",
            "border-bottom-right-radius: 55.25px;",
            "-unity-background-scale-mode: scale-and-crop;"
        })
        {
            Require(fallback.Contains(property), ".allclear-v2-disc-fallback " +
                "is not the 110.5 eyecatch circle; missing " + property);
        }

        // Inline UXML styles beat USS, so a per-card scale mode would silently
        // reinstate the stretched square on every stage at once.
        Require(!uxml.Contains("class=\"allclear-v2-disc-image\" style="),
            "A stage disc image carries an inline style that overrides the disc circle.");
    }

    // The difficulty rim. The arcade itself has none -- Total_Result_Star.vce
    // draws one static per-mode bezel crop per stage and never tints it by
    // difficulty -- so this is the theme's own, reusing the four Disc Border
    // canvases the single-song Result screen already paints over its 264 disc.
    // Two things decide whether it is visible or merely present: the quad that
    // lands its ink on the bezel's ink centre, and a draw order that puts it
    // over the chrome. Measured under the bezel, only 52% of the ink survived --
    // a ~4px band at r 50..54 at mean alpha 0.32-0.51, which reads as no rim.
    private static void VerifyDifficultyRing(string uss, string uxml,
        string screen, string assets, string projectPath)
    {
        string rim = Block(uss, ".allclear-v2-disc-border {",
            "The stage disc rule .allclear-v2-disc-border {");
        foreach (string property in new[]
        {
            "left: 0.25px;",
            "top: 1.5px;",
            "width: 140px;",
            "height: 140px;",
            "border-top-left-radius: 70px;",
            "border-bottom-right-radius: 70px;",
            "-unity-background-scale-mode: scale-to-fit;"
        })
        {
            Require(rim.Contains(property), ".allclear-v2-disc-border no longer " +
                "parks its 109.4 ink circle on the bezel and artwork ink centre, " +
                "disc-local (70.5, 71.5); missing " + property);
        }

        MatchCollection discs = Regex.Matches(uxml,
            "<ui:VisualElement name=\"disc\" class=\"allclear-v2-disc\">(.*?)" +
            "</ui:VisualElement>", RegexOptions.Singleline);
        Require(discs.Count == 3, "The All Clear needs three stage discs; found " +
            discs.Count + ".");
        foreach (Match disc in discs)
        {
            string children = disc.Groups[1].Value;
            int bezel = children.IndexOf("class=\"allclear-v2-disc-ring\"",
                StringComparison.Ordinal);
            int colour = children.IndexOf("class=\"allclear-v2-disc-border\"",
                StringComparison.Ordinal);
            Require(bezel >= 0 && colour >= 0,
                "A stage disc is missing either the bezel or the difficulty rim.");
            Require(colour > bezel, "A stage disc draws the difficulty rim under " +
                "the bezel, where the chrome's gloss dims it to nothing.");
        }

        string show = Section(screen,
            "ShowDifficultyRing = function(border, result)", "\n    end,");
        Require(show.Contains("local path = result.discBorderPath") &&
            show.Contains("arcadeAllClearV2Assets.DifficultyRing(result.patternName)") &&
            show.Contains("border.display = true"),
            "ShowDifficultyRing must take Result Screen.txt's discBorderPath when " +
            "a real credit carries one and fall back to patternName, which is all " +
            "the F2 preview harness supplies.");
        Require(screen.Contains(
            "image.backgroundImage = tm.io.LoadTextureFromTheme(result.discThemePath)\n" +
            "            image.display = true\n" +
            "            arcadeAllClearV2Screen.ShowDifficultyRing(border, result)"),
            "The theme-art disc path returns before showing the difficulty rim, " +
            "so every preview stage loses it.");
        Require(screen.Contains(
            "if (source != \"disc\") then\n" +
            "            arcadeAllClearV2Screen.ShowDifficultyRing(border, result)\n" +
            "        end"),
            "The rim must be resolved from `source` synchronously: deciding it " +
            "inside the load callback blinks a rim onto disc art that already " +
            "carries one, and drops it when the artwork fails to load.");
        string release = Section(screen, "ReleaseDynamicTextures = function()",
            "\n    end,");
        Require(release.Contains("disc.Q(\"border\").backgroundImage = nil"),
            "ReleaseDynamicTextures leaves the rim texture bound, so the previous " +
            "credit's difficulty colour survives into the next one.");

        string resolver = Section(assets, "DifficultyRing = function(patternName)",
            "\n    end,");
        Require(resolver.Contains(
                "\"Assets/UI/Sprites/Disc Border/\" .. patternName .. \".png\""),
            "DifficultyRing must resolve the same Disc Border canvases the " +
            "single-song Result screen draws.");
        Require(assets.Contains(
                "difficultyRingNames = {NM = true, HD = true, MX = true, EX = true},"),
            "DifficultyRing has no whitelist, so an unexpected patternName would " +
            "ask the theme for a texture that does not exist.");
        foreach (string difficulty in new[] { "NM", "HD", "MX", "EX" })
        {
            Require(File.Exists(Path.Combine(projectPath, "Assets", "UI",
                    "Sprites", "Disc Border", difficulty + ".png")),
                "The difficulty rim texture " + difficulty + ".png is missing " +
                "from Assets/UI/Sprites/Disc Border.");
        }
    }

    private static void RequireRect(string block, int left, int top, int width,
        int height, string name)
    {
        RequireProperty(block, name, "left", left);
        RequireProperty(block, name, "top", top);
        RequireProperty(block, name, "width", width);
        RequireProperty(block, name, "height", height);
    }

    // The total star-point numerals. Every tier document parks six 128x128
    // numeral quads and reveals a subset: NNstar_eff L23-L25 is the three-digit
    // row, L26-L27 the two-digit row, L28 the lone one-digit socket (L23's crop
    // is the atlas' second cell because the hundreds digit is always the 1 of
    // 100). Settled centers at frame 169, in 1280x768 canvas pixels:
    //
    //   tier   L23   L24    L25    L26   L27    L28    centre y
    //   100    931   1010   1097    987  1076   1032   623
    //    90    941   1021   1109    989  1078   1035   618
    //    80    941   1021   1110    991  1080   1037   614
    //    50    941   1020   1108    991  1080   1032   616
    //    00    942   1022   1110    991  1080   1034   614
    //
    // The container's global left is 892.5 and a glyph is 97 wide, so a socket
    // centre is 941 + left and its y is 618 + top. Tier 100 therefore reads
    // {-10, 69, 156} / +5, the two-digit tiers park element 1 on L23 and place
    // elements 2 and 3 on L26/L27, and a value under ten lands element 3 on L28
    // at +93. Each row below is that arithmetic run backwards.
    private static void VerifyStarDigitSockets(string screen)
    {
        string layout = Section(screen, "LayoutTotalStarDigits = function(",
            "\n    end,");
        RequireLefts(layout, "-10, 69, 156", 5, "tier 100 (three digits)");
        RequireLefts(layout, "0, 48, 137", 0, "tier 90");
        RequireLefts(layout, "0, 50, 139", -4, "tiers 80 and 0 (two digits)");
        RequireLefts(layout, "0, 50, 139", -2, "tier 50");
        RequireLefts(layout, "0, 0, 93", -4, "a single digit");
        Require(layout.Contains("StyleLength(97)") && layout.Contains("StyleLength(128)"),
            "The total star numerals are no longer the authored 97x128 glyph.");
        Require(layout.Contains("arcadeAllClearV2Screen.StarPointGroup(points)"),
            "LayoutTotalStarDigits must pick its tier through StarPointGroup, " +
            "or the numerals can land on a socket from a different tier file.");
    }

    private static void RequireLefts(string layout, string lefts, int top,
        string what)
    {
        Require(layout.Contains("lefts = {" + lefts + "}"),
            "The star numeral lefts for " + what + " are not {" + lefts +
            "}; NNstar_eff L23-L28 is the authority for these sockets.");
        Require(Regex.IsMatch(layout, @"top = " + top + @"\b"),
            "No star numeral row sits at top " + top + " any more, but " + what +
            " is authored there.");
    }

    private static void RequireProperty(string block, string name,
        string property, int value)
    {
        Require(block.Contains(property + ": " + value + "px;"),
            "The " + name + " anchor's " + property + " is not " + value +
            "px; the Total_Result dummy quad is the authority for this socket.");
    }

    // A preview fixture must never be able to reach arcade state, and a real
    // credit must never be able to reach the preview escape hatches.
    private static void VerifyPreviewGuards(string screen)
    {
        Require(screen.Contains(
                "if (credit.preview == true and credit.previewFinaleRank != nil) then"),
            "The forced-finale override is not gated on credit.preview; a real " +
            "credit could reach rank_d / rank_f.");

        int requestExit = screen.IndexOf("RequestExit = function(reason, ownerKind)",
            StringComparison.Ordinal);
        int previewBranch = screen.IndexOf("arcadeAllClearV2Screen.currentCredit.preview == true",
            StringComparison.Ordinal);
        Require(requestExit >= 0 && previewBranch > requestExit,
            "RequestExit has no preview branch, so a preview would try to claim " +
            "an arcade transition and block on the card-removal gate.");
        string exitBranch = Section(screen,
            "arcadeAllClearV2Screen.currentCredit.preview == true", "\n        end");
        Require(exitBranch.Contains("StopCoroutineField(fieldName, ownerKind)"),
            "The preview exit must honour ownerKind, or a coroutine asking to " +
            "exit stops itself mid-teardown.");
        int stops = exitBranch.IndexOf("StopCoroutineField(fieldName, ownerKind)",
            StringComparison.Ordinal);
        int finalize = exitBranch.IndexOf("FinalizePresentation()", StringComparison.Ordinal);
        int hide = exitBranch.IndexOf("arcadeAllClearV2Screen.Hide()", StringComparison.Ordinal);
        Require(stops >= 0 && finalize > stops && hide > finalize,
            "The preview exit must stop coroutines, then land the presentation, " +
            "then hide -- the same order as the credit path.");
        Require(exitBranch.Contains("arcadeAllClearPreview.OnPreviewClosed()"),
            "The preview exit never hands control back to the harness, so the " +
            "title screen would be left unfocused with the idle timer disarmed.");
        Require(!exitBranch.Contains("arcadeEndingScreen"),
            "The preview exit must not continue into the arcade Ending.");
    }

    // The harness must delegate every scoring decision. If it grows its own
    // copy of the curve, the presets stop being evidence about the real screen.
    private static void VerifyHarness(string preview)
    {
        Require(preview.Contains("arcadeAllClearPreview = {"),
            "The All Clear preview harness is missing.");
        Require(preview.Contains("arcadeSession.RankForTotalScore(") &&
            preview.Contains("arcadeSession.AggregateStarPoints(") &&
            preview.Contains("arcadeAllClearV2Screen.StarPointGroup("),
            "The harness must resolve ranks and star tiers through the " +
            "production functions, not its own copies.");
        Require(!Regex.IsMatch(preview, @">=\s*\d{6}"),
            "The harness has grown its own six-digit score threshold; the curve " +
            "lives in arcadeSession.RankForTotalScore and nowhere else.");
        Require(preview.Contains("arcadeAllClearV2Screen.ValidateCredit(credit)"),
            "SelfTest must put every fixture through the screen's own validation.");
        Require(preview.Contains("SelfTest = function()") &&
            preview.Contains("SELF-TEST PASSED") &&
            preview.Contains("SELF-TEST FAILED"),
            "SelfTest must report a verdict both ways.");
        Require(preview.Contains("arcadeAllClearV2Screen.LoadRecordTrophy("),
            "SelfTest must prove each fixture's record trophy actually loads.");
        Require(preview.Contains("arcadeAllClearV2Assets.FinaleTexture(textureKey)"),
            "SelfTest must prove every finale layer texture key resolves.");

        Require(preview.Contains("preview = true,"),
            "BuildCredit must mark its credits as previews.");
        Require(preview.Contains("if (not arcadeAllClearScreen.LoadV2()) then"),
            "The harness must lazy-load the V2 modules like the real entry does.");
        Require(preview.Contains("arcadeAllClearV2Screen.ShowWithCredit(credit)"),
            "The harness must hand its fixture straight to the screen instead of " +
            "mutating arcadeSession.credit.");
        Require(!preview.Contains("arcadeSession.credit ="),
            "A preview must never write arcade session state.");
        Require(preview.Contains("arcadeAttractScreen.DisarmIdleTimer()"),
            "The attract idle timer would cut a preview off part-way through.");
        Require(preview.Contains("tm.InEditor() or themeOptions.GetBoolWithDefault("),
            "F9 / F10 must be always-live in the editor and opt-in in a build.");

        // Pinned so the C# replication below cannot drift from the fixtures.
        Require(preview.Contains("local total = 500") &&
            preview.Contains("if (points >= 100) then return total, 0, 0, total end") &&
            preview.Contains("if (points < 2) then return points * 5, 0, 0, total end") &&
            preview.Contains("return points * 5 - 7, 10, 0, total"),
            "StarCounts changed; this test's replication of it must change too.");
    }

    private static readonly string[] PanelControls =
    {
        "allclear-preview-variant", "allclear-preview-badges",
        "allclear-preview-new-record", "allclear-preview-hotkeys",
        "allclear-preview-show", "allclear-preview-self-test",
        "allclear-preview-status"
    };

    private static void VerifyPanel(string attract, string uxml, string boot)
    {
        int previewLoad = boot.IndexOf("Arcade All Clear Preview.txt",
            StringComparison.Ordinal);
        int menuLoad = boot.IndexOf("Attract Settings Menu.txt",
            StringComparison.Ordinal);
        Require(previewLoad >= 0 && menuLoad > previewLoad,
            "MainScript must load the All Clear preview before the Attract " +
            "Settings menu, whose Initialize reads the preset list at load time.");

        int menuRoot = uxml.IndexOf("name=\"attract-settings-menu\"",
            StringComparison.Ordinal);
        Require(menuRoot >= 0, "The Attract Settings menu is missing from MainTree.uxml.");
        foreach (string name in PanelControls)
        {
            int control = uxml.IndexOf("name=\"" + name + "\"", StringComparison.Ordinal);
            Require(control > menuRoot,
                "MainTree.uxml has no " + name + " inside the Attract Settings menu.");
            Require(attract.Contains("\"" + name + "\""),
                "Attract Settings Menu.txt never binds " + name + ", so the " +
                "control would exist but do nothing.");
        }

        Require(attract.Contains("previewVariant.choices = arcadeAllClearPreview.Labels()"),
            "The preset dropdown is not filled from the harness's own list.");
        Require(attract.Contains("previewBadges.choices = arcadeAllClearPreview.badgeModes"),
            "The badge dropdown is not filled from the harness's own list.");
        Require(attract.Contains("arcadeAllClearPreview.ShowSelected(nil)"),
            "SHOW ALL CLEAR does not start the selected preset.");
        Require(attract.Contains("arcadeAllClearPreview.SelfTest()"),
            "RUN SELF-TEST does not run the self-test.");
        Require(attract.Contains("SetPreviewStatus = function(text)") &&
            attract.Contains("attractSettingsMenu.SetPreviewStatus(\"\")"),
            "The panel must own a status line and clear it on every open, or a " +
            "stale verdict reads as a fresh one.");
        Require(attract.Contains("arcadeAllClearPreview.SelectedNewRecord()"),
            "The NEW RECORD toggle is not read back from the harness.");
    }

    private static readonly int[] RankThresholds =
        { 295000, 285000, 270000, 245000, 230000, 220000, 190000 };
    private static readonly string[] RankLetters =
        { "S++", "S+", "S", "A++", "A+", "A", "B" };

    private static int VerifyFixtures(string preview, string session)
    {
        MatchCollection curve = Regex.Matches(session,
            @"if \(score >= (\d+) \* stageCount\) then return ""([SAB+]+)"" end");
        Require(curve.Count == RankThresholds.Length,
            "RankForTotalScore has " + curve.Count + " thresholds, not " +
            RankThresholds.Length + "; this test's copy of the curve must follow.");
        for (int i = 0; i < curve.Count; i++)
        {
            Require(int.Parse(curve[i].Groups[1].Value, CultureInfo.InvariantCulture)
                    == RankThresholds[i] &&
                curve[i].Groups[2].Value == RankLetters[i],
                "RankForTotalScore step " + i + " is " +
                curve[i].Groups[2].Value + " at " + curve[i].Groups[1].Value +
                ", not " + RankLetters[i] + " at " + RankThresholds[i] + ".");
        }
        Require(Regex.IsMatch(session, @"return ""C""\s*\n\s*end,"),
            "RankForTotalScore must still bottom out at C, which is why the D " +
            "and F presets carry forceRank.");
        Require(session.Contains(
                "(maxCount + coolCount * 0.7 + goodCount * 0.4) * 100 / totalNotes"),
            "AggregateStarPoints no longer uses the measured star-point weights.");

        string variants = Section(preview, "variants = {", "\n    },");
        string[] chunks = variants.Split(new[] { "{ label = " },
            StringSplitOptions.None);
        Require(chunks.Length > 1, "The harness declares no presets.");
        var popFinales = new HashSet<string>();
        var starTiers = new HashSet<int>();
        for (int i = 1; i < chunks.Length; i++)
        {
            EvaluatePreset(chunks[i], popFinales, starTiers);
        }
        foreach (string letter in new[] { "S", "A", "B", "C", "D", "F" })
        {
            Require(popFinales.Contains(letter),
                "No preset reaches the pop finale " + letter + ", so rank_" +
                letter.ToLowerInvariant() + ".vce would go unchecked.");
        }
        foreach (int tier in new[] { 0, 50, 80, 90, 100 })
        {
            Require(starTiers.Contains(tier),
                "No preset reaches the star tier " + tier + ".");
        }
        return chunks.Length - 1;
    }

    private static void EvaluatePreset(string chunk, HashSet<string> popFinales,
        HashSet<int> starTiers)
    {
        string label = Regex.Match(chunk, "^\"([^\"]+)\"").Groups[1].Value;
        Require(label.Length > 0, "A preset has no label.");
        string mode = Regex.Match(chunk, "mode = \"(pop|star)\"").Groups[1].Value;
        Require(mode.Length > 0, label + ": mode must be pop or star.");
        // Pop expects a quoted rank letter (S, A, B, C, D, F), star an unquoted
        // tier number, so the letters cannot be narrowed to a hex-looking range.
        string expect = Regex.Match(chunk, @"expect = ""?([A-Z+]+|\d+)""?").Groups[1].Value;
        Require(expect.Length > 0, label + ": no expected finale.");

        int[] scores = Numbers(chunk, "scores", label);
        Require(scores.Length == 3, label + ": a credit is exactly three stages.");
        RequireTriple(chunk, "combos", label);
        foreach (string pattern in Strings(chunk, "patterns", label))
        {
            Require(pattern == "NM" || pattern == "HD" || pattern == "MX" ||
                pattern == "EX",
                label + ": pattern " + pattern + " has no trophy texture row.");
        }
        foreach (string trophy in Strings(chunk, "trophies", label))
        {
            Require(trophy == "bronze" || trophy == "silver" || trophy == "gold",
                label + ": trophy " + trophy + " is not one of the three the " +
                "Result screen can actually assign.");
        }

        if (mode == "pop")
        {
            int total = scores[0] + scores[1] + scores[2];
            string forced = Regex.Match(chunk, "forceRank = \"([DF])\"").Groups[1].Value;
            string rank = forced.Length > 0 ? forced : RankForTotalScore(total, 3);
            string basis = rank.Replace("+", string.Empty);
            Require(basis == expect, label + ": total " + total +
                " resolves to finale " + basis + ", not " + expect + ".");
            if (forced.Length > 0)
            {
                Require(RankForTotalScore(total, 3) == "C", label +
                    ": a forced finale must sit below the reachable curve, but " +
                    total + " already earns " + RankForTotalScore(total, 3) + ".");
            }
            popFinales.Add(basis);
            return;
        }

        int[] points = Numbers(chunk, "points", label);
        Require(points.Length == 3, label + ": star presets need three point values.");
        int max = 0, cool = 0, good = 0, notes = 0;
        foreach (int value in points)
        {
            int[] counts = StarCounts(value);
            Require(counts[0] + counts[1] + counts[2] <= counts[3], label +
                ": " + value + " points needs more judgements than the chart has notes.");
            max += counts[0];
            cool += counts[1];
            good += counts[2];
            notes += counts[3];
        }
        int aggregate = (int)Math.Floor((max + cool * 0.7 + good * 0.4) * 100 / notes);
        int tier = StarPointGroup(aggregate);
        Require(tier.ToString(CultureInfo.InvariantCulture) == expect, label +
            ": " + aggregate + " star points land in tier " + tier + ", not " + expect + ".");
        starTiers.Add(tier);
    }

    // Replications. Each one is pinned to its Lua original by an assertion above,
    // so they cannot drift silently.
    private static string RankForTotalScore(int score, int stageCount)
    {
        for (int i = 0; i < RankThresholds.Length; i++)
        {
            if (score >= RankThresholds[i] * stageCount) return RankLetters[i];
        }
        return "C";
    }

    private static int StarPointGroup(int points)
    {
        if (points >= 100) return 100;
        if (points >= 90) return 90;
        if (points >= 80) return 80;
        if (points >= 50) return 50;
        return 0;
    }

    // { maxCount, coolCount, goodCount, totalNotes } that reproduce `points`
    // exactly under the star-point formula.
    private static int[] StarCounts(int points)
    {
        const int total = 500;
        if (points >= 100) return new[] { total, 0, 0, total };
        if (points < 2) return new[] { points * 5, 0, 0, total };
        return new[] { points * 5 - 7, 10, 0, total };
    }

    private static int[] Numbers(string chunk, string field, string label)
    {
        Match match = Regex.Match(chunk, field + @" = \{([^}]*)\}");
        Require(match.Success, label + ": no " + field + " list.");
        string[] parts = match.Groups[1].Value.Split(',');
        var values = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            values[i] = int.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
        }
        return values;
    }

    private static string[] Strings(string chunk, string field, string label)
    {
        Match match = Regex.Match(chunk, field + @" = \{([^}]*)\}");
        Require(match.Success, label + ": no " + field + " list.");
        MatchCollection items = Regex.Matches(match.Groups[1].Value, "\"([^\"]*)\"");
        Require(items.Count == 3, label + ": " + field + " must cover three stages.");
        var values = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            values[i] = items[i].Groups[1].Value;
        }
        return values;
    }

    private static void RequireTriple(string chunk, string field, string label)
    {
        Require(Numbers(chunk, field, label).Length == 3,
            label + ": " + field + " must cover three stages.");
    }

    private static string Block(string source, string header, string what)
    {
        int start = source.IndexOf(header, StringComparison.Ordinal);
        Require(start >= 0, what + " is missing (" + header + ").");
        int end = source.IndexOf('}', start);
        Require(end > start, what + " block is unterminated.");
        return source.Substring(start, end - start);
    }

    private static string Section(string source, string header, string terminator)
    {
        int start = source.IndexOf(header, StringComparison.Ordinal);
        Require(start >= 0, "Missing block: " + header);
        int end = source.IndexOf(terminator, start, StringComparison.Ordinal);
        Require(end > start, "Unterminated block: " + header);
        return source.Substring(start, end - start);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
