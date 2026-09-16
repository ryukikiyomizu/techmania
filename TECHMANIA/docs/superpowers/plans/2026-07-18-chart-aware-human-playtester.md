# Chart-Aware Human Playtester Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a test-only TECHMANIA player that analyzes real chart patterns, plays through the normal judgement pipeline like a capable imperfect human, and saves ordinarily valid Guest records.

**Architecture:** Pure chart analysis and human-planning classes convert a calculated `Pattern` into per-note pressure and seeded actions. `GameInputManager` executes those actions without enabling ordinary AutoPlay, while the Technika 2 theme stores the test toggle as a theme option. A dedicated Unity editor build entrypoint injects `TECHMANIA_HUMAN_PLAYTESTER` only into the `_TestBuild3` Windows build.

**Tech Stack:** Unity 6000.3.9f1, C#/.NET, UI Toolkit UXML, MoonSharp Lua theme scripts, Unity batch-mode editor builds.

## Global Constraints

- Derive performance from note timing, lanes, note types, overlap, travel, bursts, and recovery; never weight `PatternMetadata.level`.
- Keep `GameController.autoPlay` false and `Modifiers.Mode.Normal` while the human playtester is active.
- Never bypass `ScoreIsValid()`, assign score directly, or write `records.json` from the playtester.
- Bound action quality only; do not choose a target score, rank, or grade.
- Reach Results reliably without altering HP or score directly.
- Add song GUID/title and chart GUID/fingerprint/ruleset/pattern name to diagnostic logs.
- The source directory has no `.git`; skip commit commands and verify each task with compilation/tests plus file hashes.
- Deploy the rebuilt theme to `_TestBuild3\Themes\Technika 2.tmtheme` through the existing `BuildAssetBundleWindow` handoff.

---

## File Structure

- `Assets/Scripts/Components/Main Scene/Game/Human Playtester/ChartDifficultyAnalyzer.cs`: pure pattern feature extraction and choke detection.
- `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayProfile.cs`: deterministic seeded human state and judgement/action selection.
- `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayPlan.cs`: plan/action/context data types and timing-window offset conversion.
- `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayScheduler.cs`: frame-by-frame action execution state and diagnostics.
- `Assets/Scripts/Components/Main Scene/Game/GameInputManager.cs`: selects normal input, ordinary AutoPlay, or human test execution and maintains long notes.
- `Assets/Scripts/Components/Main Scene/Game/GameController.cs`: supplies chart identity context and emits final run diagnostics.
- `Assets/Scripts/Editor/HumanPlaytesterVerification.cs`: deterministic editor verification suite.
- `Assets/Scripts/Editor/HumanPlaytesterBuild.cs`: test-only Windows build and `_TestBuild3` deployment entrypoint.
- `Assets/UI/MainTree.uxml` and mirror `Technika 2 Theme/MainTree.uxml`: Settings toggle and popup description.
- `Assets/UI/Scripts/Theme Options.txt` and mirror: default `HumanPlaytester=False` key.
- `Assets/UI/Scripts/Select Song Screen.txt` and mirror: toggle state, hover popup, persistence.

### Task 1: Pure chart analysis

**Files:**
- Create: `Assets/Scripts/Components/Main Scene/Game/Human Playtester/ChartDifficultyAnalyzer.cs`
- Create: `Assets/Scripts/Editor/HumanPlaytesterVerification.cs`

**Interfaces:**
- Consumes: `Pattern.notes`, `Pattern.patternMetadata.playableLanes`, `Note.time`, `Note.lane`, `Note.type`, `HoldNote.endTime`, `DragNote.endTime`, `DragNote.nodes`.
- Produces: `ChartDifficultyAnalysis ChartDifficultyAnalyzer.Analyze(Pattern pattern)`; `float PressureFor(Note note)`; `List<ChartChokeSegment> chokes`.

- [ ] **Step 1: Add failing analyzer verification**

Create `HumanPlaytesterVerification.RunAnalyzerChecks()` that constructs calculated patterns with the same notes but different `level` values, plus sparse and dense/cross-lane/overlap variants. Assert:

```csharp
Require(Mathf.Approximately(level1.overallPressure, level99.overallPressure),
    "Displayed level changed analyzer output.");
Require(dense.overallPressure > sparse.overallPressure,
    "Dense chart was not harder than sparse chart.");
Require(dense.chokes.Count > 0,
    "Dense chart produced no choke segment.");
```

- [ ] **Step 2: Run verification to establish failure**

Run Unity batch mode with `-executeMethod HumanPlaytesterVerification.RunAnalyzerChecks -quit -batchmode -nographics`.
Expected: compile failure because analyzer types do not exist.

- [ ] **Step 3: Implement analyzer**

