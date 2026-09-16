using System;
using System.IO;
using UnityEngine;

public static class ResultVceVerification
{
    public static void RunStarPointDigitChecks()
    {
        string resultScreen = File.ReadAllText(Path.Combine(
            Application.dataPath, "UI", "Scripts", "Result Screen.txt"))
            .Replace("\r\n", "\n");
        string mainTree = File.ReadAllText(Path.Combine(
            Application.dataPath, "UI", "MainTree.uxml"));

        Require(resultScreen.Contains("local oneDigitLeft = 979.5"),
            "Star Point one-digit centered socket is missing.");
        Require(resultScreen.Contains(
            "local twoDigitLefts = {938.5, 1020.5}"),
            "Star Point two-digit centered sockets are missing.");
        Require(resultScreen.Contains(
            "local threeDigitLefts = {897.8, 978.8, 1060.8}"),
            "Star Point three-digit centered sockets are missing.");
        Require(resultScreen.Contains(
            "PositionStarPointDigits(animationGroup, starPoints)"),
            "Dynamic Star Point values do not select VCE sockets by digit count.");
        Require(resultScreen.Contains(
            "PositionStarPointDigits(hundredPointGroup, 100)"),
            "The 100-point result does not use the three-digit VCE sockets.");
        Require(resultScreen.Contains("ForceFailedStarZero = function") &&
            resultScreen.Contains("resultScreen.ForceFailedStarZero(animationGroup)"),
            "Failed Star Mixing does not force its visible zero digit.");

        // result_rec.vce L83/L84 -- the only 128x128 dummy sockets in any
        // single-song document, byte-identical in results/pop and results/star.
        Require(resultScreen.Contains("local upperMedalSlotLeft = 824") &&
            resultScreen.Contains("local upperMedalSlotTop = 512"),
            "The upper achievement canvas is not on its result_rec L83 dummy anchor.");
        Require(resultScreen.Contains("local lowerMedalSlotLeft = 824") &&
            resultScreen.Contains("local lowerMedalSlotTop = 575"),
            "The lower trophy canvas is not on its result_rec L84 dummy anchor.");
        Require(mainTree.Contains(
            "name=\"medal-or-trophy-1\" picking-mode=\"Ignore\" style=\"position: absolute; width: 128px; height: 128px; left: 824px; top: 512px;\"") &&
            mainTree.Contains(
            "name=\"medal-or-trophy-2\" picking-mode=\"Ignore\" style=\"position: absolute; width: 128px; height: 128px; left: 824px; top: 575px;\""),
            "The static badge slots in MainTree drift from the result_rec anchors.");
        // Both badge PNGs are 128x128 canvases whose transparent margins already
        // encode the arcade ink size, so they draw at 1:1 in the 128 slot. The
        // old 96/80 pair shrank the ink and needed the 577 row and hand offsets
        // to stay visually clear of each other.
        Require(resultScreen.Contains("local achievementVisualSize = 128") &&
            resultScreen.Contains("local trophyVisualSize = 128"),
            "Result achievements and trophies do not fill their arcade 128 quad.");
        Require(resultScreen.Contains("trophyInSlot2.display = true"),
            "All Combo and Perfect Play do not retain the result trophy.");
        Require(resultScreen.Contains(
            "else\n            SetMedalSlot(medalOrTrophy1, upperMedalSlotLeft, upperMedalSlotTop)"),
            "A result without an achievement does not place its trophy in the upper slot.");
        Require(mainTree.Contains(
            "class=\"combo-digits\" style=\"width: 48px; position: absolute; height: 14px; top: 79px; left: 436px; flex-direction: row; justify-content: flex-end;\""),
            "Popup Max Combo digits are not right-aligned to the VCE record edge.");

        // result_rec.vce draws the 264 bezel plate (L79 at 106,185) OVER the
        // 300 disc socket (L78 at 88,166), so the plate has to be a topmost
        // child rather than the holder's own background -- an element's
        // background always renders under its children. `disc-image` takes
        // disc-convention art, whose own alpha supplies the circle at 0.7784 of
        // the canvas (measured over the 610 DiscImg files), so it gets L78's
        // whole 300x300 quad: holder-local (-18, -19) because the holder starts
        // at (106, 185). The art then reaches 300 * 0.7784 = 233.5 by itself and
        // tucks ~6px under disc_holder.png's chrome band (r 110.5..123.75).
        // Sizing the box to 233.5 applied the ratio twice and drew it at 181px.
        Require(mainTree.Contains(
            "name=\"disc-holder\" picking-mode=\"Ignore\" style=\"position: absolute; left: 106px; top: 185px; width: 264px; height: 264px;\""),
            "The Result disc holder is not the bare result_rec L79 quad.");
        Require(mainTree.Contains(
            "name=\"disc-bezel\" picking-mode=\"Ignore\" class=\"fully-expand\" style=\"background-image: url(&quot;project://database/Assets/UI/Sprites/Result/disc_holder.png"),
            "The Result disc bezel is not a child element drawing over the jacket.");
        Require(mainTree.IndexOf("name=\"disc-bezel\"", StringComparison.Ordinal) >
            mainTree.IndexOf("name=\"disc-or-eyecatch\"", StringComparison.Ordinal),
            "The Result disc bezel does not draw over the jacket.");
        // The difficulty rim belongs on top of the chrome, not under it.
        // disc_holder.png's gloss covers r 36..107 of the 264 box at alpha up
        // to 176, which erased a rim whose own ink lands at r 95.9..101.6, so
        // nesting it inside `disc-or-eyecatch` made it invisible. Same
        // convention as Arcade All Clear V2 Screen's ShowDifficultyRing.
        Require(mainTree.IndexOf("name=\"disc-border\"", StringComparison.Ordinal) >
            mainTree.IndexOf("name=\"disc-bezel\"", StringComparison.Ordinal),
            "The Result difficulty rim is painted under the bezel gloss.");
        Require(resultScreen.Contains(
            "local discBorder = discHolder.Q(\"disc-border\")"),
            "Result Screen no longer resolves the difficulty rim from the holder.");
        Require(resultScreen.Contains("discBorder.display = true") &&
            resultScreen.Contains("discBorder.display = false"),
            "Result Screen does not state the difficulty rim's visibility in " +
            "both disc branches; it no longer inherits masked-eyecatch's.");
        Require(mainTree.Contains(
            "name=\"disc-image\" picking-mode=\"Ignore\" style=\"position: absolute; left: -18px; top: -19px; width: 300px; height: 300px; overflow: hidden; border-top-left-radius: 150px;"),
            "Result disc art is not result_rec.vce L78's 300x300 quad at the bezel centre.");
        Require(mainTree.Contains(
            "border-bottom-right-radius: 150px; -unity-background-scale-mode: scale-and-crop;"),
            "Result disc art is not circle-clipped and cropped to fill.");

        // All seven result rules share one 292x4 cut of result_cut1.png at
        // (556,928), but none of them is drawn at that size: L2/L4/L6/L8/L10 and
        // L12 stretch it to 333 wide and L14 to 363. Measuring the layout off the
        // crop instead of the drawn quad left every rule 41px short and, because
        // the shortfall was symmetric, 20.5px right of its column.
        Require(resultScreen.Contains("dividerLeft = 430.5") &&
            resultScreen.Contains("dividerWidth = 333"),
            "The result judgement rules are not on their result_rec column and width.");
        Require(resultScreen.Contains("maxComboDivider = {431.5, 664}") &&
            resultScreen.Contains("MoveIn(breakdown.Q(\"divider-6\"), 95, 110, {131, 0}, -15)"),
            "The MAX COMBO rule from result_rec.vce L12 is missing.");
        for (int i = 1; i <= 5; i++)
        {
            Require(mainTree.Contains("name=\"divider-" + i + "\" picking-mode=\"Ignore\" " +
                "style=\"position: absolute; width: 333px; height: 4px;"),
                "Result divider " + i + " is not the arcade's 333x4 drawn quad.");
        }
        Require(mainTree.Contains("name=\"divider-6\" picking-mode=\"Ignore\" " +
                "style=\"position: absolute; width: 333px; height: 4px;") &&
            mainTree.Contains("left: 431.5px; top: 664px;"),
            "MainTree is missing the MAX COMBO rule element.");
        // L14 hangs the score rule under the digits at 814.5, not under the SCORE
        // label at 810, and it is the widest of the seven at 363. It also flies in
        // on the judgement rules' quartic rather than the score digits' cubic:
        // a third of the way through, 115 * (2/3)^4 = 22.72, which is its key.
        Require(resultScreen.Contains("scoreDivider = {814.5, 450, 363}") &&
            resultScreen.Contains("SetRect(breakdown.Q(\"score-divider\"), " +
                "layout.scoreDivider[1], layout.scoreDivider[2],"),
            "The score rule is not on its result_rec L14 column.");
        Require(mainTree.Contains("name=\"score-divider\" picking-mode=\"Ignore\" " +
                "style=\"position: absolute; width: 363px; height: 4px;") &&
            mainTree.Contains("left: 814.5px; top: 450px;"),
            "MainTree's score rule is not the arcade's 363x4 drawn quad.");
        Require(resultScreen.Contains(
            "MoveIn(breakdown.Q(\"score-divider\"), 110, 125, {115, 0}, -15)"),
            "The score rule no longer flies in on the L14 quartic.");
        // One arcade cut serves all seven rules, so they take one texture.
        Require(resultScreen.Contains("for i = 1, 6 do") &&
            !resultScreen.Contains("divider_2.png"),
            "The result rules no longer share the single arcade divider cut.");

        // Every arcade digit run is packed tighter than the glyph art: 21px
        // count and percentage glyphs on a 17px pitch, 28px score glyphs on 26,
        // 17px MY RECORD glyphs on 15, and five bonus glyphs across 43px. A flex
        // row would shrink the art to fit instead, so SetChildrenPitch keeps the
        // exact VCE sizes and overlaps them with transforms, which never feed
        // back into layout.
        Require(resultScreen.Contains(
            "local function SetChildrenPitch(parent, width, height, pitch)") &&
            resultScreen.Contains(
            "child.style.translate = StyleTranslate((pitch - width) * count, 0)"),
            "The digit pitch helper is missing or no longer uses a transform.");
        // Every result-screen digit rule in MainStyle.uss carries its own negative
        // margin-right to overlap the glyphs, and a margin IS layout: leaving it
        // in place applies the pitch twice, once through the flex row and again
        // through the transform. The judgement runs came out on a 13px pitch
        // instead of 17 and every 26px run at 24. The helper owns the pitch, so
        // it has to neutralise the margin first.
        Require(resultScreen.Contains("child.style.marginLeft = StyleLength(0)") &&
            resultScreen.Contains("child.style.marginRight = StyleLength(0)"),
            "SetChildrenPitch no longer cancels the USS margin, so the pitch doubles.");
        Require(resultScreen.Contains("SetChildrenPitch(row.Q(\"number\"), 21, 27, 17)") &&
            resultScreen.Contains("SetChildrenPitch(row.Q(\"percentage\"), 21, 27, 17)"),
            "Judgement counts and percentages are not on the result_rec 17px pitch.");
        Require(resultScreen.Contains(
            "SetChildrenPitch(breakdown.Q(\"max-combo-value\"), 28, 38, 26)") &&
            resultScreen.Contains(
            "SetChildrenPitch(breakdown.Q(\"score-value\"), 28, 38, 26)") &&
            resultScreen.Contains(
            "SetChildrenPitch(breakdown.Q(\"new-record-score-value\"), 28, 38, 26)"),
            "MAX COMBO and score digits are not on the result_rec 26px pitch.");
        Require(resultScreen.Contains(
            "SetChildrenPitch(djInfoElement.Q(\"max-combo-record-digits\"), 17, 20, 15)") &&
            resultScreen.Contains(
            "SetChildrenPitch(djInfoElement.Q(\"record-digits\"), 17, 20, 15)"),
            "MY RECORD digits are not on the result_myinfo 15px pitch.");
        Require(resultScreen.Contains("local bonusPitch = 43 / 4"),
            "Bonus digits are not spread across the bouns_score 43px run.");

        // result_rec.vce L16-L25 (and L26-L29 five frames later for COOL): every
        // glyph in a judgement row punches in from an oversized copy on a fixed
        // centre, overshoots inward, then settles, all three legs linear. Digits
        // run 41x51 -> 15x21 -> 21x27, the brackets 25x58 -> 9x28 -> 13x34 and
        // the percent sign 45x58 -> 25x28 -> 31x34.
        Require(resultScreen.Contains(
            "ZoomInDigit(digit, startFrame, {41, 51}, {15, 21}, {21, 27})"),
            "Judgement digits do not zoom in on the result_rec sizes.");
        Require(resultScreen.Contains(
            "ZoomInDigit(valueContainer.Q(\"left-bracket\"), startFrame, " +
            "{25, 58}, {9, 28}, {13, 34})") &&
            resultScreen.Contains(
            "ZoomInDigit(valueContainer.Q(\"right-bracket\"), startFrame, " +
            "{25, 58}, {9, 28}, {13, 34})"),
            "Judgement brackets do not zoom in on the result_rec L20/L21 sizes.");
        Require(resultScreen.Contains(
            "ZoomInDigit(valueContainer.Q(\"percent-sign\"), startFrame, " +
            "{45, 58}, {25, 28}, {31, 34})"),
            "The percent sign does not zoom in on the result_rec L22 sizes.");
        // The fade lands on the inner keyframe, nine frames in, and the settle
        // takes three more.
        Require(resultScreen.Contains("FadeIn(element, startFrame, (startFrame + 9))") &&
            resultScreen.Contains("time = FrameTime(startFrame + 12), values = {1, 1}"),
            "The judgement zoom no longer settles on the result_rec 9/12 keys.");
        // Punctuation sits one pixel below the digit boxes in every row.
        Require(resultScreen.Contains("SetRect(row.Q(\"left-bracket\"), 75.5, 1, 13, 34)") &&
            resultScreen.Contains("SetRect(row.Q(\"percent-sign\"), 134.5, 1, 31, 34)") &&
            resultScreen.Contains("SetRect(row.Q(\"right-bracket\"), 160.5, 1, 13, 34)"),
            "Judgement punctuation is off its result_rec baseline.");

        // bouns_score.vce: the bright num_bonus copy flies in over frames
        // 125-135, rests on exactly the num_bonus2 columns (1111.5 / 1121.5),
        // holds, then slides 87px left into the score. The theme had anchored
        // both rows on that fly-away END position, so the bonus sat 87px too far
        // left for its entire hold.
        Require(!mainTree.Contains("left: 1024.5px") &&
            !mainTree.Contains("left: 1034.5px"),
            "A bonus row is still anchored on its fly-away end position.");
        Require(CountOccurrences(mainTree, "left: 1111.5px; top: 370.5px") == 2 &&
            CountOccurrences(mainTree, "left: 1111.5px; top: 390.5px") == 2,
            "The staying and flying bonus rows do not share their arcade columns.");
        Require(resultScreen.Contains("Appear(breakdown.Q(\"bonus-value-staying\"), 165)") &&
            resultScreen.Contains(
            "BonusValueFlyAway(bonusValueFlying.Q(\"fever-bonus\"), 170)") &&
            resultScreen.Contains(
            "BonusValueFlyAway(bonusValueFlying.Q(\"combo-bonus\"), 175)"),
            "The bonus hand-off no longer matches bouns_score.vce.");
        // The arcade note shipped with the Star result frames ("스코어 숫자
        // 올라가는 프레임 165~195") and the two fly-away windows agree on the
        // span the score ticker occupies.
        Require(resultScreen.Contains(
            "local startTime = 165 / 60 * resultScreen.animationTimeScale") &&
            resultScreen.Contains(
            "local endTime = 195 / 60 * resultScreen.animationTimeScale"),
            "The score ticker does not run over the arcade's 165-195 window.");

        // newrecord.vce L9 is an additive burst tinted (255,244,153) that
        // implodes from 800x600 to 8x6 on the banner's centre between frames
        // 140 and 170. L8 shows the banner is already a fifth opaque when it
        // starts flying, and L1-L7 fade the seven digits in two frames apart.
        Require(File.Exists(Path.Combine(Application.dataPath, "UI", "Sprites",
            "Result", "clearglow.jpg")),
            "The NEW RECORD burst texture from results/pop is not in the theme.");
        Require(mainTree.Contains("name=\"new-record-glow\"") &&
            mainTree.Contains("Additive%20Shader%20Material%20In%20Theme.mat"),
            "The NEW RECORD burst element is missing or is not additive.");
        Require(resultScreen.Contains(
            "SetRect(breakdown.Q(\"new-record-glow\"), 767, 159, 800, 600)") &&
            resultScreen.Contains("{ time = FrameTime(170), values = {0.01, 0.01} }"),
            "The NEW RECORD burst does not implode from 800x600 onto the banner.");
        Require(resultScreen.Contains("{ time = FrameTime(120), values = {51 / 255} }"),
            "The NEW RECORD banner does not start its flight already part-visible.");
        Require(resultScreen.Contains(
            "FadeIn(digit, newRecordDigitFrame, newRecordDigitFrame + 5)") &&
            resultScreen.Contains("newRecordDigitFrame = newRecordDigitFrame + 2"),
            "The NEW RECORD digits do not fade in two frames apart.");

        // resource/script/grade.csv weights a COOL at 0.9 of a MAX and a GOOD at
        // 0.85, then truncates the star point and caps it at 100.
        Require(CountOccurrences(resultScreen,
            "maxCount + coolCount * 0.9 + goodCount * 0.85") == 2 &&
            resultScreen.Contains("unity.mathf.Min(100, unity.mathf.FloorToInt("),
            "Star points are not on the arcade grade.csv weighting.");

        // result_rec.vce L87 is a 294x314 dummy that slides from (91,583) to
        // (91,459) over frames 30-49 on an exact cubic ease-out, and L85 is the
        // 292x192 no_card plate doing the same 200px slide onto (92,443).
        // result_myinfo.vce -- the sub-document those dummies host -- puts its
        // own 292x280 background at panel-local (1,17), so the anchor pads the
        // content by 1/1 horizontally and 17/17 vertically. That symmetry is
        // what proves the sub-document origin is the anchor's top-left, so the
        // panel has to sit at 459: at 462 every child inside it (effectors, DJ
        // card, MY RECORD digits) drew three pixels low.
        Require(mainTree.Contains(
            "name=\"dj-info-and-record\" picking-mode=\"Ignore\" style=\"position: absolute; " +
            "width: 294px; height: 314px; left: 91px; top: 459px;"),
            "The result DJ panel is not on its result_rec L87 dummy quad.");
        Require(mainTree.Contains(
            "name=\"no-card\" picking-mode=\"Ignore\" style=\"position: absolute; " +
            "left: 92px; top: 443px; width: 292px; height: 192px;"),
            "The result no-card plate is not on its result_rec L85 quad.");
        Require(resultScreen.Contains(
            "MoveInNoFade(content.Q(\"no-card\"), 30, 50, {0, 200}, -10)") &&
            resultScreen.Contains(
            "MoveInNoFade(content.Q(\"dj-info-and-record\"), 30, 50, {0, 124}, -10)"),
            "The result lower-left panels do not slide their result_rec distances.");
        // result_myinfo.vce hangs its three 56x56 blind.png effector dummies on
        // centres (67,114)/(146,114)/(225,114) -- panel-local top-left 39/118/197
        // at y 86 -- and its two digit runs at (192.5,211) and (162.5,240).
        string[] effectors = { "note-opacity", "scanline-opacity", "scan-direction" };
        int[] effectorLefts = { 39, 118, 197 };
        for (int i = 0; i < effectors.Length; i++)
        {
            Require(mainTree.Contains("name=\"" + effectors[i] + "\" picking-mode=\"Ignore\" " +
                "class=\"effector-button\" style=\"left: " + effectorLefts[i] + "px; top: 86px;\""),
                "Result effector " + effectors[i] + " is off its result_myinfo blind quad.");
        }
        // The pop/star copies of result_myinfo.vce carry a fourth blind, but
        // CLIENT.EXE only ever loads the userInfo copy, which has three. The
        // vestigial slot also overlapped scan-direction's 197..253 span.
        Require(!mainTree.Contains("extra-effector"),
            "The dead fourth effector slot is still in the result DJ panel.");
        Require(mainTree.Contains(
            "name=\"max-combo-record-digits\" picking-mode=\"Ignore\" style=\"position: absolute; " +
            "left: 192.5px; top: 211px;") &&
            mainTree.Contains(
            "name=\"record-digits\" picking-mode=\"Ignore\" style=\"position: absolute; " +
            "left: 162.5px; top: 240px;"),
            "MY RECORD digit runs are off their result_myinfo columns.");
        Debug.Log("ResultVceVerification result-layout checks passed.");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length,
                StringComparison.Ordinal);
        }
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
