# Full Arcade Login Choreography Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reproduce the complete DMT2 LoginState card choreography while using TECHMANIA's asynchronous USB token as the physical card.

**Architecture:** Keep the extracted VCE manifest and independent Lua actors as the presentation layer. Extend `ProfileManager` with a single-flight background presence check, expose it through `ThemeProfileApi`, and make `Login VCE Flow.txt` an explicit clip-duration-driven state machine. Generated asset tooling owns the authentic LoginState sound copies and manifest metadata.

**Tech Stack:** Unity 6000.3.9f1, C#, UI Toolkit UXML, MoonSharp Lua, Python/Pillow VCE generation, Windows USB storage APIs.

## Global Constraints

- `C:\Users\Jen\Downloads\Technika Projects\DMT2\ReverseEngineering\login_reference_extract\LoginState` and the byte-identical `t2\resource\LoginState` tree are the visual, animation, hitbox, and sound authority.
- Keep USB drive I/O off Unity's main thread and allow only one scan task at a time.
- Never display or log USB token values.
- Do not add generic Unity buttons, text fields, panels, or an emulated remote membership service.
- Preserve the separate `BuildResources\Default.tmtheme` and deployed `Default.tmtheme` below 90 MiB.
- After a successful theme build, deploy `Assets\AssetBundles\default` as `_TestBuild3\Themes\Technika 2.tmtheme`.
- The project has no `.git` directory; replace commit steps with recorded test/build checkpoints.

---

### Task 1: Add failing arcade choreography contracts

**Files:**
- Modify: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: generated `loginVceManifest`, Lua source text, `ProfileManager`, and `ThemeProfileApi`.
- Produces: regression checks for exact state order, clip-driven timing, audio mapping, background reset, token-presence polling, and cancellation recovery.

- [ ] **Step 1: Extend the source contracts**

Add checks requiring these exact production concepts:

```csharp
loginLua.Contains("BeginCardAuthentication = function()")
loginLua.Contains("SetState(\"login\")")
loginLua.Contains("SetState(\"identify1\")")
loginLua.Contains("SetState(\"identify2\")")
loginLua.Contains("ClipSeconds = function(name)")
loginLua.Contains("loginVceManifest[name].maxFrame / loginVceManifest[name].fps")
loginLua.Contains("PlayLoginSfx = function(name)")
loginLua.Contains("Assets/UI/SFX/Login/keypad.ogg")
loginLua.Contains("Assets/UI/SFX/Login/ok.ogg")
loginLua.Contains("Assets/UI/SFX/Login/back.ogg")
loginLua.Contains("element.time = 0")
profileManager.Contains("pollPendingTokenPresence()")
themeApi.Contains("pollPendingTokenPresence()")
```

Require the ordered known and unknown path markers `login -> loading -> identify1 -> identify2` and `login -> loading -> join -> nameKeypad -> joinok -> identify1 -> identify2`. Reject fixed transition waits `Wait(0.45)`, `Schedule(1.15`, and `Schedule(1.35`.

- [ ] **Step 2: Run the contract and preserve the red result**

Run:

```powershell
$env:ALLUSERSPROFILE='C:\ProgramData'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath (Get-Location).Path -executeMethod T2LoginVceUsbVerification.Run -logFile t2-full-arcade-login-red.log
```

Expected: failure names the missing full arcade choreography rather than a compile or Lua syntax error.

### Task 2: Package authentic LoginState sounds and timing metadata

**Files:**
- Modify: `tools/generate_login_vce_assets.py`
- Generate: `Assets/UI/Scripts/Login VCE Manifest.txt`
- Create: `Assets/UI/SFX/Login/back.ogg`
- Create: `Assets/UI/SFX/Login/keypad.ogg`
- Create: `Assets/UI/SFX/Login/ok.ogg`
- Create: `Assets/UI/SFX/Login/quit.ogg`
- Create: `Assets/UI/SFX/Login/warning.ogg`

**Interfaces:**
- Consumes: authentic OGG files under `t2/resource/LoginState` and `t2/resource/LoginState/keyboard`.
- Produces: `loginVceAudio` Lua paths and existing `fps`/`maxFrame` clip metadata used by the runtime state machine.

- [ ] **Step 1: Add deterministic audio export**

Add an `AUDIO_FILES` mapping from logical names to source-relative paths and copy each file into `Assets/UI/SFX/Login` with `shutil.copyfile`. Emit:

```lua
loginVceAudio = {
    click = "Assets/UI/SFX/Login/group_click.ogg",
    keypad = "Assets/UI/SFX/Login/keypad.ogg",
    confirm = "Assets/UI/SFX/Login/ok.ogg",
    back = "Assets/UI/SFX/Login/back.ogg",
    quit = "Assets/UI/SFX/Login/quit.ogg",
    warning = "Assets/UI/SFX/Login/warning.ogg"
}
```

- [ ] **Step 2: Regenerate and verify byte identity**

Run `python tools\generate_login_vce_assets.py`, then compare SHA-256 hashes for each generated OGG against its source. Expected: all pairs match and the manifest still exposes 38 keypad targets.

### Task 3: Add asynchronous presented-card removal detection

**Files:**
- Modify: `Assets/Scripts/ProfileManager.cs`
- Modify: `Assets/Scripts/Theme API/ThemeProfileApi.cs`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: `private static Task<string> usbScanTask`, `FindUsbToken()`, and `pendingCardId`.
- Produces: `public static bool pollPendingTokenPresence()` and Lua-facing `public bool pollPendingTokenPresence()`.

- [ ] **Step 1: Add a failing presence contract**

