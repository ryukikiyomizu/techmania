# T2 Profile Editor Arcade Hybrid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the generic profile form with the approved T2 arcade-hybrid card editor while preserving active-card session, catalog, save, cancel, and USB behavior.

**Architecture:** Keep `Profile Screen.txt` as the behavior owner and preserve every element name it queries. Restructure only the profile-editor branch of `MainTree.uxml`, implement its visual hierarchy in `MainStyle.uss`, and protect the contract with a focused Unity Editor verifier before building and runtime testing.

**Tech Stack:** Unity UI Toolkit UXML/USS, MoonSharp Lua UI scripts, Unity Editor batch verification, Unity AssetBundles, Windows test runtime.

## Global Constraints

- Fixed arcade viewport: 1280x720 with no clipping.
- Reuse authentic T2 LoginState/VCE assets already present in the theme.
- Preserve existing profile and cosmetic-catalog selectors and session routing.
- Build `Assets/AssetBundles/default`, then deploy it as `_TestBuild3/Themes/Technika 2.tmtheme`.
- The workspace is not a Git repository, so commit steps are recorded as unavailable rather than simulated.

---

### Task 1: Lock the Arcade-Hybrid UI Contract

**Files:**
- Create: `Assets/Scripts/Editor/T2ProfileArcadeHybridVerification.cs`
- Inspect: `Assets/UI/MainTree.uxml`
- Inspect: `Assets/UI/MainStyle.uss`

**Interfaces:**
- Consumes: UXML element names queried by `Assets/UI/Scripts/Profile Screen.txt`.
- Produces: `T2ProfileArcadeHybridVerification.Run()` batch entry point.

- [ ] **Step 1: Write the failing verifier**

Create a verifier that loads `MainTree.uxml` and `MainStyle.uss`, then requires the approved structural classes `profile-identity-hero`, `profile-cosmetic-grid`, `profile-cosmetic-tile`, `profile-identity-strip`, and `profile-card-state`, all six catalog category controls, the existing preview names, and fixed 1280x720-safe bounds.

- [ ] **Step 2: Run the verifier and observe the expected failure**

Run Unity in batch mode with `-executeMethod T2ProfileArcadeHybridVerification.Run` and a dedicated log. Expected result: failure identifying the first missing arcade-hybrid class.

- [ ] **Step 3: Keep the red log as evidence**

Record the failing class and command in the task log; do not change the verifier to match the old layout.

---

### Task 2: Implement the Card-Hero Composition

**Files:**
- Modify: `Assets/UI/MainTree.uxml` profile-editor branch
- Modify: `Assets/UI/MainStyle.uss` profile-editor rules
- Test: `Assets/Scripts/Editor/T2ProfileArcadeHybridVerification.cs`

**Interfaces:**
- Consumes: existing names `dj-info`, `preview`, `crew-plate`, `crew-pattern`, `dj-icon`, `dj-name`, `dj-plate`, `dj-pattern`, `dj-message`, `level-icon`, `settings`, `crew-settings`, `dj-settings`, `no-crew-toggle`, `no-card-toggle`, `crew-name-field`, `dj-name-field`, `dj-message-field`, `dj-level-field`, `browse-button`, `value`, `cancel-button`, and `start-button`.
- Produces: the approved unified customization stage and unchanged query contract.

- [ ] **Step 1: Restructure the UXML**

Move the live preview into a left-side `profile-identity-hero`, add a card-state/status band, arrange the five cosmetic category controls plus catalog affordance as large `profile-cosmetic-tile` elements in a 3-by-2 grid, and move editable identity fields into `profile-identity-strip`.

- [ ] **Step 2: Style the hierarchy in USS**

Apply T2 silver/cyan layered panels, beveled stable touch targets, warm selected accents, compact outlined metadata, disabled crew dimming, and fixed geometry. Keep poster and crest as supporting background elements.

- [ ] **Step 3: Run the arcade-hybrid verifier to green**

Run `T2ProfileArcadeHybridVerification.Run`. Expected result: `[T2 Profile UI Test] Arcade hybrid profile contract passed.` and batch exit code 0.

- [ ] **Step 4: Run existing focused regressions**

Run `T2AuthenticCosmeticCatalogVerification`, `T2ThemeOptionSnapshotTests`, `T2ProfileSessionBehaviorTests`, and `T2LoginVceUsbVerification`. Expected result: all batch invocations exit 0 without Lua/selector contract failures.

---

### Task 3: Build, Deploy, and Verify the Runtime

**Files:**
- Build output: `Assets/AssetBundles/default`
- Deploy: `C:/Users/Jen/Downloads/Technika Projects/_TestBuild3/Themes/Technika 2.tmtheme`
- Inspect: `C:/Users/Jen/AppData/LocalLow/TECHMANIA Team/TECHMANIA/Player.log`

**Interfaces:**
- Consumes: green UI and session regression suite.
- Produces: deployed theme with matching source/deployed SHA-256 and runtime evidence.

- [ ] **Step 1: Build the theme bundle**

Run Unity batch mode with `-executeMethod BuildAssetBundleWindow.BuildForDefaultPlatform`. Require the final build marker and exit code 0.

- [ ] **Step 2: Deploy the fresh artifact**

Copy `Assets/AssetBundles/default` to `_TestBuild3/Themes/Technika 2.tmtheme`, replacing only that theme file, then compare SHA-256 hashes.

- [ ] **Step 3: Launch a fresh test runtime**

Stop only the workflow-owned test instance, launch `_TestBuild3/TECHMANIA.exe`, and wait for a responsive process.

- [ ] **Step 4: Exercise the complete route**

Open the login/profile route at 1280x720; verify the editor opens, all category tiles open the catalog, selected items preview, cancel restores, save uses the active card session, back exits, and no controls clip or move under the pointer.

- [ ] **Step 5: Inspect runtime evidence**

Read the fresh `Player.log` tail and require the profile-open marker with no new MoonSharp, Lua, UI Toolkit, null-reference, or catalog errors.

- [ ] **Step 6: Refresh Graphify**

Run `graphify update .`; report success or the exact timeout/error without treating graph refresh as runtime verification.