Add immutable result/segment types and calculate per-note pressure from three timing windows, chord proximity, normalized lane travel/reversal, repeated lanes, active long-note overlap, drag nodes, note-type transitions, local density spikes, and recovery gaps. Filter hidden lanes with `note.lane >= playableLanes`. Detect choke segments when smoothed pressure crosses `0.58`, merge gaps shorter than `0.35s`, and record dominant cause labels.

Use this public surface:

```csharp
public sealed class ChartDifficultyAnalysis
{
    public float overallPressure { get; }
    public IReadOnlyList<ChartChokeSegment> chokes { get; }
    public float PressureFor(Note note);
}

public static class ChartDifficultyAnalyzer
{
    public static ChartDifficultyAnalysis Analyze(Pattern pattern);
}
```

- [ ] **Step 4: Run analyzer verification**

Expected: Unity exits 0 and logs `HumanPlaytesterVerification analyzer checks passed.`

### Task 2: Seeded human plan and action-quality bounds

**Files:**
- Create: `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayPlan.cs`
- Create: `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayProfile.cs`
- Modify: `Assets/Scripts/Editor/HumanPlaytesterVerification.cs`

**Interfaces:**
- Consumes: `ChartDifficultyAnalysis`, ordered playable notes, each note's real `timeWindow` dictionary.
- Produces: `HumanPlayPlan HumanPlayProfile.BuildPlan(Pattern pattern, ChartDifficultyAnalysis analysis, int seed)` and `HumanPlayAction HumanPlayPlan.ActionFor(Note note)`.

- [ ] **Step 1: Add failing planner checks**

Add `RunPlannerChecks()` and assert identical seeds produce identical action kinds/offsets, different seeds differ, timing offsets map back to the intended real judgement through `GameInputManager.TimeDifferenceToJudgement`, a 20+ note plan contains at least one non-Rainbow action, and dense chart plans average more Cool/Good/Miss/Break actions than sparse plans across seeds 100-119.

- [ ] **Step 2: Run verification to establish failure**

Expected: compile failure because planner types do not exist.

- [ ] **Step 3: Implement plan and profile**

Define:

```csharp
public enum HumanPlayActionKind { Hit, Skip, DropLongNote }

public sealed class HumanPlayAction
{
    public Note note { get; }
    public HumanPlayActionKind kind { get; }
    public Judgement intendedJudgement { get; }
    public float timingOffset { get; }
    public float longNoteReleaseTime { get; }
}
```

Generate correlated decisions using persistent timing bias, slow drift, focus, fatigue, recent-error penalty, and recovery. Convert intended judgement to an offset strictly inside that judgement's adjacent timing-window bounds. Limit consecutive severe actions and inject one `Max` action only when a sufficiently long plan would otherwise be all Rainbow; never calculate a score/rank target.

- [ ] **Step 4: Run analyzer and planner verification**

Expected: Unity exits 0 and both verification sections pass.

### Task 3: Runtime integration and valid-record path

**Files:**
- Create: `Assets/Scripts/Components/Main Scene/Game/Human Playtester/HumanPlayScheduler.cs`
- Modify: `Assets/Scripts/Components/Main Scene/Game/GameInputManager.cs`
- Modify: `Assets/Scripts/Components/Main Scene/Game/GameController.cs`
- Modify: `Assets/Scripts/Editor/HumanPlaytesterVerification.cs`

**Interfaces:**
- Consumes: theme option key `TECHNIKA 3/HumanPlaytester`, `HumanPlayPlan`, `NoteManager.notesInLane`, `GameTimer.gameTime`.
- Produces: `bool GameInputManager.humanPlaytesterActive`, normal `HitNote`/Break/ongoing-note resolutions, and one load/result diagnostic pair.

- [ ] **Step 1: Add failing runtime checks**

Verify `HumanPlaytesterSettings.ParseEnabled()` accepts only a case-insensitive valid boolean true, the compile-disabled path returns false, diagnostics include all identifiers, and a normal modifier set still reports no special modifier.

- [ ] **Step 2: Run verification to establish failure**

Expected: compile failure because runtime settings/scheduler do not exist.

- [ ] **Step 3: Integrate scheduler behind compile define**

Under `#if TECHMANIA_HUMAN_PLAYTESTER`, read the theme option and build a scheduler during `GameInputManager.Prepare()`. Update input dispatch order to ordinary AutoPlay first, human playtester second, physical input third. Human execution calls `controller.HitNote(elements, action.timingOffset)`, leaves skipped notes for `CheckForBreak()`, and marks ongoing long notes each frame until their planned release. Keep `controller.autoPlay` false.

Add `HumanPlayRunContext` with song GUID/title, pattern GUID/name/fingerprint, and ruleset. Scope the loaded `Track` in `GameController.Load()` so its metadata can populate the context. Call scheduler result logging once when all playable notes resolve. Do not modify `ScoreIsValid()`, `ScoreIsNewRecord()`, or `UpdateRecord()`.

