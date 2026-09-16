using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class HumanPlaytesterVerification
{
    public static void RunAll()
    {
        RunAnalyzerChecks();
        RunPlannerChecks();
        RunRuntimeChecks();
        RunThemeSourceChecks();
        RunBuildSourceChecks();
        Debug.Log("HumanPlaytesterVerification all checks passed.");
    }

    public static void RunAnalyzerChecks()
    {
        Type analyzerType = Type.GetType(
            "ChartDifficultyAnalyzer, Assembly-CSharp");
        Require(analyzerType != null,
            "ChartDifficultyAnalyzer must exist in the runtime assembly.");
        MethodInfo analyze = analyzerType.GetMethod("Analyze",
            BindingFlags.Public | BindingFlags.Static);
        Require(analyze != null,
            "ChartDifficultyAnalyzer.Analyze must exist.");

        Pattern sparseLevel1 = CreateSparsePattern(level: 1);
        Pattern sparseLevel99 = CreateSparsePattern(level: 99);
        Pattern dense = CreateDensePattern();

        object level1 = analyze.Invoke(null, new object[] { sparseLevel1 });
        object level99 = analyze.Invoke(null, new object[] { sparseLevel99 });
        object denseResult = analyze.Invoke(null, new object[] { dense });

        float level1Pressure = ReadFloat(level1, "overallPressure");
        float level99Pressure = ReadFloat(level99, "overallPressure");
        float densePressure = ReadFloat(denseResult, "overallPressure");
        Require(Mathf.Approximately(level1Pressure, level99Pressure),
            "Displayed level changed analyzer output.");
        Require(densePressure > level1Pressure + 0.15f,
            "Dense chart was not materially harder than sparse chart.");
        Require(ReadCount(denseResult, "chokes") > 0,
            "Dense chart produced no choke segment.");

        Debug.Log("HumanPlaytesterVerification analyzer checks passed.");
    }

    public static void RunPlannerChecks()
    {
        Type profileType = Type.GetType(
            "HumanPlayProfile, Assembly-CSharp");
        Require(profileType != null,
            "HumanPlayProfile must exist in the runtime assembly.");
        MethodInfo buildPlan = profileType.GetMethod("BuildPlan",
            BindingFlags.Public | BindingFlags.Static);
        Require(buildPlan != null, "HumanPlayProfile.BuildPlan must exist.");

        Pattern sparse = CreateSparsePattern(level: 42);
        Pattern dense = CreateDensePattern();
        object sparseAnalysis = Analyze(sparse);
        object denseAnalysis = Analyze(dense);
        object first = buildPlan.Invoke(null,
            new object[] { sparse, sparseAnalysis, 12345 });
        object same = buildPlan.Invoke(null,
            new object[] { sparse, sparseAnalysis, 12345 });
        object different = buildPlan.Invoke(null,
            new object[] { sparse, sparseAnalysis, 54321 });

        string firstSignature = PlanSignature(first);
        Require(firstSignature == PlanSignature(same),
            "Same seed did not produce the same human plan.");
        Require(firstSignature != PlanSignature(different),
            "Different seeds produced identical human plans.");
        Require(CountIntended(first, Judgement.RainbowMax) <
            ReadCount(first, "actions"),
            "Long plan was allowed to become fully perfect.");

        int sparseSevere = 0;
        int denseSevere = 0;
        for (int seed = 100; seed < 120; seed++)
        {
            object sparsePlan = buildPlan.Invoke(null,
                new object[] { sparse, sparseAnalysis, seed });
            object densePlan = buildPlan.Invoke(null,
                new object[] { dense, denseAnalysis, seed });
            sparseSevere += CountSevere(sparsePlan);
            denseSevere += CountSevere(densePlan);
            VerifyOffsetsMatchJudgements(sparsePlan);
            VerifyOffsetsMatchJudgements(densePlan);
        }
        Require(denseSevere > sparseSevere,
            "Dense charts did not produce more difficult actions.");
        Debug.Log("HumanPlaytesterVerification planner checks passed.");
    }

    public static void RunRuntimeChecks()
    {
        Type settingsType = Type.GetType(
            "HumanPlaytesterSettings, Assembly-CSharp");
        Require(settingsType != null,
            "HumanPlaytesterSettings must exist in the runtime assembly.");
        MethodInfo parse = settingsType.GetMethod("ParseEnabled",
            BindingFlags.Public | BindingFlags.Static);
        Require(parse != null, "HumanPlaytesterSettings.ParseEnabled must exist.");
        Require((bool)parse.Invoke(null, new object[] { "true" }),
            "Lowercase true did not enable the setting.");
        Require((bool)parse.Invoke(null, new object[] { "TRUE" }),
            "Uppercase true did not enable the setting.");
        Require(!(bool)parse.Invoke(null, new object[] { "yes" }),
            "Invalid boolean enabled the setting.");
        Require(!(bool)parse.Invoke(null, new object[] { null }),
            "Null enabled the setting.");

        Options machineOptions = new Options();
        machineOptions.GetThemeOptions(HumanPlaytesterSettings.ThemeName)
            [HumanPlaytesterSettings.OptionKey] = true.ToString();
        PlayerOptions profileWithoutOperatorSettings = new PlayerOptions();
        profileWithoutOperatorSettings.discordRichPresence = false;
        profileWithoutOperatorSettings.ApplyTo(machineOptions);
        string preservedValue;
        Require(machineOptions.GetThemeOptions(
                HumanPlaytesterSettings.ThemeName).TryGetValue(
                    HumanPlaytesterSettings.OptionKey, out preservedValue) &&
            HumanPlaytesterSettings.ParseEnabled(preservedValue),
            "Profile login erased the machine human playtester switch.");

        MethodInfo scoreModifiersAreValid = settingsType.GetMethod(
            "ScoreModifiersAreValid",
            BindingFlags.Public | BindingFlags.Static);
        Require(scoreModifiersAreValid != null,
            "HumanPlaytesterSettings.ScoreModifiersAreValid must exist.");
        Modifiers keyboardOverride = new Modifiers
        {
            mode = Modifiers.Mode.Normal,
            controlOverride = Modifiers.ControlOverride.OverrideToKeys,
            scrollSpeed = Modifiers.ScrollSpeed.Normal
        };
        Require((bool)scoreModifiersAreValid.Invoke(null,
            new object[] { keyboardOverride, true }),
            "Active human playtester did not permit its irrelevant input override.");
        Require(!(bool)scoreModifiersAreValid.Invoke(null,
            new object[] { keyboardOverride, false }),
            "Ordinary play incorrectly permitted a control override record.");
        Modifiers autoPlay = keyboardOverride.Clone();
        autoPlay.mode = Modifiers.Mode.AutoPlay;
        Require(!(bool)scoreModifiersAreValid.Invoke(null,
            new object[] { autoPlay, true }),
            "Human playtester incorrectly permitted AutoPlay records.");
        Modifiers shiftedScroll = keyboardOverride.Clone();
        shiftedScroll.scrollSpeed = Modifiers.ScrollSpeed.HalfSpeed;
        Require(!(bool)scoreModifiersAreValid.Invoke(null,
            new object[] { shiftedScroll, true }),
            "Human playtester incorrectly permitted altered-scroll records.");
        Require(typeof(GameInputManager).GetProperty(
            "humanPlaytesterActive") != null,
            "GameInputManager must expose humanPlaytesterActive.");
        Require(typeof(GameInputManager).GetMethod(
            "LogHumanPlaytesterResult") != null,
            "GameInputManager must expose result logging integration.");

        Type contextType = Type.GetType(
            "HumanPlayRunContext, Assembly-CSharp");
        Require(contextType != null,
            "HumanPlayRunContext must exist in the runtime assembly.");
        object context = Activator.CreateInstance(contextType,
            "song-guid", "Song Title", "pattern-guid", "Pattern Name",
            "fingerprint-123", "Standard");
        string identity = ReadProperty(context, "diagnosticIdentity").ToString();
        foreach (string expected in new[] { "song-guid", "Song Title",
            "pattern-guid", "Pattern Name", "fingerprint-123", "Standard" })
        {
            Require(identity.Contains(expected),
                $"Diagnostic identity omitted {expected}.");
        }

        Type schedulerType = Type.GetType(
            "HumanPlayScheduler, Assembly-CSharp");
        Require(schedulerType != null,
            "HumanPlayScheduler must exist in the runtime assembly.");
        Require(schedulerType.GetMethod("ShouldStart") != null,
            "HumanPlayScheduler.ShouldStart must exist.");
        Require(schedulerType.GetMethod("ShouldHold") != null,
            "HumanPlayScheduler.ShouldHold must exist.");

        Pattern runtimePattern = CreateDensePattern();
        object runtimeAnalysis = Analyze(runtimePattern);
        Type profileType = Type.GetType("HumanPlayProfile, Assembly-CSharp");
        object runtimePlan = profileType.GetMethod("BuildPlan",
            BindingFlags.Public | BindingFlags.Static).Invoke(null,
                new object[] { runtimePattern, runtimeAnalysis, 555 });
        object scheduler = Activator.CreateInstance(schedulerType,
            runtimePlan, runtimeAnalysis, context);
        MethodInfo shouldStart = schedulerType.GetMethod("ShouldStart");
        MethodInfo shouldHold = schedulerType.GetMethod("ShouldHold");
        object startAction = null;
        object longAction = null;
        foreach (object action in ReadEnumerable(runtimePlan, "actions"))
        {
            if (ReadProperty(action, "kind").ToString() != "Skip" &&
                startAction == null) startAction = action;
            Note note = (Note)ReadProperty(action, "note");
            if ((note is HoldNote || note is DragNote) &&
                ReadProperty(action, "kind").ToString() != "Skip")
                longAction = action;
        }
        Require(startAction != null, "Runtime plan had no playable hit action.");
        Note startNote = (Note)ReadProperty(startAction, "note");
        float targetTime = startNote.time + Convert.ToSingle(
            ReadProperty(startAction, "timingOffset"));
        Require(!(bool)shouldStart.Invoke(scheduler,
            new object[] { startAction, targetTime - 0.001f }),
            "Scheduler started an action before its planned time.");
        Require((bool)shouldStart.Invoke(scheduler,
            new object[] { startAction, targetTime }),
            "Scheduler did not start an action at its planned time.");
        Require(longAction != null, "Runtime plan had no playable long note.");
        float releaseTime = Convert.ToSingle(
            ReadProperty(longAction, "longNoteReleaseTime"));
        Require((bool)shouldHold.Invoke(scheduler,
            new object[] { longAction, releaseTime - 0.001f }),
            "Scheduler released a long note before its plan.");
        Require(!(bool)shouldHold.Invoke(scheduler,
            new object[] { longAction, releaseTime }),
            "Scheduler held a long note past its plan.");

        Modifiers normal = new Modifiers();
        normal.mode = Modifiers.Mode.Normal;
        Require(!normal.HasAnySpecialModifier(),
            "Normal modifier unexpectedly invalidates records.");
        Debug.Log("HumanPlaytesterVerification runtime checks passed.");
    }

    public static void RunThemeSourceChecks()
    {
        string uiRoot = Path.Combine(Application.dataPath, "UI");
        string tree = File.ReadAllText(Path.Combine(uiRoot, "MainTree.uxml"));
        string themeOptions = File.ReadAllText(Path.Combine(uiRoot,
            "Scripts", "Theme Options.txt"));
        string attractSettings = File.ReadAllText(Path.Combine(uiRoot,
            "Scripts", "Attract Settings Menu.txt"));
        Require(tree.Contains("name=\"attract-human-playtester\""),
            "Attract F2 UXML omitted the human playtester toggle.");
        Require(tree.Contains("name=\"human-playtester-tooltip\""),
            "Options UXML omitted the human playtester popup.");
        Require(themeOptions.Contains("HumanPlaytester") &&
            themeOptions.Contains("SetBool(\"HumanPlaytester\", false)"),
            "Theme options omitted the default-false tester key.");
        Require(attractSettings.Contains(
                "controls.Q(\"attract-human-playtester\")") &&
            attractSettings.Contains(
                "SetBool(\"HumanPlaytester\", event.newValue)"),
            "Attract F2 settings omitted tester toggle persistence.");
        Debug.Log("HumanPlaytesterVerification theme source checks passed.");
    }

    public static void RunBuildSourceChecks()
    {
        Type buildType = Type.GetType(
            "HumanPlaytesterBuild, Assembly-CSharp-Editor");
        Require(buildType != null,
            "HumanPlaytesterBuild must exist in the editor assembly.");
        MethodInfo build = buildType.GetMethod("BuildTestPlayer",
            BindingFlags.Public | BindingFlags.Static);
        Require(build != null,
            "HumanPlaytesterBuild.BuildTestPlayer must exist.");
        Debug.Log("HumanPlaytesterVerification build source checks passed.");
    }

    private static object Analyze(Pattern pattern)
    {
        Type analyzerType = Type.GetType(
            "ChartDifficultyAnalyzer, Assembly-CSharp");
        return analyzerType.GetMethod("Analyze",
            BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { pattern });
    }

    private static string PlanSignature(object plan)
    {
        List<string> parts = new List<string>();
        foreach (object action in ReadEnumerable(plan, "actions"))
        {
            parts.Add(string.Format("{0}:{1}:{2:F6}:{3:F6}",
                ReadProperty(action, "kind"),
                ReadProperty(action, "intendedJudgement"),
                ReadProperty(action, "timingOffset"),
                ReadProperty(action, "longNoteReleaseTime")));
        }
        return string.Join("|", parts);
    }

    private static int CountIntended(object plan, Judgement judgement)
    {
        int count = 0;
        foreach (object action in ReadEnumerable(plan, "actions"))
        {
            if ((Judgement)ReadProperty(action, "intendedJudgement") == judgement)
                count++;
        }
        return count;
    }

    private static int CountSevere(object plan)
    {
        int count = 0;
        foreach (object action in ReadEnumerable(plan, "actions"))
        {
            string kind = ReadProperty(action, "kind").ToString();
            Judgement judgement = (Judgement)ReadProperty(action,
                "intendedJudgement");
            if (kind != "Hit" || judgement == Judgement.Cool ||
                judgement == Judgement.Good || judgement == Judgement.Miss ||
                judgement == Judgement.Break)
            {
                count++;
            }
        }
        return count;
    }

    private static void VerifyOffsetsMatchJudgements(object plan)
    {
        foreach (object action in ReadEnumerable(plan, "actions"))
        {
            if (ReadProperty(action, "kind").ToString() == "Skip") continue;
            Note note = (Note)ReadProperty(action, "note");
            float offset = Convert.ToSingle(ReadProperty(action, "timingOffset"));
            Judgement intended = (Judgement)ReadProperty(action,
                "intendedJudgement");
            Judgement actual = GameInputManager.TimeDifferenceToJudgement(
                note, offset, 1f);
            Require(actual == intended,
                $"Offset {offset} produced {actual}, expected {intended}.");
        }
    }

    private static Pattern CreateSparsePattern(int level)
    {
        Pattern pattern = CreatePattern(level);
        for (int i = 0; i < 24; i++)
        {
            pattern.notes.Add(CreateNote(i * 0.8f, i % 4));
        }
        return pattern;
    }

    private static Pattern CreateDensePattern()
    {
        Pattern pattern = CreatePattern(level: 1);
        for (int i = 0; i < 64; i++)
        {
            float time = i * 0.075f;
            pattern.notes.Add(CreateNote(time, (i * 3) % 4));
            if (i % 4 == 0)
            {
                pattern.notes.Add(CreateNote(time + 0.001f, (i + 2) % 4));
            }
            if (i % 12 == 0)
            {
                HoldNote hold = new HoldNote
                {
                    type = NoteType.Hold,
                    pulse = 10000 + i,
                    lane = (i + 1) % 4,
                    time = time,
                    endTime = time + 1.2f,
                    duration = 240,
                    timeWindow = VerificationWindows()
                };
                pattern.notes.Add(hold);
            }
        }
        return pattern;
    }

    private static Pattern CreatePattern(int level)
    {
        Pattern pattern = new Pattern();
        pattern.patternMetadata.playableLanes = 4;
        pattern.patternMetadata.level = level;
        pattern.patternMetadata.guid = "verification-pattern";
        pattern.patternMetadata.patternName = "Verification";
        return pattern;
    }

    private static Note CreateNote(float time, int lane)
    {
        return new Note
        {
            type = NoteType.Basic,
            pulse = Mathf.RoundToInt(time * 1000f),
            lane = lane,
            time = time,
            timeWindow = VerificationWindows()
        };
    }

    private static Dictionary<Judgement, float> VerificationWindows()
    {
        return new Dictionary<Judgement, float>
        {
            { Judgement.RainbowMax, 0.016f },
            { Judgement.Max, 0.040f },
            { Judgement.Cool, 0.080f },
            { Judgement.Good, 0.120f },
            { Judgement.Miss, 0.180f }
        };
    }

    private static float ReadFloat(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property);
        Require(info != null, $"Missing result property {property}.");
        return Convert.ToSingle(info.GetValue(target));
    }

    private static int ReadCount(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property);
        Require(info != null, $"Missing result property {property}.");
        object value = info.GetValue(target);
        if (value is ICollection collection) return collection.Count;
        PropertyInfo count = value.GetType().GetProperty("Count");
        Require(count != null, $"Property {property} has no Count.");
        return Convert.ToInt32(count.GetValue(value));
    }

    private static IEnumerable ReadEnumerable(object target, string property)
    {
        return (IEnumerable)ReadProperty(target, property);
    }

    private static object ReadProperty(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property);
        Require(info != null, $"Missing property {property} on {target.GetType().Name}.");
        return info.GetValue(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
