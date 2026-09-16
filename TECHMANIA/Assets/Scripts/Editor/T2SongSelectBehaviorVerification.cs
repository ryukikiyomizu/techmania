using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

// Contracts for how DMT2's Song Select behaves, as opposed to how it is laid
// out. Two arcade sources drive this file:
//   * SongSelect/star/musicmove_l.vce and musicmove_r.vce (fps 60, maxkey 35)
//     for the page-turn cross-fade envelope.
//   * CLIENT.EXE's Game::CSongSelectState string table for the surprise slot:
//     "(iIndex(%d) == m_iSurpriseSong(%d))" chooses between
//     resource/songselect/question.bik + question.ogg and the song's own
//     resource/preview/<x>_pre.avi + <x>.ogg.
public static class T2SongSelectBehaviorVerification
{
    [MenuItem("TECHMANIA/Tests/Verify Song Select Behavior")]
    public static void Run()
    {
        List<string> failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                failures));
        UnityEngine.Debug.Log("[T2 Test] Song Select behavior contracts passed.");
    }

    public static List<string> Evaluate()
    {
        string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        string ui = Path.Combine(project, "Assets", "UI");
        string selectSong = Read(Path.Combine(ui, "Scripts", "Select Song Screen.txt"));
        string prepare = Read(Path.Combine(ui, "Scripts", "Prepare Screen.txt"));
        string tree = Read(Path.Combine(ui, "MainTree.uxml"));
        string style = Read(Path.Combine(ui, "MainStyle.uss"));
        string flat = Squash(selectSong);
        string flatPrepare = Squash(prepare);

        var failures = new List<string>();

        // --- Page turn: musicmove_l/r cross-fade, not a bare slide ----------
        Check(flat.Contains("element = currentPageElement, property = \"opacity\", " +
            "keyframes = { { time = 0, values = {1} }, { time = 0.25, values = {0} } }"),
            "The outgoing songs page does not fade 255 -> 0 across f0..f15 like musicmove_l.vce.",
            failures);
        Check(flat.Contains("element = otherPageElement, property = \"opacity\", " +
            "keyframes = { { time = 0.167, values = {0} }, { time = 0.417, values = {1} }, " +
            "{ time = 0.583, values = {1} } }"),
            "The incoming songs page does not fade 0 -> 255 across f10..f25 and hold to f35.",
            failures);
        Check(flat.Contains("otherPageElement.style.opacity = StyleFloat(0)"),
            "The incoming songs page is not primed at alpha 0 before the slide.", failures);
        Check(flat.Contains("currentPageElement.style.opacity = StyleFloat(1)"),
            "The parked songs page is never restored to full alpha after the slide.", failures);
        Check(selectSong.Contains("selectSongScreen.page1Element.style.opacity = StyleFloat(1)") &&
            selectSong.Contains("selectSongScreen.page2Element.style.opacity = StyleFloat(1)"),
            "Re-entering Song Select does not reset both page alphas, so a faded grid can persist.",
            failures);

        // --- Surprise slot: the rolled song stays hidden --------------------
        Check(selectSong.Contains("IsSurpriseCard = function(t3Track)"),
            "There is no predicate for the slot CSongSelectState::SetCurSel treats as the surprise song.",
            failures);
        Check(selectSong.Contains("Show = function(t3Track, positionInPage, isSurprise)") &&
            selectSong.Contains("selectSongPopup.isSurprise = isSurprise"),
            "The popup cannot tell a surprise slot from an ordinary card.", failures);
        Check(selectSong.Contains("selectSongPopup.isSurprise = false"),
            "Re-entering Song Select does not clear the surprise flag.", failures);
        Check(flat.Contains("selectSongPopup.currentT3Track == t3Track and " +
            "selectSongPopup.isSurprise == isSurprise"),
            "The popup fast path ignores surprise state, so a masked popup can be reused for a plain card.",
            failures);
        Check(flat.Contains("tm.io.LoadAudioFromTheme( \"Assets/UI/Music/question.ogg\")"),
            "The surprise slot does not substitute question.ogg for the song's own preview clip.",
            failures);
        Check(selectSong.Contains("tm.io.LoadVideoFromTheme(\"Assets/UI/Videos/question.mp4\""),
            "The surprise slot does not substitute question.bik for the song's own BGA preview.",
            failures);
        Check(selectSong.Contains("content.Q(\"texts\").Q(\"title\").text = \"RANDOM\""),
            "The surprise popup does not open on the arcade's masked ? ? ? / RANDOM rows.", failures);

        // --- Surprise slot: the metadata rows riffle fast enough to blur ------
        Check(selectSong.Contains("surpriseCycleInterval = 0.033,") &&
            selectSong.Contains("ShowSurpriseCycleEntry = function()"),
            "The surprise popup's metadata riffle is slow enough to read a title off.",
            failures);
        Check(flat.Contains("texts.Q(\"title\").text = metadata.title " +
            "texts.Q(\"genre\").text = metadata.genre " +
            "texts.Q(\"artist\").text = metadata.artist"),
            "The riffle does not swap all three rows (title, genre and artist).", failures);
        Check(flat.Contains("if (step != selectSongPopup.surpriseCycleStep) then " +
            "selectSongPopup.surpriseCycleStep = step " +
            "selectSongPopup.ShowSurpriseCycleEntry() end"),
            "FrameUpdate does not advance the riffle on a fixed interval.", failures);

        // --- Surprise slot: NM..EX stay pickable, the roll follows the pick --
        Check(selectSong.Contains("RandomSong = function(requiredDifficulty)") &&
            flat.Contains("if (t3Track.t3Patterns[requiredDifficulty] != nil) then " +
                "table.insert(pool, t3Track) end"),
            "The roll cannot be restricted to songs that carry the locked difficulty.", failures);
        Check(selectSong.Contains("LibraryDifficulties = function()") &&
            flat.Contains("offeredPatternNames = selectSongScreen.LibraryDifficulties()") &&
            selectSong.Contains("if (not offeredPatternNames[patternName]) then"),
            "The surprise popup offers one track's difficulties instead of the library's NM..EX.",
            failures);
        Check(flat.Contains("local rolled = selectSongScreen.RandomSong(patternName)"),
            "Locking a difficulty on the surprise popup does not re-roll within that difficulty.",
            failures);
        Check(selectSong.Contains("if (t3Pattern != nil and not isSurprise) then"),
            "The surprise popup still awards trophies, which identifies the hidden song.", failures);
        Check(flat.Contains("if (selectSongPopup.isSurprise == true) then visibleStars = 0 end"),
            "The surprise popup still shows the chart-derived star count.", failures);
        Check(flat.Contains("bestRecordScore = 0 myRecordScore = 0 recordMaxCombo = 0"),
            "The surprise popup reports real records instead of the arcade's 0 / 0 / 0.", failures);

        // --- Surprise slot: blank right panel, one "?" bar ------------------
        Check(flat.Contains("local recordsVisible = selectSongPopup.isSurprise != true " +
            "selectSongPopup.content.Q(\"record-labels\").display = recordsVisible " +
            "selectSongPopup.content.Q(\"best-record\").display = recordsVisible " +
            "selectSongPopup.content.Q(\"my-record\").display = recordsVisible " +
            "selectSongPopup.content.Q(\"max-combo-record\").display = recordsVisible"),
            "The surprise popup still draws popup_info.vce's BEST / MY / MAX COMBO block " +
            "instead of leaving the panel black.", failures);
        Check(selectSong.Contains("LayoutSurpriseButton = function(patternName)") &&
            flat.Contains("button.display = isSelected " +
                "button.Q(\"question\").display = isSelected " +
                "button.Q(\"text\").visible = not isSelected"),
            "The surprise popup does not collapse its difficulty stack to a single \"?\" bar.",
            failures);
        Check(flat.Contains("button.style.top = StyleLength(137) " +
            "button.Q(\"tens\").visible = false " +
            "button.Q(\"ones\").visible = false"),
            "The surprise popup's one bar does not sit in the bottom slot without level digits.",
            failures);
        Check(flat.Contains("if (selectSongPopup.isSurprise == true) then " +
            "selectSongPopup.LayoutSurpriseButton(patternName) end"),
            "Locking a difficulty does not re-park the surprise popup's single bar, so its " +
            "colour and the visible slot can disagree.", failures);
        Check(selectSong.Contains("NextSurpriseDifficulty = function()") &&
            flat.Contains("local nextDifficulty = selectSongPopup.NextSurpriseDifficulty()"),
            "Touching the surprise popup's \"?\" bar does not cycle through NM..EX.", failures);
        Check(flat.Contains("button.Q(\"question\").display = false " +
            "button.Q(\"text\").visible = true"),
            "An ordinary popup opened after a surprise one keeps the \"?\" in place of its " +
            "difficulty caption.", failures);
        Check(Regex.Matches(tree, "name=\"question\"").Count >= 4,
            "The four difficulty bars do not each carry a \"?\" label socket.", failures);

        // --- Entry lands on the songs grid, never the random page -----------
        Check(flat.Contains("if (pageToTurnTo == trackList.randomPageIndex) then " +
            "pageToTurnTo = 1 positionToPopup = 1 end"),
            "Re-entering Song Select can open on the random page instead of the first songs page.",
            failures);
        Check(flat.Contains("if (selectSongScreen.pageMemoryCredit != pageMemoryCredit) then " +
            "selectSongScreen.pageMemoryCredit = pageMemoryCredit " +
            "selectSongScreen.currentPage = -1 end"),
            "A fresh credit inherits the previous credit's page instead of opening on page 1.",
            failures);

        // --- Page bar: a genre logo owns the capsule's left end -------------
        // The RESPECT V capsule the user supplied measures, on this theme's
        // 822px pill (x 229..1050): logo ink from x 263, the dot run's first
        // sphere from x 493, and 48px of pitch between spheres.
        Check(tree.Contains("name=\"page-logo\"") &&
            tree.Contains("left: 263px; top: 693px; width: 170px; height: 40px"),
            "The page bar has no 170x40 logo socket parked at the capsule's left end.",
            failures);
        Check(Regex.IsMatch(tree, "name=\"page-logo\"[\\s\\S]{0,400}?name=\"letter\"") &&
            Regex.IsMatch(tree, "name=\"letter\"[^>]*font-size: 34px"),
            "The logo socket carries no bigger-than-normal letter for folders that " +
            "ship no logo.", failures);
        Check(Regex.IsMatch(tree, "name=\"letter\"[^>]*-unity-text-align: middle-left"),
            "The fallback name is centred in the socket instead of starting at the " +
            "same left edge as a logo, so the mark jumps sideways between folders.",
            failures);
        Check(tree.Contains("name=\"page-dots\" picking-mode=\"Ignore\" style=\"position: " +
            "absolute; left: 489px; top: 692px; height: 40px; width: 561px; " +
            "flex-direction: row; justify-content: flex-start;"),
            "The dot run does not start where RESPECT V's does, to the right of the " +
            "logo socket.", failures);
        Check(Regex.IsMatch(Squash(style), "#select-song-screen \\.page-dot \\{[^}]*" +
            "width: 40px; height: 40px; /\\* [^*]*\\*/ margin-right: 8px;"),
            "The page dots do not carry the 8px of margin that makes RESPECT V's " +
            "48px pitch.", failures);
        Check(Regex.IsMatch(tree, "name=\"page-display\"[^>]*width: 84px") &&
            Regex.IsMatch(tree, "name=\"page-display\"[^>]*right: 6px"),
            "The page counter is wide enough to reach under the tenth dot.", failures);
        Check(Regex.Matches(tree, "name=\"page-dot\"").Count == 10,
            "The capsule does not carry the ten page dots the logo socket leaves room for.",
            failures);
        Check(selectSong.Contains("dotCount = 10,") &&
            selectSong.Contains("dotsPerScreen = 10,") &&
            !flat.Contains("CeilToInt(numPages / 15)"),
            "The dot table still pages fifteen dots at a time.", failures);

        // --- Page bar: the dots count the genre, not the library -------------
        Check(selectSong.Contains("PageRun = function(pageNumber)") &&
            flat.Contains("if (trackSort.GetConfigured(selectModeScreen.selectedMode) != " +
                "\"Genre folders\") then return 1, numPages end"),
            "Ten dots stand for the whole library, so a genre's own page count is lost.",
            failures);
        Check(selectSong.Contains("CategoryKey = function(pageNumber)") &&
            flat.Contains("return (t3Track.categoryNumbered == true and \"n:\" or \"u:\") " +
                ".. tostring(t3Track.categoryOrder or \"\") .. \":\" .. " +
                "string.lower(tostring(t3Track.category or \"\"))"),
            "The dot run splits somewhere other than where BuildPages cut the pages.",
            failures);
        Check(flat.Contains("local firstPage = pageNumber while (firstPage > 1 and " +
            "selectSongPageDots.CategoryKey(firstPage - 1) == key) do " +
            "firstPage = firstPage - 1 end"),
            "The dots do not walk back to the first page of the current folder.", failures);
        Check(flat.Contains("local firstPage, lastPage = selectSongPageDots.PageRun(pageNumber) " +
            "local runLength = lastPage - firstPage + 1"),
            "A page turn does not re-scope the dots to the folder it landed in.", failures);
        Check(flat.Contains("selectSongPageDots.pageDisplay.text = " +
            "(pageNumber - firstPage + 1) .. \" / \" .. runLength") &&
            !flat.Contains("pageDisplay.text = pageNumber .. \" / \" .. numPages"),
            "The counter reports the library's page numbers instead of the folder's.",
            failures);
        Check(flat.Contains("local newPage = selectSongPageDots.windowFirstPage + dotNumber - 1 " +
            "if (newPage > selectSongPageDots.windowLastPage) then return end " +
            "selectSongScreen.TurnToPage(newPage)"),
            "Touching a dot jumps to a library page number rather than the one it draws.",
            failures);

        // --- Page bar: where the logo comes from ----------------------------
        Check(prepare.Contains("FindCategoryLogo = function(folder)") &&
            flatPrepare.Contains("for _, basename in ipairs({\"logo.png\", \"logo.jpg\"}) do " +
                "local path = tm.paths.Combine(folder, basename) " +
                "if (tm.io.FileExists(path)) then return path end end"),
            "A genre folder's own logo.png / logo.jpg is never looked for.", failures);
        Check(flatPrepare.Contains("childCategoryInfo.logoPath = " +
            "trackList.FindCategoryLogo(subfolder.fullPath)"),
            "Folders that do not name a category are searched for a logo, or the ones " +
            "that do are not.", failures);
        Check(prepare.Contains("categoryLogoPath = categoryInfo.logoPath,"),
            "The category's logo never reaches the tracks on its pages.", failures);

        // --- Page bar: resolving the socket per page ------------------------
        Check(selectSong.Contains("selectSongPageLogo = {") &&
            flat.Contains("selectSongPageDots.ShowPage(newPage) " +
                "selectSongPageLogo.ShowPage(newPage)"),
            "A page turn does not re-resolve the logo socket.", failures);
        Check(flat.Contains("if (pageNumber == trackList.randomPageIndex) then " +
            "selectSongPageLogo.element.display = false return end"),
            "The random page keeps the previous genre's logo on screen.", failures);
        Check(flat.Contains("if (trackSort.GetConfigured(selectModeScreen.selectedMode) != " +
            "\"Genre folders\") then return nil end"),
            "A logo can be shown under a sort where one page is not one folder.", failures);
        Check(flat.Contains("if (t3Track.categoryLogoPath == nil) then return nil end " +
            "if (logoPath != nil and t3Track.categoryLogoPath != logoPath) then return nil end"),
            "A page that mixes categories still borrows the first folder's logo.", failures);
        Check(selectSong.Contains("FirstLetter = function(value)") &&
            flat.Contains("if (lead >= 240) then length = 4 elseif (lead >= 224) then " +
                "length = 3 elseif (lead >= 192) then length = 2 end"),
            "The letter fallback cuts a UTF-8 folder name mid-codepoint.", failures);
        Check(selectSong.Contains("FolderName = function(value)") &&
            flat.Contains("elseif (criterion == \"Title\") then " +
                "return selectSongPageLogo.FirstLetter(t3Track.trackMetadata.title) end " +
                "return selectSongPageLogo.FolderName(t3Track.category)"),
            "A folder with no logo is labelled with one initial instead of its name.",
            failures);
        Check(selectSong.Contains("FitFontSize = function(text)") &&
            flat.Contains("unity.mathf.FloorToInt(selectSongPageLogo.labelWidth / ems)") &&
            flat.Contains("ems = ems + 0.65"),
            "A long folder name is not brought down to a size that fits the 170px socket.",
            failures);
        Check(flat.Contains("selectSongPageLogo.letterElement.style.fontSize = " +
            "StyleLength(size)"),
            "The fitted size is computed but never applied to the label.", failures);
        Check(selectSong.Contains("labelNudgePerEm = 0.085,") &&
            flat.Contains("local nudge = unity.mathf.RoundToInt( " +
                "size * selectSongPageLogo.labelNudgePerEm) " +
                "selectSongPageLogo.letterElement.style.top = StyleLength(nudge) " +
                "selectSongPageLogo.letterElement.style.bottom = StyleLength(-nudge)"),
            "The fallback name keeps SDGothicB's high middle alignment, so it sits " +
            "above the dots' centre line instead of on it.", failures);
        Check(flat.Contains("local stale = generation != selectSongPageLogo.generation"),
            "A logo that loads after the player has turned the page can still land.",
            failures);
        Check(flat.Contains("selectSongPageLogo.texture = texture " +
            "selectSongPageLogo.element.backgroundImage = texture"),
            "The loaded logo is never shown.", failures);
        Check(flat.Contains("selectSongPageLogo.letterElement.display = (logoPath == nil)"),
            "The folder name is shown while its logo is still loading, so it blinks " +
            "through the socket on every page turn.", failures);
        Check(flat.Contains("if (logoPath != nil and logoPath == " +
            "selectSongPageLogo.loadedPath and selectSongPageLogo.texture != nil) then " +
            "selectSongPageLogo.letterElement.display = false return end"),
            "Paging inside one folder reloads the logo it is already showing.", failures);
        Check(flat.Contains("Release = function() selectSongPageLogo.loadedPath = nil"),
            "Dropping the texture leaves the loaded path set, so the next page turn " +
            "keeps a logo that is no longer there.", failures);
        Check(flat.Contains("if (not stale) then selectSongPageLogo.loadedPath = nil " +
            "selectSongPageLogo.letterElement.display = true end"),
            "A logo that fails to load leaves the socket empty instead of falling back " +
            "to the folder name.", failures);
        Check(selectSong.Contains("selectSongPageLogo.letterElement.display = false"),
            "The letter stays behind the logo that replaced it.", failures);
        Check(flat.Contains("selectSongScreen.ReleaseEyecatches(" +
            "selectSongScreen.page2EyecatchTextures) selectSongPageLogo.Release()"),
            "Leaving Song Select leaks the loaded genre logo.", failures);
        Check(selectSong.Contains("selectSongPageLogo.Initialize()"),
            "The logo socket is never bound to its elements.", failures);

        // --- Page bar: the -, + and ? buttons outboard of the capsule --------
        // songselect_navi.vce parks ? left of the 530px capsule and the +/-
        // socket right of it, both as 50x50 quads on the capsule's own centre
        // line, with ~43px of clear space between the circles' ink and the
        // capsule's. Carried onto this theme's 823px pill (ink x 229..1051,
        // centre y 712.5): - ink ends at 186, + ink starts at 1095, and ? takes
        // the same gap again to the left of -.
        Check(Squash(tree).Contains("name=\"page-display\"") &&
            Regex.IsMatch(Squash(tree), "name=\"page-display\"[^>]*/> </ui:VisualElement> " +
                "<ui:VisualElement name=\"page-random\""),
            "The three navi buttons are not siblings declared straight after the " +
            "page-dots container, so either they paint under page_bg or " +
            "ShowPage's display = false takes them away with the dot strip on " +
            "the random page.", failures);
        foreach (var socket in new[] {
            new[] { "page-random", "58px" },
            new[] { "page-prev-folder", "141px" },
            new[] { "page-next-folder", "1090px" } })
        {
            Check(tree.Contains("<ui:VisualElement name=\"" + socket[0] +
                "\" class=\"page-navi-button\" style=\"left: " + socket[1] + ";\"/>"),
                socket[0] + " is not parked at left " + socket[1] +
                " as a pickable page-navi-button.", failures);
            Check(!Regex.IsMatch(tree, "name=\"" + socket[0] +
                "\"[^>]*picking-mode=\"Ignore\""),
                socket[0] + " ignores picking, so its click never arrives.", failures);
        }
        Check(Regex.IsMatch(Squash(style), @"#select-song-screen \.page-navi-button \{ " +
            @"position: absolute; top: 687\.5px; width: 50px; height: 50px; \}"),
            "The navi buttons do not draw songselect_navi1.png's 50x50 cell centred " +
            "on the capsule's centre line, so their 40px ink no longer matches a " +
            "page dot.", failures);
        foreach (string sprite in new[] { "page_prev", "page_next", "page_random",
            "page_random_active" })
        {
            string png = Path.Combine(ui, "Sprites", "Select Song", sprite + ".png");
            Check(File.Exists(png) && PngSize(png) == "50x50",
                "Assets/UI/Sprites/Select Song/" + sprite + ".png is missing or is " +
                "not the 50x50 cell cut out of songselect_navi1.png.", failures);
            Check(File.Exists(png + ".meta"),
                sprite + ".png carries no .meta, so its import settings are not " +
                "pinned to the ones the other page sprites use.", failures);
        }
        Check(selectSong.Contains("selectSongPageNavi = {") &&
            selectSong.Contains("selectSongPageNavi.Initialize()"),
            "The navi buttons are never bound to their elements.", failures);
        Check(flat.Contains("selectSongPageLogo.ShowPage(newPage) " +
            "selectSongPageNavi.ShowPage(newPage)"),
            "A page turn does not repaint the navi buttons, so - and + can stay " +
            "lit with nowhere left to go.", failures);
        Check(flat.Contains("function() selectSongPageNavi.TurnToFolder(-1) end") &&
            flat.Contains("function() selectSongPageNavi.TurnToFolder(1) end"),
            "- and + are not wired to a folder step.", failures);
        Check(flat.Contains("function() selectSongScreen.TurnToPage(" +
            "trackList.randomPageIndex) end)"),
            "? does not turn to the random card's own page.", failures);
        Check(flat.Contains("local firstPage, lastPage = " +
            "selectSongPageDots.PageRun(pageNumber)") &&
            flat.Contains("return (selectSongPageDots.PageRun(firstPage - 1))"),
            "- steps by something other than the folder runs the dots are cut on, " +
            "or lands on the previous folder's last page instead of its first.",
            failures);
        Check(flat.Contains("if (lastPage >= numPages) then return nil end") &&
            flat.Contains("return lastPage + 1"),
            "+ can walk off the end of the list.", failures);
        Check(flat.Contains("if (pageNumber == trackList.randomPageIndex) then " +
            "if (direction < 0) then return nil end return 1 end"),
            "The random page has no folder before it, so - must be dead there and " +
            "+ must land on the first songs page.", failures);
        Check(selectSong.Contains("dimmedOpacity = 0.35,") &&
            flat.Contains("selectSongPageNavi.SetEnabled(selectSongPageNavi.prevButton, " +
                "selectSongPageNavi.FolderTarget(-1, pageNumber) != nil)"),
            "A navi button with nowhere to go is not dimmed, so it silently eats " +
            "the touch.", failures);

        // --- Resource ownership --------------------------------------------
        Check(selectSong.Contains("previewSoundIsThemeAudio"),
            "Theme-loaded preview audio is not distinguished from file-loaded audio.", failures);
        Check(flat.Contains("if (selectSongPopup.previewSoundIsThemeAudio != true) then " +
            "tm.io.ReleaseAudio(selectSongPopup.previewSound) end"),
            "Hide() would release theme audio, which the runtime owns and caches.", failures);
        Check(selectSong.Contains("Hide() drops the loaded preview but keeps the track pointer"),
            "A page turn leaves the popup's track pointer set, so the next popup can open without a preview.",
            failures);

        // --- Assets --------------------------------------------------------
        Check(File.Exists(Path.Combine(ui, "Videos", "question.mp4")),
            "Assets/UI/Videos/question.mp4 (converted from resource/songselect/question.bik) is missing.",
            failures);
        Check(File.Exists(Path.Combine(ui, "Music", "question.ogg")),
            "Assets/UI/Music/question.ogg (copied from resource/songselect/question.ogg) is missing.",
            failures);
        return failures;
    }

    private static string Read(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    // IHDR, so a sprite can be size-checked without importing it.
    private static string PngSize(string path)
    {
        byte[] header = new byte[24];
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Read(header, 0, header.Length) < header.Length) return "";
        }
        int width = (header[16] << 24) | (header[17] << 16) |
            (header[18] << 8) | header[19];
        int height = (header[20] << 24) | (header[21] << 16) |
            (header[22] << 8) | header[23];
        return width + "x" + height;
    }

    // Collapse indentation so multi-line Lua tables can be asserted compactly.
    private static string Squash(string text)
    {
        return Regex.Replace(text, @"\s+", " ");
    }

    private static void Check(bool condition, string message,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