Require a private `FindPresentedToken(string expectedToken)` worker, a private `usbScanExpectedToken`, and public polling wrappers. Run `T2LoginVceUsbVerification.Run`; expected: failure reports missing removal detection.

- [ ] **Step 2: Implement single-flight verification**

Change `StartUsbScan` to accept an optional expected token. Normal discovery retains `Task.Run(FindUsbToken)`. Presence verification uses `Task.Run(() => FindPresentedToken(expectedToken))`, where `FindPresentedToken` enumerates existing token files without provisioning media and returns the expected token only when it remains mounted.

When `CompleteUsbScanIfReady` completes a verification task, clear `pendingCardId` only if the expected token is absent. `pollPendingTokenPresence()` returns false after that completed absence result and never performs drive I/O directly.

- [ ] **Step 3: Expose and verify the API**

Add the `ThemeProfileApi` wrapper, compile, and run the contract. Expected: the presence API contract passes and existing asynchronous scan contracts remain green.

### Task 4: Implement the full clip-driven arcade state machine

**Files:**
- Modify: `Assets/UI/Scripts/Login VCE Flow.txt`
- Modify only if a missing authored hitbox is proven: `Assets/UI/MainTree.uxml`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: `loginVceManifest[name].fps`, `maxFrame`, `loginVceAudio`, and `tm.profile.pollPendingTokenPresence()`.
- Produces: explicit states `main`, `login`, `loading`, `join`, `nameKeypad`, `joinok`, `identify1`, `identify2`, `error`, and `transfer`.

- [ ] **Step 1: Add timing and sound helpers**

Implement `ClipSeconds(name)` as `maxFrame / fps`, `ScheduleClip(name, action)`, and `PlayLoginSfx(name)` using `themeAudio.PlaySfx(tm.io.LoadAudioFromTheme(loginVceAudio[name]))`. Missing audio returns without aborting the state.

- [ ] **Step 2: Add card authentication choreography**

Implement `BeginCardAuthentication()` so both idle USB detection and MEMBER LOGIN use:

```text
login (inputcard_login, one shot)
loading (inputcard_loading for its authored duration)
known -> identify1 -> identify2 -> proceed
unknown -> join
```

Each automatic transition uses `ScheduleClip`; each state checks `currentState` before continuing. `joinok` flows through `identify1` and `identify2`, not directly to proceed.

- [ ] **Step 3: Map authentic sound feedback**

Play `click` for main buttons, `keypad` for character presses, `back` for deletion/BACK, `confirm` for NEXT and successful join confirmation, `quit` for keypad X/cancel, and `warning` on the error state. Never loop a button or sound effect.

- [ ] **Step 4: Handle removal and suppression**

While in `login`, `loading`, `join`, `nameKeypad`, `joinok`, `identify1`, or `identify2`, poll `tm.profile.pollPendingTokenPresence()`. A completed missing-card result enters `error`. BACK/X/Escape sets suppression; `main` clears suppression after the token is physically absent, while MEMBER LOGIN explicitly clears it and starts scanning.

- [ ] **Step 5: Reset media and eliminate arbitrary timing**

Set the loaded login video element's `time` to `0` before `Play()`. Remove the fixed authentication/join/identify waits and retain only scan polling cadence and error readability timing. Keep the original 38 target geometry, four-layer pressed-key chrome, stable authored NEXT art, and existing case-insensitive duplicate-name retry.

- [ ] **Step 6: Run Lua and source contracts**

Run `T2LoginVceUsbVerification.Run`. Expected: Lua parses and all LoginState, keypad, full-state-order, audio, USB, and recovery contracts pass.

### Task 5: Build, deploy, and runtime-verify

**Files:**
- Build: `Assets/AssetBundles/default`
- Deploy: `_TestBuild3/Themes/Technika 2.tmtheme`
- Build: `_TestBuild3/TECHMANIA.exe` and `_TestBuild3/TECHMANIA_Data`
- Update: `graphify-out/graph.json`

**Interfaces:**
- Consumes: all prior task outputs.
- Produces: a fresh arcade-testable MXGG player and deployed Technika 2 theme.

- [ ] **Step 1: Run final editor contracts**

Run `T2LoginVceUsbVerification.Run` and `T2ThemePackageVerification.Run`. Expected: both explicit pass markers and zero compile errors.

- [ ] **Step 2: Run attached-USB responsiveness verification when hardware is mounted**

Run `T2LoginVceUsbVerification.RunWithUsb`. Expected with a mounted USB: asynchronous detection within 10 seconds and every Unity-thread poll under 100 ms. If Windows exposes no USB drive, report this case as unverified rather than passing it by assumption.

- [ ] **Step 3: Build player and theme**

Run `HumanPlaytesterBuild.BuildTestPlayer`, then `BuildAssetBundleWindow.BuildForDefaultPlatform`. Expected: player build succeeds with zero errors; theme build returns code 0 and logs the copy to `_TestBuild3\Themes\Technika 2.tmtheme`.

- [ ] **Step 4: Smoke-test the rebuilt executable**

Launch the freshly built `_TestBuild3\TECHMANIA.exe` with a dedicated log, wait for the asynchronous USB initialization and LoginState script markers, then stop only that verification-owned process. Expected: no startup exception and no token value in the log.

- [ ] **Step 5: Verify artifacts and refresh the graph**

Require matching SHA-256 for `Assets/AssetBundles/default` and deployed `Technika 2.tmtheme`, matching SHA-256 for `BuildResources/Default.tmtheme` and deployed `Default.tmtheme`, and Default size below 90 MiB. Run `graphify update .` and record final timestamps, sizes, hashes, and verification markers.
