using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using UnityEngine;

public static class T2TrackSortingContract
{
    private const string ScriptPath = "Assets/UI/Scripts/Track Sorting.txt";

    public static void Run()
    {
        var failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                failures));
        Debug.Log("[T2 Test] Track-folder sorting contracts passed.");
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string path = Path.Combine(Directory.GetCurrentDirectory(), ScriptPath);
        if (!File.Exists(path))
        {
            failures.Add("The reusable Track Sorting theme module is missing.");
            return failures;
        }

        try
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(File.ReadAllText(path), null, "Track Sorting.txt");

            DynValue categories = script.DoString(@"
local preview = trackSort.ParseCategory('(00) Previews')
local vocaloid = trackSort.ParseCategory('(01) VOCALOID')
local plain = trackSort.ParseCategory('Anime')
local malformed = trackSort.ParseCategory('(A1) Bad')
local wrongPreview = trackSort.ParseCategory('(01) Previews')
return preview.category, preview.categoryOrder, preview.categoryNumbered, preview.isPreview,
    vocaloid.category, vocaloid.categoryOrder, vocaloid.categoryNumbered, vocaloid.isPreview,
    plain.category, plain.categoryOrder == nil, plain.categoryNumbered, plain.isPreview,
    malformed.category, malformed.categoryNumbered, wrongPreview.isPreview
");
            Check(categories.Tuple[0].String == "Previews" &&
                  categories.Tuple[1].Number == 0 &&
                  categories.Tuple[2].Boolean && categories.Tuple[3].Boolean,
                "The reserved (00) Previews folder was not parsed correctly.", failures);
            Check(categories.Tuple[4].String == "VOCALOID" &&
                  categories.Tuple[5].Number == 1 &&
                  categories.Tuple[6].Boolean && !categories.Tuple[7].Boolean,
                "A numbered genre folder was not normalized correctly.", failures);
            Check(categories.Tuple[8].String == "Anime" &&
                  categories.Tuple[9].Boolean && !categories.Tuple[10].Boolean &&
                  !categories.Tuple[11].Boolean,
                "An unnumbered genre folder was not preserved.", failures);
            Check(categories.Tuple[12].String == "(A1) Bad" &&
                  !categories.Tuple[13].Boolean && !categories.Tuple[14].Boolean,
                "Malformed prefixes or non-zero Previews were treated as reserved.", failures);

            DynValue folders = script.DoString(@"
local tracks = {
    { category = 'Order Ten', categoryOrder = 10, categoryNumbered = true, trackMetadata = { title = 'Ten', artist = 'B' } },
    { category = 'Order Two', categoryOrder = 2, categoryNumbered = true, trackMetadata = { title = 'Delta', artist = 'D' } },
    { category = 'VOCALOID', categoryNumbered = false, trackMetadata = { title = 'Vocal', artist = 'V' } },
    { category = 'Order Two', categoryOrder = 2, categoryNumbered = true, trackMetadata = { title = 'Alpha', artist = 'A' } },
    { category = 'Anime', categoryNumbered = false, trackMetadata = { title = 'Anime', artist = 'C' } },
    { category = 'Order Two', categoryOrder = 2, categoryNumbered = true, trackMetadata = { title = 'Charlie', artist = 'C' } },
    { category = 'Order Two', categoryOrder = 2, categoryNumbered = true, trackMetadata = { title = 'Beta', artist = 'B' } }
}
local pages = trackSort.BuildPages(tracks, 'Genre folders', 9)
return #pages,
    #pages[1], pages[1][1].category, pages[1][1].trackMetadata.title,
    pages[2][1].category, pages[3][1].category, pages[4][1].category
");
            Check(folders.Tuple[0].Number == 4 && folders.Tuple[1].Number == 4,
                "Genre sorting did not preserve category page boundaries.", failures);
            Check(folders.Tuple[2].String == "Order Two" &&
                  folders.Tuple[4].String == "Order Ten" &&
                  folders.Tuple[5].String == "Anime" &&
                  folders.Tuple[6].String == "VOCALOID",
                "Numbered and unnumbered genre folders are in the wrong order.", failures);
            Check(folders.Tuple[3].String == "Alpha",
                "Genre sorting is not stable by title inside each folder.", failures);

            DynValue orders = script.DoString(@"
local tracks = {
    { category = 'X', trackMetadata = { title = 'Zulu', artist = 'Able' }, sortBpm = 180, sortSongLength = 90, sortDifficulty = 4 },
    { category = 'X', trackMetadata = { title = 'Alpha', artist = 'Zulu' }, sortBpm = 120, sortSongLength = 210, sortDifficulty = 9 },
    { category = 'X', trackMetadata = { title = 'Mike', artist = 'Mike' }, sortBpm = 150, sortSongLength = 150, sortDifficulty = 6 }
}
local title = trackSort.BuildPages(tracks, 'Title', 9)
local artist = trackSort.BuildPages(tracks, 'Artist', 9)
local bpm = trackSort.BuildPages(tracks, 'BPM', 9)
local length = trackSort.BuildPages(tracks, 'Song Length', 9)
local difficulty = trackSort.BuildPages(tracks, 'Difficulty', 9)
return title[1][1].trackMetadata.title,
    artist[1][1].trackMetadata.title,
    bpm[1][1].trackMetadata.title,
    length[1][1].trackMetadata.title,
    difficulty[1][1].trackMetadata.title
");
            Check(orders.Tuple[0].String == "Alpha" &&
                  orders.Tuple[1].String == "Zulu" &&
                  orders.Tuple[2].String == "Alpha" &&
                  orders.Tuple[3].String == "Zulu" &&
                  orders.Tuple[4].String == "Zulu",
                "One or more per-mode ordering criteria return the wrong first track.",
                failures);

            DynValue previews = script.DoString(@"
local pools = {
    star = {
        { trackMetadata = { title = 'Star Preview' }, t3Patterns = { NM = { patternMetadata = { guid = 'star-nm' } } } }
    },
    pop = {
        { trackMetadata = { title = 'Pop Preview' }, t3Patterns = { NM = { patternMetadata = { guid = 'pop-nm' } } } }
    }
}
local mode1, track1, pattern1, cursor1 = trackSort.NextPreview(pools, 0)
local mode2, track2, pattern2, cursor2 = trackSort.NextPreview(pools, cursor1)
local mode3, track3, pattern3, cursor3 = trackSort.NextPreview(pools, cursor2)
local emptyMode, emptyTrack, emptyPattern, emptyCursor = trackSort.NextPreview({ star = {}, pop = {} }, 5)
return trackSort.IsPreviewCategory('(00) PREVIEWS'),
    mode1, track1.trackMetadata.title, pattern1.patternMetadata.guid,
    mode2, track2.trackMetadata.title, pattern2.patternMetadata.guid,
    mode3, track3.trackMetadata.title, pattern3.patternMetadata.guid,
    emptyMode == nil, emptyTrack == nil, emptyPattern == nil, emptyCursor
");
            Check(previews.Tuple[0].Boolean,
                "The reserved (00) Previews folder match is not case-insensitive.", failures);
            Check(previews.Tuple[1].String == "star" &&
                  previews.Tuple[2].String == "Star Preview" &&
                  previews.Tuple[3].String == "star-nm" &&
                  previews.Tuple[4].String == "pop" &&
                  previews.Tuple[5].String == "Pop Preview" &&
                  previews.Tuple[6].String == "pop-nm" &&
                  previews.Tuple[7].String == "star",
                "Reserved preview tracks do not rotate deterministically by mode.", failures);
            Check(previews.Tuple[10].Boolean && previews.Tuple[11].Boolean &&
                  previews.Tuple[12].Boolean && previews.Tuple[13].Number == 5,
                "An absent Previews folder does not return a safe no-op result.", failures);
        }
        catch (Exception exception)
        {
            failures.Add("Track sorting behavior is missing or invalid: " +
                exception.GetBaseException().Message);
        }
        return failures;
    }

    private static void Check(bool condition, string message,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
