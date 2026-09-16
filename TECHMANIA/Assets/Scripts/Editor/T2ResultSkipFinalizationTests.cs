using System;
using System.IO;

public static class T2ResultSkipFinalizationTests
{
    public static void Run()
    {
        RunFromProject(Directory.GetCurrentDirectory());
    }

    public static void RunFromProject(string projectPath)
    {
        string source = File.ReadAllText(Path.Combine(projectPath,
                "Assets", "UI", "Scripts", "Result Screen.txt"))
            .Replace("\r\n", "\n");

        Require(source.Contains("animationFinalized = false,"),
            "Result animation lifecycle has no monotonic finalized latch.");
        Require(source.Contains("IsAnimationRunActive = function(runId)"),
            "Late result callbacks have no shared run-validity guard.");
        Require(source.Contains("FinalizeAnimations = function(runId)"),
            "Result touch skip is not consolidated into one finalizer.");
        Require(source.Contains(
                "if (resultScreen.animationFinalized or\n" +
                "            resultScreen.showRunId != runId) then return false end\n" +
                "        resultScreen.animationFinalized = true"),
            "The finalizer must latch before stopping coroutines or painting final state.");

        Require(Count(source,
                "animation.SkipToEnd(resultScreen.mainAnimation)") == 1,
            "Main result finalization must have exactly one authored end-state path.");
        Require(source.Contains(
                "return resultScreen.FinalizeAnimations(resultScreen.showRunId)"),
            "Touch skip does not use the shared idempotent finalizer.");
        Require(source.Contains(
                "if (resultScreen.animationFinalized or resultScreen.skipRequested) then"),
            "The delayed video-load path can restart a result after an early skip.");

        string[] stoppedCoroutines =
        {
            "mainAnimationCoroutine",
            "rankOrStarPointAnimationCoroutine",
            "rankFrameCoroutine",
            "effectorEnterCoroutine",
            "sfxCoroutine",
            "addBonusCoroutine"
        };
        foreach (string field in stoppedCoroutines)
        {
            Require(source.Contains(
                    "resultScreen.StopCoroutineField(\"" + field + "\")"),
                "Finalization does not stop " + field + ".");
        }

        int showStart = source.IndexOf("    Show = function()", StringComparison.Ordinal);
        int loadedStart = source.IndexOf(
            "        local function StartLoadedResult", showStart,
            StringComparison.Ordinal);
        Require(showStart >= 0 && loadedStart > showStart,
            "Could not locate the Result Show lifecycle.");
        string showSetup = source.Substring(showStart, loadedStart - showStart);
        Require(showSetup.Contains(
                "animation.ResetToBeginning(resultScreen.mainAnimation)"),
            "A new result run does not reset the main animation to its first keyframes.");
        Require(showSetup.Contains(
                "animation.ResetToBeginning(resultScreen.rankOrStarPointAnimationToPlay)"),
            "A new result run does not reset rank/star artwork to its first keyframes.");
        // Game Screen hides itself before calling Show(), so the result root
        // has to appear before the videos are requested -- otherwise the
        // several seconds of movie loading show nothing, and because a hidden
        // root receives no clicks, a skip during that window was dropped.
        // Arcade All Clear V2 Screen displays its root before LoadMedia too.
        Require(showSetup.Contains("resultScreen.screen.display = true"),
            "The result root is not shown before its videos are requested, so " +
            "a click during the load cannot reach the skip handler.");
        Require(Count(source, "resultScreen.screen.display = true") == 1,
            "The result root must be revealed from exactly one site in Show().");
        Require(showSetup.Contains(
                "resultScreen.screen.Q(\"content\").display = false"),
            "Showing the root early must still keep the result content hidden " +
            "until the animation starts or the finalizer reveals it.");
        Require(source.Contains(
                "resultScreen.screen.RegisterCallback(eventType.Click, function()\n" +
                "            if (resultScreen.SkipAnimations()) then return end"),
            "The skip handler is not registered on the result root itself.");

        Require(source.Contains(
                "SetRect(trackInfo.Q(\"title\"), 437, 235.5, 448, 35)"),
            "Song title is not centered on Pop VCE anchor (661,253).");
        Require(source.Contains(
                "SetRect(trackInfo.Q(\"genre\"), 928, 233.5, 256, 35)"),
            "Genre is not centered on Pop VCE anchor (1056,251).");
        Require(source.Contains(
                "SetRect(trackInfo.Q(\"artist\"), 928, 255.5, 256, 35)"),
            "Artist is not centered on Pop VCE anchor (1056,273).");
        Require(source.Contains(
                "SetRect(breakdown.Q(\"new-record\"), 1058, 452, 128, 16)"),
            "NEW RECORD banner does not use its Pop VCE socket.");

        Console.WriteLine(
            "[T2 Test] Result skip finalization and VCE anchors passed.");
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
