# DMT2 DJ Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement DMT2-compatible cumulative DJ EXP and automatic levels for local USB profiles.

**Architecture:** A pure `DjProgression` rules class owns the exact DMT2 threshold and result-award tables. `ProfileManager` owns session EXP and persistence in `profile.json`; `GameController` awards once at the existing terminal stage transition; `ThemeProfileApi` exposes derived values to Lua. The T2 profile and gameplay UI read the derived level and no longer edit it.

**Tech Stack:** Unity C#, NUnit EditMode tests, MoonSharp Lua theme API, TECHMANIA profile JSON.

## Global Constraints

- Use DMT2 `expTitle.csv` thresholds and `ResultExpPoint.csv` awards exactly.
- DJ level is derived from cumulative EXP and capped at level 50.
- Logged-in USB profiles persist EXP; Guest EXP is session-only.
- Award a stage exactly once, including failed stages, and never from result-screen exits.
- Existing profiles without `userExp` migrate as zero EXP.

---

### Task 1: Arcade progression rules

**Files:**
- Create: `Assets/Scripts/DjProgression.cs`
- Test: `Assets/Scripts/Editor/DjProgressionTests.cs`

**Interfaces:**
- Produces: `DjProgression.LevelForExp(long)`, `ExpForResult(int, string)`, `ApplyAward(long, int, string)`.

- [ ] Write table-driven tests for threshold boundaries, rank awards, chart-level clamping, and non-negative saturation.
- [ ] Run the EditMode tests and confirm they fail because `DjProgression` does not exist.
- [ ] Implement the exact DMT2 tables and pure calculation methods.
- [ ] Run the focused EditMode tests and confirm they pass.

### Task 2: Profile persistence and stage award

**Files:**
- Modify: `Assets/Scripts/Serializable/ProfileData.cs`
- Modify: `Assets/Scripts/ProfileManager.cs`
- Modify: `Assets/Scripts/Components/Main Scene/Game/GameController.cs`
- Modify: `Assets/Scripts/Theme API/ThemeProfileApi.cs`
- Test: `Assets/Scripts/Editor/DjProgressionTests.cs`

**Interfaces:**
- Consumes: `DjProgression` pure rules.
- Produces: `ProfileManager.currentDjExp()`, `currentDjLevel()`, `AwardDjExperience(int, string)` and Lua equivalents.

- [ ] Add regression tests covering profile defaults and award results.
- [ ] Add `userExp` to `ProfileData`, cache/load it with the active profile, and persist after logged-in awards.
- [ ] Keep Guest EXP in memory and reset it at session initialization/logout.
- [ ] Award at the one-time clear/fail state transition before Lua callbacks.
- [ ] Expose EXP, level, and next-level threshold through `tm.profile`.

### Task 3: Derived-level UI and deployment

**Files:**
- Modify: `Assets/UI/Scripts/Profile Screen.txt`
- Modify: `Assets/UI/Scripts/Game Screen.txt`
- Modify: `Assets/UI/Scripts/Login Screen.txt`
- Modify: `Assets/UI/Scripts/Login VCE Flow.txt`

**Interfaces:**
- Consumes: `tm.profile.djLevel()`.
- Produces: consistent derived DJ badge level across login, song select, gameplay, and results.

- [ ] Replace theme-option/manual level reads with `tm.profile.djLevel()`.
- [ ] Disable the profile editor level field and remove its mutation callback.
- [ ] Refresh `djInfo.djLevel` immediately after terminal gameplay callbacks begin.
- [ ] Run focused EditMode tests and the existing login/result verification contracts.
- [ ] Build the Windows player and Technika 2 bundle, deploy the bundle to `_TestBuild3`, and update graphify.
