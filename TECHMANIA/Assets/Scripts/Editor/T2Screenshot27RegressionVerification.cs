using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

public static class T2Screenshot27RegressionVerification
{
    [MenuItem("TECHMANIA/Tests/Verify Screenshot 2-7 Regressions")]
    public static void Run()
    {
        List<string> failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                failures));
        UnityEngine.Debug.Log("[T2 Test] Screenshot 2-7 regression contracts passed.");
    }

    public static List<string> Evaluate()
    {
        string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        string ui = Path.Combine(project, "Assets", "UI");
        string scripts = Path.Combine(ui, "Scripts");
        string result = Read(Path.Combine(scripts, "Result Screen.txt"));
        string allClear = Read(Path.Combine(scripts, "Arcade All Clear V2 Screen.txt"));
        string prepare = Read(Path.Combine(scripts, "Prepare Screen.txt"));
        string selectSong = Read(Path.Combine(scripts, "Select Song Screen.txt"));
        string ending = Read(Path.Combine(scripts, "Arcade Ending Screen.txt"));
        string warning = Read(Path.Combine(scripts, "Warning Screen.txt"));
        string utility = Read(Path.Combine(scripts, "Utility.txt"));
        string tree = Read(Path.Combine(ui, "MainTree.uxml"));
        string style = Read(Path.Combine(ui, "MainStyle.uss"));
        string options = Read(Path.Combine(project, "Assets", "Scripts",
            "Serializable", "Options.cs"));
        string playerOptions = Read(Path.Combine(project, "Assets", "Scripts",
            "Serializable", "PlayerOptions.cs"));
        string layout = Read(Path.Combine(project, "Assets", "Scripts",
            "Components", "Main Scene", "Game", "GameLayout.cs"));
        string noteManager = Read(Path.Combine(project, "Assets", "Scripts",
            "Components", "Main Scene", "Game", "NoteManager.cs"));

        var failures = new List<string>();
        Check(allClear.Contains("eyecatchImage") &&
            allClear.Contains("discBorderPath") &&
            allClear.Contains("source = \"eyecatch\""),
            "All Clear must use full eyecatch fallback plus authored difficulty border.", failures);
        Check(tree.Contains("name=\"border\" picking-mode=\"Ignore\" class=\"allclear-v2-disc-border\""),
            "All Clear stage discs need a separate authored border socket.", failures);
        Check(style.Contains("#result-screen .effector-button #selection-glow") &&
            style.Contains("left: -4px;") && style.Contains("top: -4px;") &&
            style.Contains("#result-screen .effector-button #highlight"),
            "Result effector glow layers are not centered on the 56px icon socket.", failures);
        Check(result.Contains("DisplayNumber(0") &&
            result.Contains("ForceFailedStarZero") &&
            result.Contains("SetStarPointFinalState"),
            "Star result finalization does not explicitly retain visible numeric values.", failures);
        Check(options.Contains("enum NoteSize") && options.Contains("noteSize") &&
            playerOptions.Contains("noteSize") && layout.Contains("NoteSize"),
            "Note Size is not persisted and applied by gameplay layout.", failures);
        Check(tree.Contains("name=\"note-size\"") &&
            selectSong.Contains("InitializeNoteSizeDropdown"),
            "Options UI has no Big/Normal/Small Note Size control.", failures);
        string noteManagerContract = noteManager.ToLowerInvariant();
        Check(noteManagerContract.Contains("spawn note elements in reverse order") &&
            noteManagerContract.Contains("pop") &&
            noteManagerContract.Contains("overlap"),
            "Pop Mixing does not declare and preserve overlapped-note draw ordering.", failures);
        Check(tree.Contains("question.png") &&
            selectSong.Contains("random") &&
            selectSong.Contains("RandomSong"),
            "Star/Pop Song Select has no question-card random-song route.", failures);
        // The question card no longer displaces a track: it owns page 0 behind
        // the first songs page, so every songs page keeps its nine tracks.
        Check(!prepare.Contains("carry = table.remove(page, 10)") &&
            prepare.Contains("randomPageIndex = 0") &&
            prepare.Contains("GetPageContents = function(pageIndex)"),
            "The random card still paginates through the songs pages.", failures);
        Check(utility.Contains("PlayWaterdrop") &&
            ending.Contains("PlayTransitionWaterdrop"),
            "The ending screen does not trigger the waterdrop transition.", failures);
        // Warning's ripple is a touch response only. Playing it from Show drew
        // an unprompted wave in the middle of the card the instant the screen
        // appeared, with nobody touching it; warning-touch-trails still answers
        // real taps.
        Check(!warning.Contains("PlayTransitionWaterdrop") &&
            warning.Contains("touchWaveInput.Register(warningScreen.screen"),
            "The warning screen plays an unprompted entry ripple, or lost its tap ripple.", failures);
        Check(ending.Contains("see you next time") &&
            ending.Contains("mode == \"star\""),
            "Star ending has no mode-specific See You Next Time presentation.", failures);
        Check(Path.Combine(ui, "Sprites", "Select Song", "question.png") is string questionPath &&
            File.Exists(questionPath),
            "The authored question.png random-song asset is missing.", failures);
        return failures;
    }

    private static string Read(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private static void Check(bool condition, string message,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
