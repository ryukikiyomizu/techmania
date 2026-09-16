# Chart-Aware Human Playtester Design

Date: 2026-07-18
Status: Approved concept, pending implementation plan
Target: TECHMANIA Windows test build deployed only to `_TestBuild3`

## Goal

Add a temporary, Settings-controlled automated player that exercises the normal gameplay, judgement, score, result, new-record, and profile-record paths while behaving like a capable but imperfect human. Its performance must derive from the chart's actual note placement and patterns rather than the displayed difficulty number.

## Boundaries

- The feature is available only in the dedicated `_TestBuild3` player build.
- The regular project build keeps existing gameplay behavior.
- The option is explicitly named `HUMAN PLAYTESTER` in Settings.
- Existing `AutoPlay` remains unchanged and continues to be invalid for records.
- The human playtester keeps the gameplay modifier in `Normal`, uses the normal scanline, and does not display the existing autoplay indicator.
- Scores are generated through the normal judgement and score pipeline, not written or fabricated directly.
- Records are valid and save only when the ordinary `ScoreIsValid()` conditions also pass.
- The playtester is tuned to survive and reach Results so result-screen and new-record behavior can be tested.

## Recommended Architecture

### 1. ChartDifficultyAnalyzer

Run once after the playable `Pattern` has been unpacked and note times have been calculated. Analyze every playable note and produce a per-note pressure value plus contiguous choke segments.

Features:

- onset density in short, medium, and scan-sized sliding windows;
- simultaneous notes and near-simultaneous chords;
- lane travel distance and rapid left-to-right/right-to-left reversals;
- repeated hits in one lane and fast alternation between lanes;
- overlapping `HoldNote` and `DragNote` obligations;
- drag path movement and node complexity;
- chains, repeats, and stacked note-type transitions;
- sudden density spikes relative to the chart's recent local baseline;
- reduced pressure during recovery gaps;
- BPM changes and time-stop boundaries where timing context changes.

The analyzer must not read or weight the displayed chart level. The level may be logged for diagnostics only.

Outputs:

- normalized overall chart pressure;
- per-note pressure from 0 to 1;
- choke segments with start time, end time, peak pressure, and dominant causes;
- recovery segments following a choke;
- a compact diagnostic summary written to the player log for test review.

### 2. HumanPlayProfile

Create one profile at song start with a new seed for every play. The initial target resembles a strong but imperfect arcade player, informally around the requested level-60 ability.

State:

- base timing accuracy;
- persistent early/late timing bias for the current run;
- slowly changing timing drift;
- current focus, fatigue, and recovery;
- recent error streak and post-error correction;
- hold/drag reliability;
- bounded run-to-run variance.

This state makes outcomes correlated. A choke can cause a short error cluster, followed by recovery, rather than independent random judgement rolls. Easy charts naturally receive mostly high judgements, while mechanically difficult charts create more Cool/Good/Miss/Break outcomes.

### 3. HumanPlayScheduler

Build a plan from the analyzer and profile, then execute it in `GameInputManager` without setting `GameController.autoPlay`.

For each note, select one action:

- hit with a human timing offset;
- hit late or early enough for a lower judgement;
- skip so the normal Break path resolves it;
- start a hold/drag and intentionally release it during a difficult segment;
- maintain the long note through the normal ongoing-note path.

Timing offsets are chosen against the active ruleset's real judgement windows. The scheduler calls the same `HitNote(note, timeDifference)` path used by gameplay. It must never assign score, combo, medal, or judgement totals directly.

Physical input is ignored while the test player is active to prevent double hits. Turning the option off restores untouched normal input behavior on the next song.

## Difficulty and Performance Model

Per-note failure pressure combines chart pressure with temporary human state:

`effectivePressure = patternPressure + fatigue + recentErrorPenalty - recoveryBonus`

The mapping is continuous, not rank-targeted. The implementation must not decide in advance that a chart should receive a particular letter grade. Expected behavior is emergent:

- spacious and simple charts: stable timing, mostly top judgements, occasional believable slips;
- moderate charts: small timing drift and a few lower judgements around denser passages;
- dense or awkward charts: clustered errors at cross-screen jumps, overlaps, repeats, and bursts;
- post-choke gaps: timing and focus recover gradually;
- repeated plays: similar difficulty response but different exact mistakes.

Safety clamps prevent a fully perfect run and prevent excessive failure that would block Results. They bound only the future action-quality distribution, never a target score, rank, or grade; the resulting grade remains an outcome of the normal judgement and scoring pipeline. The clamps do not alter score or health directly.

## Settings and Test-Build Isolation

Add a serialized boolean option for the human playtester and expose it as a toggle in Settings. Compile the setting and runtime integration behind a dedicated test-player scripting define. The Windows build pipeline must produce a separate player for `_TestBuild3`; the normal build must compile with the feature absent or permanently disabled.

The setting label provides the visible disclosure that automation is active. Gameplay itself retains the normal scanline and theme presentation so result and new-record visuals can be tested without the existing autoplay presentation.

## Valid Records

- Do not set `Modifiers.Mode.AutoPlay` or `Practice`.
- Do not bypass, special-case, or force `ScoreIsValid()` to true.
- Do not write `records.json` from the playtester.
- Let `GameController.UpdateRecord()` save the score exactly as it would for physical play.
- Preserve stage-failure and all other existing validity checks.
- Save test-generated records through the currently active Guest profile at `C:\Users\Jen\Documents\TECHMANIA\Profiles\Guest\records.json`. The player currently shares this Documents profile path across builds, so the reset backup is the isolation/recovery boundary for this temporary test.

## Record Reset

The current Guest record store is `C:\Users\Jen\Documents\TECHMANIA\Profiles\Guest\records.json`. Reset only the `records` and `setlistRecords` arrays while preserving the file's current schema version. Do not modify Guest options, stats, profile identity, or emblem data. Create a timestamped backup before resetting so the operation is reversible.

## Diagnostics

At song load, write one concise log entry containing:

- song GUID and title;
- chart fingerprint, ruleset, and pattern name/difficulty label for identification only;
- run seed;
- note count;
- overall pressure;
- identified choke count and top causes;
- planned judgement/action counts.

At Results, log actual judgement totals and score. No extra gameplay overlay is required.

## Failure Handling

- Unsupported or unknown note types fall back to a conservative normal hit plan.
- Empty charts disable the scheduler cleanly.
- A missing analyzer result must fall back to a simple imperfect timing model, never the existing perfect autoplay.
- All scheduled state is discarded on retry, song exit, or scene unload.
- Exiting Results immediately must not leave the playtester or audio state active.

## Verification

1. Build the dedicated Windows player with the test scripting define.
2. Deploy it into `_TestBuild3` without overwriting songs, themes, skins, profiles, or user settings beyond the new option's default.
3. Confirm normal play with the toggle off is unchanged.
4. Confirm the toggle on uses the normal scanline and no autoplay presentation.
5. Run easy, moderate, and mechanically dense charts and compare logged choke sections to observed note patterns.
6. Repeat one chart several times and confirm errors vary but remain concentrated around the same difficult sections.
7. Confirm at least one score saves through `UpdateRecord()` and triggers the existing New Record result path.
8. Confirm a lower replay does not replace the higher record.
9. Confirm score validity still rejects ordinary AutoPlay and Practice.
10. Confirm every test run reaches Results without hangs or unresolved long notes.

## Removal

The temporary feature can be removed by deleting the analyzer/profile/scheduler classes, the Settings field and toggle, and the test build define integration. No theme or record schema migration is required.