- [ ] **Step 4: Run complete verification**

Expected: Unity exits 0, existing scripts compile, and logs confirm ordinary `Modifiers.Mode.AutoPlay` remains special while the test player uses Normal.

### Task 4: Settings toggle in the Technika 2 theme

**Files:**
- Modify: `Assets/UI/MainTree.uxml`
- Modify: `Assets/UI/Scripts/Theme Options.txt`
- Modify: `Assets/UI/Scripts/Select Song Screen.txt`
- Modify mirror: `C:/Users/Jen/Downloads/Technika Projects/Technika 2 Theme/MainTree.uxml`
- Modify mirror: `C:/Users/Jen/Downloads/Technika Projects/Technika 2 Theme/Scripts/Theme Options.txt`
- Modify mirror: `C:/Users/Jen/Downloads/Technika Projects/Technika 2 Theme/Scripts/Select Song Screen.txt`

**Interfaces:**
- Produces persisted `themeOptions["HumanPlaytester"]` and visible `HUMAN PLAYTESTER` toggle.

- [ ] **Step 1: Add UXML toggle and popup**

Place `<ui:Toggle label="HUMAN PLAYTESTER" name="human-playtester" .../>` after FREEPLAY. Add an absolute popup label explaining that it runs a chart-aware imperfect test player and saves normal valid records; keep it outside flow so hovering never pushes controls.

- [ ] **Step 2: Add default and callbacks**

Initialize the key to false in `Theme Options.txt`. In `Select Song Screen.txt`, load it in `Show()`, show/hide its popup on pointer enter/leave, save on `ChangeBool`, and call `tm.options.SaveToFile()`.

- [ ] **Step 3: Synchronize and compare mirrors**

Run `fc.exe /b` for each live/mirror pair. Expected: no differences.

- [ ] **Step 4: Build and deploy theme**

Run Unity with `-executeMethod BuildAssetBundleWindow.BuildForDefaultPlatform`. Expected: `Built theme at Assets/AssetBundles/default`, successful copy to `_TestBuild3\Themes\Technika 2.tmtheme`, and exit 0.

### Task 5: Dedicated test-player build and verification

**Files:**
- Create: `Assets/Scripts/Editor/HumanPlaytesterBuild.cs`

**Interfaces:**
- Produces: `_TestBuild3/TECHMANIA.exe` and matching Unity runtime/data built with `TECHMANIA_HUMAN_PLAYTESTER`.

- [ ] **Step 1: Implement deterministic build entrypoint**

Create `HumanPlaytesterBuild.BuildTestPlayer()` using enabled `EditorBuildSettings.scenes`, `BuildTarget.StandaloneWindows64`, `_TestBuild3/TECHMANIA.exe`, and:

```csharp
BuildPlayerOptions options = new BuildPlayerOptions
{
    scenes = scenes,
    locationPathName = outputExe,
    target = BuildTarget.StandaloneWindows64,
    options = BuildOptions.CleanBuildCache,
    extraScriptingDefines = new[] { "TECHMANIA_HUMAN_PLAYTESTER" }
};
```

Throw when `BuildPipeline.BuildPlayer(options).summary.result` is not `BuildResult.Succeeded` and log total errors/warnings/output path on success.

- [ ] **Step 2: Run editor verification with the test define**

Run Unity batch mode with `-executeMethod HumanPlaytesterVerification.RunAll -extraScriptingDefines TECHMANIA_HUMAN_PLAYTESTER`. Expected: exit 0 and all analyzer/planner/runtime checks pass.

- [ ] **Step 3: Build `_TestBuild3` player**

Run Unity batch mode with `-executeMethod HumanPlaytesterBuild.BuildTestPlayer`. Expected: Windows64 success, updated `TECHMANIA.exe`/`TECHMANIA_Data`, no changes to root `Tracks`, `Themes`, `Skins`, or profile files.

- [ ] **Step 4: Package verification**

Verify `TECHMANIA.exe`, `TECHMANIA_Data/Managed/Assembly-CSharp.dll`, and `Themes/Technika 2.tmtheme` timestamps and SHA-256 hashes. Confirm Guest `records.json` still parses with zero records before launching the tester.

- [ ] **Step 5: Runtime smoke-test handoff**

Report the exact Settings toggle name, log location, package hashes, and expected first-run behavior: toggle on, choose a chart, see ordinary gameplay presentation with varied judgements, reach Results, and save the first valid New Record.

## Self-Review Result

- Spec coverage: every boundary, difficulty feature, validity rule, diagnostic identifier, theme toggle, build isolation, verification, and removal-friendly file boundary maps to a task.
- Placeholder scan: no deferred implementation markers are present.
- Type consistency: analyzer feeds profile/plan; plan feeds scheduler; scheduler is owned by input manager; controller supplies context and result totals.
- Scope decision: record reset was completed separately before this plan and is verified again during packaging.
