using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class HumanPlaytesterSettings
{
    public const string ThemeName = "TECHNIKA 2";
    public const string OptionKey = "HumanPlaytester";

    public static bool BuildHasFeature
    {
        get
        {
#if TECHMANIA_HUMAN_PLAYTESTER
            return true;
#else
            return false;
#endif
        }
    }

    public static bool ParseEnabled(string value)
    {
        bool enabled;
        return bool.TryParse(value, out enabled) && enabled;
    }

    public static bool IsEnabled()
    {
        if (!BuildHasFeature || Options.instance == null) return false;
        Dictionary<string, string> options =
            Options.instance.GetThemeOptions(ThemeName);
        string value;
        return options.TryGetValue(OptionKey, out value) &&
            ParseEnabled(value);
    }

    public static bool ScoreModifiersAreValid(Modifiers modifiers,
        bool humanPlaytesterActive)
    {
        if (modifiers == null) return false;
        if (!humanPlaytesterActive)
            return !modifiers.HasAnySpecialModifier();

        // The scheduler supplies its own input, so a saved keyboard/touch
        // override cannot affect the run. Gameplay-altering test modes and
        // scroll-speed modifiers must still invalidate the score.
        return modifiers.mode != Modifiers.Mode.AutoPlay &&
            modifiers.mode != Modifiers.Mode.Practice &&
            modifiers.scrollSpeed == Modifiers.ScrollSpeed.Normal;
    }
}

public sealed class HumanPlayRunContext
{
    public string songGuid { get; private set; }
    public string songTitle { get; private set; }
    public string patternGuid { get; private set; }
    public string patternName { get; private set; }
    public string fingerprint { get; private set; }
    public string ruleset { get; private set; }

    public string diagnosticIdentity
    {
        get
        {
            return $"songGuid={Clean(songGuid)} title={Clean(songTitle)} " +
                $"patternGuid={Clean(patternGuid)} pattern={Clean(patternName)} " +
                $"fingerprint={Clean(fingerprint)} ruleset={Clean(ruleset)}";
        }
    }

    public HumanPlayRunContext(string songGuid, string songTitle,
        string patternGuid, string patternName, string fingerprint,
        string ruleset)
    {
        this.songGuid = songGuid;
        this.songTitle = songTitle;
        this.patternGuid = patternGuid;
        this.patternName = patternName;
        this.fingerprint = fingerprint;
        this.ruleset = ruleset;
    }

    private static string Clean(string value)
    {
        return string.IsNullOrEmpty(value) ? "<unknown>" :
            value.Replace('\r', ' ').Replace('\n', ' ');
    }
}

public sealed class HumanPlayScheduler
{
    private readonly HumanPlayPlan plan;
    private readonly ChartDifficultyAnalysis analysis;
    private readonly HumanPlayRunContext context;
    private bool resultLogged;

    public HumanPlayScheduler(HumanPlayPlan plan,
        ChartDifficultyAnalysis analysis, HumanPlayRunContext context)
    {
        this.plan = plan;
        this.analysis = analysis;
        this.context = context;
    }

    public static HumanPlayScheduler Create(Pattern pattern,
        HumanPlayRunContext context)
    {
        ChartDifficultyAnalysis analysis =
            ChartDifficultyAnalyzer.Analyze(pattern);
        int seed = unchecked(Environment.TickCount * 397 ^
            Guid.NewGuid().GetHashCode());
        HumanPlayPlan plan = HumanPlayProfile.BuildPlan(pattern, analysis, seed);
        HumanPlayScheduler scheduler = new HumanPlayScheduler(
            plan, analysis, context);
        scheduler.LogPlan();
        return scheduler;
    }

    public HumanPlayAction ActionFor(Note note)
    {
        return plan.ActionFor(note);
    }

    public bool ShouldStart(HumanPlayAction action, float gameTime)
    {
        return action != null && action.kind != HumanPlayActionKind.Skip &&
            gameTime >= action.note.time + action.timingOffset;
    }

    public bool ShouldHold(HumanPlayAction action, float gameTime)
    {
        return action != null && action.kind != HumanPlayActionKind.Skip &&
            (action.note is HoldNote || action.note is DragNote) &&
            gameTime < action.longNoteReleaseTime;
    }

    public void LogResult(ScoreKeeper scoreKeeper)
    {
        if (resultLogged || scoreKeeper == null) return;
        resultLogged = true;
        Debug.Log($"[HumanPlaytester:Result] {context.diagnosticIdentity} " +
            $"seed={plan.seed} score={scoreKeeper.TotalScore()} " +
            $"maxCombo={scoreKeeper.maxCombo} " +
            $"rainbow={scoreKeeper.NumNotesWithJudgement(Judgement.RainbowMax)} " +
            $"max={scoreKeeper.NumNotesWithJudgement(Judgement.Max)} " +
            $"cool={scoreKeeper.NumNotesWithJudgement(Judgement.Cool)} " +
            $"good={scoreKeeper.NumNotesWithJudgement(Judgement.Good)} " +
            $"miss={scoreKeeper.NumNotesWithJudgement(Judgement.Miss)} " +
            $"break={scoreKeeper.NumNotesWithJudgement(Judgement.Break)}");
    }

    private void LogPlan()
    {
        string causes = analysis.chokes.Count == 0 ? "none" :
            string.Join(",", analysis.chokes
                .GroupBy(c => c.dominantCause)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}:{g.Count()}"));
        Debug.Log($"[HumanPlaytester:Plan] {context.diagnosticIdentity} " +
            $"seed={plan.seed} notes={plan.actions.Count} " +
            $"pressure={analysis.overallPressure:F3} " +
            $"chokes={analysis.chokes.Count} causes={causes} " +
            $"hit={plan.actions.Count(a => a.kind == HumanPlayActionKind.Hit)} " +
            $"drop={plan.actions.Count(a => a.kind == HumanPlayActionKind.DropLongNote)} " +
            $"skip={plan.actions.Count(a => a.kind == HumanPlayActionKind.Skip)}");
    }
}
