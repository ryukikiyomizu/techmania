# TECHMANIA fork changelog — `ryukikiyomizu/techmania` vs `techmania-team/techmania`

Generated 2026-09-16 by full-tree comparison (the fork has no usable Git history — see [Method](#method)).

## Status after cleanup (2026-09-17)

A follow-up pass removed the human playtester, all T2/T3 tooling and the session-record snapshot,
per request. **Sections 4 and 5 below, and the T2/T3 parts of sections 10-14, describe work that no
longer exists in this tree** - they are kept as the record of what the fork had done. See
[Cleanup applied](#cleanup-applied-2026-09-17) for the exact delta.

## Method

The fork contains exactly **one squashed commit** (`3d9269e "Sync Techmania source build 2"`, author
Ryuki, 2026-09-17), so there is no per-change history to read. This changelog was reconstructed by
cloning `techmania-team/techmania` and diffing the complete trees.

**Baseline:** the fork matches upstream `91182796` (2026-05-05, *"Merge pull request #35 from
rogeraabbccdd/ios"*, upstream HEAD) — 3,120 of 3,162 upstream files are byte-identical, and every
upstream file still exists. So this is a clean **fork-ahead-of-HEAD** delta, not a stale-fork merge gap.

**Before cleanup:**

| | count |
|---|---|
| Files in upstream | 3,162 |
| Files in fork | 4,336 |
| **Added** | **1,174** (1,109 non-`.meta`) |
| **Modified** | **42** |
| **Deleted** | **0** |

**After cleanup (current tree):**

| | count |
|---|---|
| Files in fork | 4,242 |
| **Added** | **1,081** - only **25** real source/doc files once `graphify-out/` is excluded |
| **Modified** | **38** (4 files reverted byte-identical with upstream) |
| **Deleted vs. pre-cleanup fork** | **94** (40 `.cs`, 42 `.meta`, 11 `.md`, 1 `.tmtheme`) |

Remaining authored delta: **12 new runtime + 6 new Editor C# files** (3,535 lines), 30 modified
`.cs`, `.vscode/`, `TECHMANIA.slnx`, `TechmaniaNfcHook.dll`, 2 design docs. Repo 501 MB → **486 MB**.
(Pre-cleanup it was 20 runtime + 38 Editor C# files / 11,495 lines.)

---

## Headline features

### 1. Player profile system (biggest change)
Upstream TECHMANIA has a single global `Documents\TECHMANIA\{records,stats,options}.json`. The fork
introduces full arcade-style per-player profiles.

- **New `ProfileManager.cs`** (1,275 lines) — static session owner with a `Guest` / `LoggedIn`
  session state invariant. "There is ALWAYS an active record/stat owner": Guest uses the cabinet's
  machine options plus `Profiles\Guest\`, a member uses that profile's records/stats/options.
  Fires a `profileChanged` event for themes; snapshots machine options at boot so logout restores
  clean defaults instead of the previous player's settings.
- **New `Paths` layout:** `Profiles/` folder, `Profiles/<name>/{profile,options,records,stats}.json`,
  plus `GetGuestProfileFolder()`.
- **One-time crash-safe migration** — `Paths.MigrateLegacyDataToGuestProfile()` moves legacy
  top-level `records.json` / `stats.json` into `Profiles/Guest/`. Each file self-gates on the target
  existing, so a mid-migration crash retries only the missing file. Call ordering is documented as
  load-bearing (`Startup.cs` runs it *before* `Statistics`/`Records.RefreshInstance()`).
- **Options split into machine vs. player scopes:**
  - New `Serializable/PlayerOptions.cs` — travelling preferences (volumes, appearance, timing offsets,
    cosmetic/DJ fields), overlaid onto `Options.instance` at login and extracted back at save.
  - `Options.SaveToFile()` became `virtual` and is overridden with profile-aware **split save**: while
    logged in, machine fields persist using the pre-login snapshot and only live player fields go to the
    profile file.
  - New `Serializable/MachineThemeOptions.cs` — operator-scoped `TECHNIKA 2` theme keys (arcade
    lifecycle, countdown clock, attract screen, F2 diagnostics) that a card login must **not** reset,
    and that are stripped from profile copies so stale values can't resurrect.
- **New `TestProfileBootstrap.cs`** — F1 create "TestPlayer" profile / F2 logout / F3 print current
  profile. *Added as a live component in `Assets/Scenes/Main.unity`.*

### 2. AIC Pico NFC card login + multi-credential profiles
Beyond USB-token login, the fork adds physical arcade card support (design doc
`specs/2026-08-15-aic-pico-multi-credential-login-design.md`).

- **New `NFC/NfcReaderService.cs`** + **`NFC/TechmaniaNfcNative.cs`**, backed by a shipped native
  plugin `Assets/Plugins/x86_64/TechmaniaNfcHook.dll` (145 KB). Emits `Present`/`Removed`
  `CredentialObservation` events with source, card kind, credential key and sequence number.
- Follows `whowechina/aic_pico` firmware `5a0dbc9d`; supports **FeliCa/Amusement IC** and
  **MIFARE (Ultralight/NTAG)**, normalizing to the Pico's canonical 8-byte CardIO ID as uppercase hex
  (4-byte UID → `E0 04` + UID + first 2 UID bytes; 7-byte UID → `E0` + UID).
- **New `Serializable/ProfileCredentials.cs`** — a profile now owns *multiple* credentials. Namespaced
  keys `usb:token:<v>`, `nfc:cardio:<v>`, `legacy:card:<v>` prevent cross-transport collisions; includes
  `LookupCandidates` ambiguity resolution, dedupe/normalize on read *and* write, and `TryLink`.
- **`ProfileData` gains a format-version migration chain** (`ProfileDataV1` → `ProfileData` v2): new
  `profileId` GUID and `credentials` list; legacy `cardId` retained so older builds can still identify
  the profile, and auto-linked into `credentials` on upgrade.

### 3. DMT2-accurate DJ EXP / level progression
- **New `DjProgression.cs`** — pure rules class holding the arcade tables verbatim from DMT2
  `expTitle.csv` (cumulative EXP thresholds, index = level, level-1 floor) and `ResultExpPoint.csv`
  (per-rank awards, chart-level clamping). `LevelForExp`, `ExpForResult`, `ApplyAward`; level derived
  from cumulative EXP, **capped at 50**; non-negative saturation.
- Logged-in USB/NFC profiles persist `userExp`; **Guest EXP is session-only**; existing profiles without
  `userExp` migrate as zero. Awarded exactly once at the terminal stage transition — *including failed
  stages* — and never from result-screen exits.
- Surfaced to Lua through `ThemeProfileApi`; T2 profile/gameplay UI reads the derived level and can no
  longer edit it.

### 4. ~~Chart-aware "Human Playtester" (test builds only)~~ — **REMOVED**
A bot that plays like a capable-but-imperfect human so result-screen/new-record paths can be tested
without fabricating scores (spec `specs/2026-07-18-chart-aware-human-playtester-design.md`).

- **New `Human Playtester/` module:** `ChartDifficultyAnalyzer.cs` (per-note pressure 0–1 + choke/recovery
  segments from onset density, chords, lane travel/reversals, same-lane repeats, hold/drag overlap, drag
  path complexity, chains/repeats, density spikes vs. local baseline, BPM/time-stop boundaries),
  `HumanPlayProfile.cs` (per-run seed; base accuracy, persistent early/late bias, slow timing drift,
  focus/fatigue/recovery), `HumanPlayPlan.cs`, `HumanPlayScheduler.cs`.
- Hard invariants: must **not** read or weight the displayed chart level (diagnostic logging only);
  `GameController.autoPlay` stays false and modifier stays `Mode.Normal`; no normal autoplay indicator;
  never bypasses `ScoreIsValid()` or writes `records.json`; only action *quality* is bounded — never a
  target score/rank.
- `GameInputManager` gains a third input path (`HandleHumanPlaytester()`) that drives `controller.HitNote`
  with per-action timing offsets, maintains long-note holds, and caps at 8 notes resolved per frame per lane.
- **Gated behind the `TECHMANIA_HUMAN_PLAYTESTER` scripting define**, injected only by
  `Editor/HumanPlaytesterBuild.cs` / `LoadProbeBuild.cs` for the separate `_TestBuild3` Windows build —
  the regular build keeps upstream behavior (`scriptingDefineSymbols: {}` is empty in `ProjectSettings`).

### 5. ~~Star GUIDE overlay (T2 tutorial hands)~~ — **REMOVED**
- **New `StarGuideOverlay.cs`** (411 lines) + **`StarGuideTimingPolicy.cs`** — TECHMANIA positions
  pre-styled "hand" elements over upcoming Star notes; the theme owns the ten T2 textures, VCE timing,
  orientation and note-state semantics.
- Chain handling made continuous: `ChainElementsBase` gains `GuideChainHead`,
  `NextChainNodeForGuide` and `AssignGuideChainHeadToLinkedNodes()`, so the head keeps owning
  presentation after gameplay resolves it — one unbroken hand glide across dense/high-BPM chains instead
  of re-acquiring per node. `NoteManager` wires the assignment at chain-head creation.
- `ThemeApi.GameSetup` exposes optional `guideContainer` + `guideEnabled` so themes can opt in.

### 6. Audio: latency, clock stability and load path
The most invasive engineering work outside profiles.

- **GameTimer audio-clock sync (anti-drift).** `GameTimer.Update()` now accepts `float? audioTime` and,
  when a backing track is playing, nudges its stopwatch offset toward FMOD's actual playback position with
  an exponential correction (`kAudioSyncRate = 2f`), which also low-pass-filters the DSP-quantized position.
  Reverts to pure stopwatch when no audio time is available; `kSyncToAudioClock` is a one-line revert switch.
  Fed by new `GameBackground.TryGetSyncTime()` → `FmodChannelWrap.TryGetTimeSeconds()` (returns `false`
  silently instead of `EnsureOk`-spamming invalid-handle warnings per frame).
- **New low-latency audio options.** `audioBufferSize`/`numAudioBuffers`/`useAsio` reclassified as
  *machine* settings; defaults dropped **1024×4 → 512×2**; `GetDefaultAudioBufferSize()` deprecation
  warning removed and returns 512; `ApplyAudioBufferSize()` now actually validates+persists (clamp
  16–2048 / 2–8) instead of no-op'ing; new **`ApplyLowLatencyAudioPreset()`** (ASIO on, 256×2).
- **FMOD robustness:** `setDSPBufferSize` failure no longer aborts startup (tiny buffers rejected by a
  device fall back to FMOD's default with a warning); new mixer-rate matching via
  `getDriverInfo` + `setSoftwareFormat` to skip an extra resampling stage, defensively skipped on error.
- **New `FmodManager.CreateSoundFromFile()`** — decodes natively in FMOD with `CREATESAMPLE`, removing the
  `UnityWebRequest → AudioClip → PCM marshal-copy` round trip. `ResourceLoader` uses it whenever
  `File.Exists`, keeping the web-request path only for in-APK `StreamingAssets` on Android, with a
  `yield return null` so the loading screen still updates.
- **New `FmodChannelWrap.SetLoopPoints(startSec, endSec)`** — applied to the *active* channel, not just
  future sound defaults.

### 7. Touch input sub-frame accuracy
- **New dependency `com.unity.inputsystem` 1.18.0.**
- `GameInputManager` switches the Touch control scheme from legacy `Input.GetTouch` to Input System
  **EnhancedTouch**, judging each tap at its real event timestamp (`t.startTime` / `t.time`) rather than
  frame time; mouse fallback uses a per-frame `frameRealtime` anchor captured at the top of `Update()`.
  `Startup.Start()` calls `EnhancedTouchSupport.Enable()`.
- `VisualElementWrap.EventType` gains **`PointerCancel`**.

### 8. Records: max combo, Custom ruleset, No Fail scoring
- **`Record.maxCombo`** added, carried through `Clone`, persisted, and **only improved, never lowered**
  (`UpdateRecord(..., int totalScore, int maxCombo, ...)` signature change).
- **Custom rulesets now save records.** Previously `GetRecord`/`UpdateRecord`/setlist equivalents all
  early-returned `null`/nothing for `Options.Ruleset.Custom`; fork adds a `Custom` dictionary to both
  `recordDict` and `setlistRecordDict` and removes every guard.
- **No Fail no longer invalidates scores:** `HasAnySpecialModifier()` was `mode != Mode.Normal` →
  `mode == Mode.AutoPlay || mode == Mode.Practice`.
- New identity-based lookup `GetRecordByIdentity(guid, fingerprint, ruleset)` so Song Select's lightweight
  entries resolve against whichever `Records` instance owns the active profile, instead of retaining Guest
  `Record` objects indexed at startup.

### 9. External scores drive (cabinet portability)
- **New `ExternalRecordsWatcher.cs`** (139 lines) hosted by `Startup` for the whole session — deliberately
  in the C# core rather than theme Lua so it survives theme switches. Every 8s, and **only while
  `GameController.IsIdle`** (new property), it checks whether the drive holding `externalRecordsPath` is
  available, redirects records load *and* save to that portable `records.json` (creating it if missing) or
  back to local, reloads `Records.instance`, and notifies the theme. Only acts on real transitions.
- `Paths` gains `Set/ClearRecordsFilePathOverride` and `Set/ClearStatsFilePathOverride`
  (`[MoonSharpHidden]`); one override covers both read and write. Default records path moved from
  `dataFolder` to the **Guest profile**, so an unplug fallback lands there automatically.
- New options `checkExternalDriveForScores` (off) + `externalRecordsPath` (default `D:\records.json`).
- **`Techmania.cs` (Lua API) gains `SetOnRecordsReloaded(callback)`** / `InvokeRecordsReloaded()`, reset
  per theme session, so themes can re-render on-screen scores after a swap.

### 10. Gameplay visuals & options
- **New note-size option:** `Options.NoteSize { Big, Normal, Small }`, default **Big**, exposed to Lua as
  `tm.enums.noteSize`.
- `GameLayout.noteVisualLaneHeight` + `GetNoteScaleCompensation(lanes, noteSize)` compensate per lane
  count (4-key: Big ×1.15 / Small ×0.92; other: Small ×0.86) so notes don't distort across layouts.
  All note subclasses (`BasicNoteElements`, `RepeatPathElements`, …) switched to the compensated height.
- **Pop Mixing draw order preserved:** `NoteManager` keeps reverse-spawn order for 4-key
  (`preservePopOverlap`) so simultaneous notes intentionally overlap instead of being visually flattened,
  while `xyzInLane` stays in original order for judgement traversal. Logs `[T2 Notes] Preserving Pop
  overlapped-note draw order.`
- **Per-frame style-write elimination (perf):** new `GameplayStyleExtensions.cs` with
  `SetBackgroundSpriteIfChanged()`. Writing `style.backgroundImage` marks a UI Toolkit element dirty even
  when the value is identical; gameplay reassigned note/trail/scanline/fever/approach/VFX/combo-digit
  sprites every frame. Applied across `GameLayout`, `NoteElements`, `VfxAndComboText` and the note
  subclasses. Behaviour identical — redundant writes removed.

### 11. Attract/demo mode and loading pipeline
- **New `DemoPlayableNoteFilter.cs`** + `GameSetup` `demoPlayableNoteWindowEnabled` /
  `demoPlayableNoteFirstScan` / `demoPlayableNoteLastScanExclusive`: when on, playable notes outside the
  scan window are **removed before `NoteManager` creates any visual or judgement objects**, while hidden
  audio/timing notes remain — an attract screen that stays cheap. Applied via
  `setup.ApplyDemoPlayableNoteWindow()` during `LoadSequence()`.
- **BGA prepared concurrently with loading:** `VideoPlayer.Prepare` is kicked off right after the loading
  background image so file-open/decoder setup overlaps skin/audio/keysound loading, with
  `cancelPendingBga` cleanup on error and on release, plus `sequentialBga`/`skipBga` A-B switches.
- **New `LoadProbe.cs`** — opt-in file-only timing trace (`-loadProbe`, `-loadProbeSkipBga`,
  `TECHMANIA_LOAD_PROBE_LOG`, default `load-probe.tsv` in persistentDataPath) with `Mark()`/`Event()`
  stage stamps through `LoadSequence` (`chart-and-modifiers`, `keysound-list`, `background-image`,
  `bga-prepare-start/end`, `error`). Built only via `Editor/LoadProbeBuild.cs`.
- **Theme loading in-editor:** `GlobalResourceLoader` adds `ShouldLoadLiveEditorTheme()` +
  `LoadThemeFromProjectAssetsCoroutine()`, loading `Default` and `Technika 2` straight from
  `Assets/UI` via `AssetDatabase` (no AssetBundle round trip), with VSync temporarily disabled.
  Also fixes two upward-traversal loop guards with `!string.IsNullOrEmpty(processingRelativeFolder)`
  (track and setlist folders) that could spin on a root-relative path.
- **`BootScreen` theme path discovery:** instead of hardcoding `assets/ui/maintree.uxml` /
  `assets/ui/mainscript.txt`, falls back to the first theme key ending in `/maintree.uxml` /
  `/mainscript.txt` — required by the newer per-theme content layout.

### 12. Theme Lua API additions
- **`Techmania.instance.profile` → new `Theme API/ThemeProfileApi.cs`** — the persistence boundary for the
  whole login system: `hasPendingToken()`, `loginByToken()`, `loginWithPendingToken()`,
  `createProfileWithPendingToken(name)`, `rescanForToken()`, profile/EXP/cosmetic reads, and a
  `profileChanged` handler with explicit **`ClearOnProfileChanged()`**, called from `ScriptSession`
  before each new Lua session so stale closures don't stay hooked to `ProfileManager.profileChanged`.
- New enums exposed: `Options.NoteSize` (as `noteSize`), `ProfileManager.SessionState` (as `sessionState`).
- `NoteList.ForEachActive(Action<INoteHolder>)` added (skips inactive without allocating a filtered list).

### 13. Build pipeline & asset hygiene — **T3/session-record parts removed**
- **Barebone Default theme enforced:** `BuildPostProcessor` now copies a committed
  `BuildResources/Default.tmtheme` (14.6 MB) instead of the freshly built bundle, **throws if it's
  missing**, and **throws if ≥ 90 MiB** (`MaxBareboneThemeBytes`), so shipped install size can't silently
  regress. New `CopyT3AssetBundle` post-step deploys `AssetBundles/t3` as `T3 HD.tmtheme` (skips silently
  if unbuilt).
- `BuildAssetBundleWindow`: `BuildAssetBundleOptions.None` → **`ForceRebuildAssetBundle`**; throws if Unity
  returns no manifest; `AssignT3Bundle.Assign()` + forced refresh before build; per-bundle log path.
- **New `GenerateSessionRecordSnapshot()`** — because the pinned `_TestBuild3` runtime predates
  `Record.maxCombo` in Lua, the build writes `Assets/UI/Data/session_records.txt` (tab-separated
  `profile / guid / fingerprint / score / maxCombo / medal`) scanned from `Documents\TECHMANIA\Profiles\*`
  so the T2 popup can still show BEST / MY / MAX COMBO accurately. Per-profile failures warn and skip.
- **New `Editor/AssignT3Bundle.cs`** (menus `Window/TECHMANIA Theme/*`) and
  **`Editor/AutoBuildThemeWhenSourcesChange.cs`** (`[InitializeOnLoad]`; rebuilds on editor load and on
  `ExitingEditMode` when theme sources are newer, with a session-keyed last-attempt timestamp).
- `Packages/manifest.json` adds **`com.unity.inputsystem` 1.18.0** and
  **`com.coplaydev.unity-mcp`** (Unity MCP, Git UPM dep); `packages-lock.json`, `PackageManagerSettings.asset`
  and `ProjectSettings.asset` re-serialized (Unity **6000.3.9f1** unchanged; the only functional-looking
  `ProjectSettings` change is an added iPhone icon block with empty `m_Textures`, i.e. incidental).
- TextMeshPro atlases genuinely regenerated: `NotoSansCJK-Regular SDF.asset` (4,686 diff lines) and
  `OpenSans-Regular SDF.asset` (198) gained glyph records (e.g. new glyph index 368) for arcade typography.
- `Assets/Fonts/*` SDF + `UserSettings/Search.settings` (local editor state) also changed.

### 14. ~~Verification & test harness (38 files, 7,588 lines)~~ — **REDUCED to 6 files**
New Editor-only suites, deliberately contract-first (specs say verifiers "must fail before and pass
after"):

- **NUnit `*Tests.cs` (12):** `DjProgressionTests`, `NfcReaderServiceTests`, `ProfileCredentialTests`,
  `ProfileCredentialFlowTests`, `MachineThemeOptionsTests`, `T2ProfileSessionBehaviorTests`,
  `T2ResultSkipFinalizationTests`, `T2ThemeOptionSnapshotTests`, `T2ThemeAudioNilChannelTests`,
  `T2TouchWaveInputTests`, `T2AnimationPlanTests`, `T2DemoPlayableNoteFilterTests`.
- **`*Verification.cs` (16):** `HumanPlaytesterVerification`, `MainGameArcadePresentationVerification`,
  `ResultVceVerification`, `SongSelectHeaderShineVerification`, `T2AllClearArcadeVerification`,
  `T2AttractPolishVerification`, `T2AuthenticCosmeticCatalogVerification`, `T2CurrentAnimationVerification`,
  `T2GameplayVisualRegressionVerification`, `T2LoginVceUsbVerification`, `T2NoteScaleVerification`,
  `T2ProfileArcadeHybridVerification`, `T2ReportedRegressionsVerification`,
  `T2Screenshot27RegressionVerification`, `T2SongSelectBehaviorVerification`, `T2ThemePackageVerification`.
- **Contracts/tools (10):** `ResultScreenTimerContract`, `T2TrackSortingContract`,
  `T2ArcadeHudAssetCompiler`, `T2ProfileActionSpriteGenerator`, `T2ProfileCardAtelierBuild`,
  `T2VceDecoder` (decodes generated glyph PNGs and asserts alpha bounds), `HumanPlaytesterBuild`,
  `LoadProbeBuild`, `AssignT3Bundle`, `AutoBuildThemeWhenSourcesChange`.

### 15. Documentation — **reduced 12 docs → 2**
`TECHMANIA/docs/superpowers/` — 6 plans + 6 specs covering chart-aware human playtester, arcade-accurate
USB login, full arcade login choreography, login runtime polish + mode input, DMT2 DJ progression, T2
profile editor (Arcade Hybrid "Direction C"), and AIC Pico NFC multi-credential login. These are the
authoritative intent records for items 1–5 above and are the only reason this changelog could be written
with confidence.

Explicit design constraints worth repeating: **never print USB tokens or card IDs** in test/build output;
USB is a *card* that locates a local profile, **not** a profile; no emulated remote membership service,
no ID/password form, no profile-format change; `MODE NOT AVAILABLE` overlay **removed** — Club/Duo/Crew
header regions are silently consumed, only Star and Pop cycle; every login state has a deterministic exit
so no failure can trap the player; transitions wait on authoritative VCE clip duration, not generic fades.

### 16. Misc
- `.vscode/{settings,extensions,launch}.json`, `TECHMANIA/TECHMANIA.slnx`, `Assets/Resources.meta`,
  `BuildResources/README.md`.
- Missing trailing newlines fixed in several files (`NoteElements.cs`, `GameSetup.cs`, `ScriptSession.cs`,
  `VisualElementWrap.cs`, `NoteList.cs`, …).

---

## Cleanup applied (2026-09-17)

Scope: remove the human playtester, all T2/T3 tools and the session-record snapshot, plus the
placeholder folders — falling back to the default theme.

### Deleted (94 files)

| group | files |
|---|---|
| **Human playtester** | `…/Game/Human Playtester/` → `ChartDifficultyAnalyzer`, `HumanPlayProfile`, `HumanPlayPlan`, `HumanPlayScheduler` (which also owned `HumanPlaytesterSettings`); `Editor/HumanPlaytesterBuild.cs`; `Editor/HumanPlaytesterVerification.cs` |
| **T2 runtime hooks** | `StarGuideOverlay.cs`, `StarGuideTimingPolicy.cs`, `DemoPlayableNoteFilter.cs` |
| **T2/T3 + arcade tools (31)** | all 24 `Editor/T2*.cs`, `Editor/AssignT3Bundle.cs`, and 4 arcade verifiers that depended on them or on absent theme files: `MainGameArcadePresentationVerification` (literally parsed `T2ArcadeHudAssetCompiler.cs`), `ResultVceVerification`, `SongSelectHeaderShineVerification`, `ResultScreenTimerContract` |
| **Machine theme-option layer** | `Serializable/MachineThemeOptions.cs`, `Editor/MachineThemeOptionsTests.cs` |
| **Placeholders** | `BuildResources/` (`Default.tmtheme` + `README.md`), orphan `Assets/Resources.meta` |
| **Docs (10)** | playtester, arcade-USB-login, full-arcade-login-choreography, login-runtime-polish, T2-profile-editor (plans + specs) |

Kept docs: `plans/2026-08-11-dmt2-dj-progression.md`, `specs/2026-08-15-aic-pico-multi-credential-login-design.md`.

### Edited (11 files)

- **`GameInputManager.cs`** — dropped scheduler field, `humanPlaytesterActive`, the `Prepare()` gate,
  `HandleHumanPlaytester()`, `LogHumanPlaytesterResult()`, the `Dispose()` reset, the
  `HumanPlayRunContext` ctor parameter and the now-unused `pattern` field. **Kept** the EnhancedTouch
  per-touch timestamp work.
- **`GameController.cs`** — dropped `starGuideOverlay` (field + creation + per-frame update),
  `ApplyDemoPlayableNoteWindow()`, the `HumanPlayRunContext` block and the now-dead `loadedTrack`
  local, and the `LogHumanPlaytesterResult()` call. `ScoreIsValid()` / `SetlistScoreIsValid()` no
  longer route through `HumanPlaytesterSettings`; now `!setup.modifiers.HasAnySpecialModifier() &&
  !stageFailed` (the fork's deliberate removal of the upstream `ruleset != Custom` guard is **kept**,
  since Custom-ruleset records still save).
- **`ChainElementsBase.cs`, `NoteManager.cs`, `GameSetup.cs` → byte-identical to upstream again.**
  `NoteManager`'s `preservePopOverlap` turned out to be a dead variable feeding only a
  `[T2 Notes]` `Debug.Log`, with no rendering effect, so it left with the log.
- **`BuildPostProcessor.cs` → reverted to upstream**: copies the freshly built `AssetBundles/default`
  to `Themes/Default.tmtheme`. No `BuildResources` requirement, no 90 MiB guard, no `CopyT3AssetBundle`.
- **`BuildAssetBundleWindow.cs`** — removed `SessionRecordFile`/`SessionRecordEntry` +
  `GenerateSessionRecordSnapshot()` (so no `Assets/UI/Data/session_records.txt`), `AssignT3Bundle.Assign()`
  and `CopyDefaultBundleToSelectedTechnikaTheme()` (so no `_TestBuild3` handoff). **Kept** the
  `manifest == null` guard and the per-bundle log fix; dropped `ForceRebuildAssetBundle`, which only
  existed to guarantee the generated snapshot got repacked.
- **`PlayerOptions.cs`** — theme options now replace wholesale on login, no machine-scoped carve-out.
- **`GlobalResourceLoader.cs`** — live in-editor theme loading narrowed to the default theme.
- **`LoadProbeBuild.cs`** — no longer injects `TECHMANIA_HUMAN_PLAYTESTER`.
- **`AutoBuildThemeWhenSourcesChange.cs`** — watches `Assets/UI` only; dropped absent `Assets/T3`.

### Deliberately kept

All of sections 1-3 and 6-13: profiles, NFC/multi-credential login, DJ progression, audio-clock sync +
FMOD/latency work, EnhancedTouch timing, records changes, external-records drive, the note-size option,
`GameplayStyleExtensions` (pure perf work in the gameplay loop), `LoadProbe`, `TestProfileBootstrap`,
and `BootScreen`'s generic per-theme `maintree.uxml` discovery.

### Verified

- Zero remaining references anywhere under `Assets/` to `HumanPlay*`, `Playtester`, `PLAYTESTER`,
  `AssignT3Bundle`, `SessionRecord`, `session_records`, `MachineThemeOptions`, `StarGuide`,
  `DemoPlayableNote`, `guideContainer`, `guideEnabled`, `BuildResources`, `_TestBuild3`, `Technika`, or
  any `T2<Identifier>` (only upstream's pre-existing `levelsInT1/T2` locals in `TrackCompare.cs` and the
  `DMT2` comment in the retained `DjProgression.cs` match loosely).
- No deleted script GUID appears in any `.unity`, `.prefab`, `.asset`, `.uxml` or `.uss` file → no
  "missing MonoScript" holes in `Main.unity`.
- Orphan `.meta` set now matches upstream exactly; the deletions introduced none.
- **Not compile-verified** — no Unity toolchain in this sandbox. The 4 retained Editor test files were
  individually checked for references to deleted types and are clean.

---

## Concerns and repo hygiene

1. **[STILL OPEN] 208 MB of committed tool cache.** `TECHMANIA/graphify-out/` is 1,031 tracked files (dated snapshots
   `2026-08-09`…`2026-08-19`, `cache/ast` = 986 files, `graph.html`, `GRAPH_REPORT.md`) — about 41% of the
   501 MB checkout. It's regenerable (`graphify update .`) and should be gitignored - plus a second stray cache under `TECHMANIA/Assets/Scripts/graphify-out/`. There is **no
   `.gitignore` anywhere** in the fork or upstream, which is why this got in; the cache is now ~98% of the fork's entire added file count. `UserSettings/Search.settings`
   (local editor state) belongs in the same bucket.
2. **[LARGELY RESOLVED] The theme half of every arcade feature is missing from the repo.** `Assets/UI` is *byte-identical* to
   upstream (261 tracked files, unchanged), and there is no `Assets/T3`, no `Technika 2 Theme/`, no
   `Assets/UI/Data/`, and no `tools/generate_login_vce_assets.py` — all of which the plans/specs and the new
   Editor code reference. Consequences: the C# side is **dangling hooks with no caller** (the HUMAN
   PLAYTESTER Settings toggle, `guideContainer`, `demoPlayableNoteWindow`, external-scores toggle, and
   `session_records.txt` all have no theme-side UI or asset source here), `AssignT3Bundle` and
   `T2ProfileCardAtelierBuild` would find nothing to build, and the Login/Profile-editor VCE work is
   reproducible only on the original machine (work was deployed out-of-band to
   `_TestBuild3\Themes\Technika 2.tmtheme`). **This tree cannot reproduce the fork's headline features.**
3. **[RESOLVED] Three Editor scripts hardcoded a personal path** and would fail for everyone else:
   `T2ArcadeHudAssetCompiler.cs`, `T2GameplayVisualRegressionVerification.cs`, and
   `T2LoginVceUsbVerification.cs` pin `C:\Users\Jen\Downloads\Technika Projects\t2\resource\…` as their
   visual authority. All three scripts are now deleted, so this is resolved.
4. **`TestProfileBootstrap` ships in the real scene.** It has no `#if UNITY_EDITOR` and no test-build
   define guard, and was added as a `MonoBehaviour` on a `Main.unity` GameObject — so F1/F2/F3 profile
   debug hotkeys are live in every build. Given items 1–5 of the playtester spec are carefully fenced
   behind `TECHMANIA_HUMAN_PLAYTESTER`, this looks like an oversight.
5. **`com.coplaydev.unity-mcp` is a Git-URL UPM dependency** in a player `Packages/manifest.json`, with
   `m_EnablePreviewPackages: 1` removed from `PackageManagerSettings` — dev/agent tooling leaking into the
   shipping manifest, and a floating `#main` ref that isn't reproducible. Worth dropping or moving to a
   separate editor-only manifest.
6. **[RESOLVED] Dangling `.meta`:** the fork-introduced `Assets/Resources.meta` (no such folder) is
deleted; the four remaining orphan `.meta`s are pre-existing upstream and were left untouched.
7. **`PERFORMANCE-AND-FEATURES-BACKLOG.md` doesn't exist**, but `GameplayStyleExtensions.cs` cites it by
   name ("See PERFORMANCE-AND-FEATURES-BACKLOG.md (P1)"). Same class of problem as item 2: the rationale
   doc isn't in the repo.
8. **Binary blob committed directly:** `TechmaniaNfcHook.dll` (145 KB, no source, x86_64-only — so NFC is
   Windows-only and unauditable here) wants release-asset handling rather than Git.
   `BuildResources/Default.tmtheme` (14.6 MB) is gone with the cleanup, which is where the ~15 MB size
   drop came from.
9. **One squashed commit destroys the change history.** Every feature above had to be reverse-engineered
   from trees + docs; the plans themselves note "the source directory has no `.git`; skip commit commands".
   Real commits per feature (and ideally upstream-mergeable branch history) would make this repo
   reviewable and keep future upstream syncs sane.
10. **Platform-scope caveat:** the fork is Windows-cabinet-shaped (NFC plugin x86_64-only,
    `D:\records.json` default, `Documents\TECHMANIA` path assumptions, Windows batch-mode builds), while
    upstream HEAD just finished an iOS/Android PR merge. Worth confirming the mobile build still boots —
    `ProfileManager.Initialize()` + `ExternalRecordsWatcher.ApplyInitialSource()` now sit on the `Startup`
    path for all platforms.

---

## Appendix: all 42 modified files, by size of change

Diff lines (`+`/`−`, excluding headers). Binary/asset re-serializations marked.

| ± | file |
|---|---|
| 4,686 | `Assets/Fonts/Noto Sans CJK/NotoSansCJK-Regular SDF.asset` *(glyph atlas regen)* |
| 198 | `Assets/Fonts/Open Sans/OpenSans-Regular SDF.asset` *(glyph atlas regen)* |
| 145 | `Assets/Scripts/Components/Main Scene/Game/GameController.cs` |
| 140 | `Assets/Scripts/Components/Main Scene/Game/GameInputManager.cs` |
| 123 | `Assets/Scripts/Editor/BuildAssetBundleWindow.cs` |
| 116 | `Assets/Scripts/Paths.cs` |
| 103 | `Assets/Scripts/Serializable/Options.cs` |
| 99 | `ProjectSettings/ProjectSettings.asset` *(incidental iPhone icon block)* |
| 80 | `Assets/Scripts/Components/Main Scene/GlobalResourceLoader.cs` |
| 68 | `Assets/Scripts/Serializable/Records.cs` |
| 57 | `Assets/Scenes/Main.unity` |
| 56 | `Assets/Scripts/Fmod/FmodManager.cs` |
| 50 | `Assets/Scripts/Components/ResourceLoader.cs` |
| 41 | `Assets/Scripts/Editor/BuildPostProcessor.cs` |
| 39 | `Assets/Scripts/Fmod/FmodChannelWrap.cs` |
| 32 | `Assets/Scripts/Components/Main Scene/Game/GameTimer.cs` |
| 29 | `Assets/Scripts/Components/Main Scene/Game/GameLayout.cs` |
| 27 | `ProjectSettings/PackageManagerSettings.asset` |
| 26 | `Packages/packages-lock.json` |
| 24 | `Assets/Scripts/Components/Main Scene/Startup.cs` |
| 21 | `Assets/Scripts/Theme API/GameSetup.cs` |
| 20 | `Assets/Scripts/Theme API/Techmania.cs` |
| 18 | `…/NoteElements Subclasses/ChainElementsBase.cs` |
| 17 | `…/Game/NoteManager.cs` |
| 14 | `Assets/Scripts/Components/Main Scene/BootScreen.cs` |
| 10 | `…/Game/NoteElements.cs` |
| 9 | `Assets/Scripts/Data Structures/NoteList.cs` |
| 8 | `Assets/Scripts/Theme API/ScriptSession.cs` |
| 8 | `…/NoteElements Subclasses/HoldTrailElements.cs` |
| 8 | `…/NoteElements Subclasses/ChainNodeElements.cs` |
| 8 | `…/Game/GameBackground.cs` |
| 6 | `…/Game/VfxAndComboText.cs` |
| 6 | `…/NoteElements Subclasses/RepeatPathElements.cs` |
| 5 | `UserSettings/Search.settings` |
| 4 | `…/NoteElements Subclasses/HoldNoteElements.cs` |
| 4 | `…/NoteElements Subclasses/DragNoteElements.cs` |
| 3 | `Assets/Scripts/Theme API/VisualElementWrap.cs` |
| 3 | `…/NoteElements Subclasses/ChainHeadElements.cs` |
| 3 | `…/NoteElements Subclasses/BasicNoteElements.cs` |
| 2 | `Packages/manifest.json` |
| 2 | `…/NoteElements Subclasses/RepeatNoteElementsBase.cs` |
| 2 | `…/NoteElements Subclasses/RepeatHeadElementsBase.cs` |

Files the fork does **not** touch, despite gameplay-adjacent features: all of `Assets/UI` (default theme +
UXML/USS/Lua), `Assets/Sprites`, `Assets/Sfx`, `Assets/Prefabs`, `Assets/Editor`, `link.xml`, the FMOD
Unity integration, and all track/chart content. **No tracks, charts, or note skins were added or edited** —
this is a code-and-tooling fork, not a content fork.
