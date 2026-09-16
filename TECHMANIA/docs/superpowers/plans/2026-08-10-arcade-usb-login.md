# Arcade-Accurate USB Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an authentic Technika 2 animated login screen that uses a single DJ-name keypad for first-time USB profiles and automatically loads existing USB profiles.

**Architecture:** Generate a richer Lua manifest from the authoritative LoginState VCE files, including per-key blend classes and keypad hit targets. Replace the single-player reconstructed form with independent Lua VCE actors for persistent, state, button, and keypad timelines, while leaving `ProfileManager` and `ThemeProfileApi` as the USB persistence boundary.

**Tech Stack:** Unity 6000.3.9f1, UI Toolkit UXML, MoonSharp Lua, Python 3/Pillow, original Technika 2 VCE assets, C# Editor verification.

## Global Constraints

- Visual authority: `C:\Users\Jen\Downloads\Technika Projects\t2\resource\LoginState`.
- Runtime resolution and coordinates: 1280x720.
- First-time USB input: one DJ name only; no cosmetic ID/password fields.
- Preserve `ProfileManager` and `ThemeProfileApi` profile storage and pending-token semantics.
- Route `(5,2)` and `(2,2)` through the additive material; route `(5,6)` and `(2,1)` through normal rendering.
- Never print USB tokens or card identifiers in test or build output.
- Generated manifest and VCE sprite cutouts are owned only by `tools/generate_login_vce_assets.py`.
- This directory is not a Git repository, so task gates use saved artifacts and command output instead of commits.
- After a successful AssetBundle build, deploy `Assets\AssetBundles\default` as `C:\Users\Jen\Downloads\Technika Projects\_TestBuild3\Themes\Technika 2.tmtheme` and verify matching SHA-256 hashes.

---

### Task 1: Lock the arcade-login contracts with failing verification

**Files:**
- Modify: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: generated Lua text, UXML text, `ProfileManager` and `ThemeProfileApi` source.
- Produces: `T2LoginVceUsbVerification.Run()` and `Evaluate()` contracts for every later task.

- [ ] **Step 1: Replace the old clip and hierarchy expectations**

Define the authoritative clip families and new required hierarchy names:

```csharp
private static readonly string[] SourceVces =
{
    "inputcard_head", "inputcard_panel", "banner_image", "inputcard_main",
    "inputcard_login", "inputcard_loading", "inputcard_error",
    "inputcard_identify1", "inputcard_identify2", "inputcard_join",
    "inputcard_joinok", "inputcard_transfer", "inputcard_transfering",
    "button_back", "button_guest", "button_idlogin", "button_join",
    "button_joinnow", "button_memberlogin", "button_startgame",
    "button_transfer", "keypad", "key", "cursor", "keypad_join",
    "keypad_next"
};

private static readonly string[] ActorRoots =
{
    "login-vce-head", "login-vce-panel", "login-vce-banner", "login-vce-state",
    "login-vce-button-1", "login-vce-button-2", "login-vce-button-3",
    "login-vce-keypad", "login-vce-key-feedback", "login-vce-cursor"
};
```

Add checks for `blendClass`, `hitTargets = {`, exactly 38 emitted keypad targets, the arcade character map, `CreateActor`, `nameKeypad`, and the absence of `profile-name-input`/`ui:TextField` inside the login hierarchy.

- [ ] **Step 2: Run the verifier and confirm the new checks fail**

Run:

```powershell
$env:ALLUSERSPROFILE='C:\ProgramData'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\Jen\Downloads\Technika Projects\Techmania MXGG build 2 source\Techmania source\TECHMANIA' -executeMethod T2LoginVceUsbVerification.Run -logFile 'C:\Users\Jen\Downloads\Technika Projects\Techmania MXGG build 2 source\Techmania source\TECHMANIA\t2-login-red.log'
```

Expected: non-zero exit and contract failures naming missing clips, actor roots, keypad targets, or generic TextField removal.

- [ ] **Step 3: Save the red-test evidence**

Run:

```powershell
Select-String -Path '.\t2-login-red.log' -Pattern 'T2 Login Test|InvalidOperationException|missing|must' | Select-Object -Last 40
```

Expected: at least one new arcade-login contract is visibly failing for the intended reason.

### Task 2: Generate complete VCE timelines, blend classes, and keypad metadata

**Files:**
- Modify: `tools/generate_login_vce_assets.py`
- Generate: `Assets/UI/Scripts/Login VCE Manifest.txt`
- Generate: `Assets/UI/Sprites/Login/T2/VCE/*`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: `vce_parse.parse_vce(path)` dictionaries and LoginState textures.
- Produces: `loginVceManifest[clip]`, key tuple `{frame,left,top,width,height,opacity,r,g,b,rotation,textureIndex,blendClass}`, and `loginKeypad.hitTargets`.

- [ ] **Step 1: Expand the generator's clip authority**

Replace `VCE_NAMES` with the full `SourceVces` set from Task 1. Add an explicit relative-path map for `banner/banner_image.vce` and every `keyboard/*.vce` clip. Make `source_file()` search recursively with `SOURCE.rglob("*")` so keyboard textures resolve without flattening or renaming the authoritative source tree.

- [ ] **Step 2: Encode blend class per keyframe**

Add this function and append its result in `key_to_lua`:

```python
def blend_class(key: dict) -> int:
    pair = tuple(key["blend"])
    if pair in ((5, 2), (2, 2)):
        return 1
    if pair in ((5, 6), (2, 1)):
        return 0
    raise ValueError(f"Unsupported LoginState blend pair: {pair}")
```

The manifest header must state that the twelfth tuple field is `blendClass`. Remove layer-level `additive = true/false` because the sampled key owns this decision.

- [ ] **Step 3: Preserve keypad dummy layers as hit targets**

When processing `keypad.vce`, collect the 38 printable-key dummy layers in source order and emit:

```lua
loginKeypad = {
    characters = "1234567890QWERTYUIOPASDFGHJKL  ZXCVBNM",
    hitTargets = {
        {106.5,318.5,109,91,1},
        {213.5,318.5,109,91,2},
        {319.5,318.5,109,91,3},
        {426.5,318.5,109,91,4},
        {532.5,318.5,109,91,5},
        {639.5,318.5,109,91,6},
        {745.5,318.5,109,91,7},
        {852.5,318.5,109,91,8},
        {958.5,318.5,109,91,9},
        {1065.5,318.5,109,91,10},
        {106.5,407.5,109,91,11},
        {213.5,407.5,109,91,12},
        {319.5,407.5,109,91,13},
        {426.5,407.5,109,91,14},
        {532.5,407.5,109,91,15},
        {639.5,407.5,109,91,16},
        {745.5,407.5,109,91,17},
        {852.5,407.5,109,91,18},
        {958.5,407.5,109,91,19},
        {1065.5,407.5,109,91,20},
        {106.5,496.5,109,91,21},
        {213.5,496.5,109,91,22},
        {319.5,496.5,109,91,23},
        {426.5,496.5,109,91,24},
        {532.5,496.5,109,91,25},
        {639.5,496.5,109,91,26},
        {745.5,496.5,109,91,27},
        {852.5,496.5,109,91,28},
        {958.5,496.5,109,91,29},
        {1065.5,496.5,109,91,30},
        {106.5,585.5,109,91,31},
        {213.5,585.5,109,91,32},
        {319.5,585.5,109,91,33},
        {426.5,585.5,109,91,34},
        {532.5,585.5,109,91,35},
        {639.5,585.5,109,91,36},
        {745.5,585.5,109,91,37},
        {852.5,585.5,109,91,38},
    }
}
```

The fifth field is the one-based character-map index. The generator must assert `len(hit_targets) == 38` and must not export `dummy.png` as a render texture.

- [ ] **Step 4: Regenerate assets and inspect the manifest**

Run:

```powershell
python '.\tools\generate_login_vce_assets.py'
rg -n "inputcard_head|inputcard_main|button_startgame|keypad_next|blendClass|hitTargets|characters" '.\Assets\UI\Scripts\Login VCE Manifest.txt'
```

Expected: every named timeline, a twelfth tuple field, the character map, and 38 hit-target records are present.

- [ ] **Step 5: Run the verifier to isolate remaining runtime/UI failures**

Run the Task 1 Unity command with log file `t2-login-generator.log`.

Expected: generator/manifest contracts pass; actor, UXML, and controller contracts still fail.

### Task 3: Build independent VCE actors and authentic login hierarchy

**Files:**
- Modify: `Assets/UI/MainTree.uxml`
- Modify: `Assets/UI/Scripts/Login VCE Flow.txt`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: `loginVceManifest` key tuples and theme additive material.
- Produces: `CreateActor(rootName, layerCount)`, actor methods `Play(name, loop)`, `Stop()`, `Apply(element, layer, sample)`, and named UXML actor roots.

- [ ] **Step 1: Replace the reconstructed login form hierarchy**

Remove `profile-name-input`, `profile-name-confirm`, `profile-name-cancel`, and the hand-positioned reconstructed login panel. Add actor roots for head, panel, banner, state, three simultaneous buttons, keypad, key feedback, and cursor. Each actor root contains paired normal/additive child elements named `<root>-normal-1` and `<root>-additive-1`. Use these verified render capacities: head 4, panel 5, banner 1, state 11, each button 3, keypad 7, key feedback 5, and cursor 1.

Keep only semantic overlays that VCE cannot render: `login-dj-name`, `login-status-copy`, and transparent touch regions. These overlays use absolute 1280x720 coordinates and do not replace source art.

- [ ] **Step 2: Replace the singleton player with an actor factory**

Implement the factory shape:

```lua
CreateActor = function(rootName, layerCount)
    local actor = { rootName = rootName, normal = {}, additive = {}, active = nil, coroutine = nil }
    actor.Initialize = function()
        for i = 1, layerCount do
            actor.normal[i] = loginScreen.screen.Q(rootName .. "-normal-" .. i)
            actor.additive[i] = loginScreen.screen.Q(rootName .. "-additive-" .. i)
        end
    end
    actor.Stop = function()
        if (actor.coroutine != nil) then tm.StopCoroutine(actor.coroutine) end
        actor.coroutine = nil
        actor.active = nil
        actor.HideAll()
    end
    return actor
end
```

Sampling linearly interpolates numeric fields 2-10, carries `textureIndex` from the preceding key, and carries `blendClass` from the preceding key. `blendClass == 1` selects the additive child; otherwise it selects the normal child.

- [ ] **Step 3: Compose persistent and state actors concurrently**

Initialize:

```lua
loginActors = {
    head = CreateActor("login-vce-head", 4),
    panel = CreateActor("login-vce-panel", 5),
    banner = CreateActor("login-vce-banner", 1),
    state = CreateActor("login-vce-state", 11),
    button1 = CreateActor("login-vce-button-1", 3),
    button2 = CreateActor("login-vce-button-2", 3),
    button3 = CreateActor("login-vce-button-3", 3),
    keypad = CreateActor("login-vce-keypad", 7),
    keyFeedback = CreateActor("login-vce-key-feedback", 5),
    cursor = CreateActor("login-vce-cursor", 1)
}
```

Play `inputcard_head`, `inputcard_panel`, and `banner_image` persistently. `SetState` stops only state/button/keypad actors, then starts the correct concurrent timelines for the new state.

- [ ] **Step 4: Run the verifier**

Run the Task 1 Unity command with log file `t2-login-actors.log`.

Expected: hierarchy, actor, Lua parse, and blend-routing checks pass; incomplete state/keypad checks may remain.

### Task 4: Implement the single-name arcade keypad and USB state machine

**Files:**
- Modify: `Assets/UI/Scripts/Login VCE Flow.txt`
- Modify: `Assets/UI/MainTree.uxml`
- Test: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`

**Interfaces:**
- Consumes: `loginKeypad.characters`, `loginKeypad.hitTargets`, `tm.profile.rescanForToken()`, `tm.profile.loginWithPendingToken()`, and `tm.profile.createProfileWithPendingToken(name)`.
- Produces: `loginKeypadController.Append(index)`, `Backspace()`, `Confirm()`, `Reset()`, and the states `main`, `loading`, `join`, `nameKeypad`, `joinok`, `identify`, `error`.

- [ ] **Step 1: Register all 38 authoritative touch targets**

Create transparent absolute-positioned UXML elements `login-key-target-1` through `login-key-target-38`. During initialization, apply each generated target's `{left,top,width,height}` and register a click callback that calls `loginKeypadController.Append(index)`.

- [ ] **Step 2: Implement bounded DJ-name editing**

Use this controller contract:

```lua
loginKeypadController = {
    value = "",
    maxLength = 12,
    Reset = function()
        loginKeypadController.value = ""
        loginFlow.screen.Q("login-dj-name").text = ""
    end,
    Append = function(index)
        local ch = string.sub(loginKeypad.characters, index, index)
        if (ch == " " or string.len(loginKeypadController.value) >= loginKeypadController.maxLength) then return end
        loginKeypadController.value = loginKeypadController.value .. ch
        loginFlow.screen.Q("login-dj-name").text = loginKeypadController.value
        loginActors.keyFeedback.PlayAt("key", loginKeypad.hitTargets[index])
    end
}
```

`Backspace()` removes one byte from the ASCII character set. `Confirm()` trims the value, refuses empty input, then calls `createProfileWithPendingToken(name)` exactly once.

- [ ] **Step 3: Implement the approved USB flow**

Map controls and automatic transitions exactly:

```lua
-- existing token
main -> loading -> identify -> ProceedOnce

-- unknown token
main -> join -> nameKeypad -> joinok -> identify -> ProceedOnce

-- cancellation or failure
join/nameKeypad -> main
loading/nameKeypad -> error -> main
```

While `main` is active, poll `rescanForToken()`. A known token calls `loginWithPendingToken()`. An unknown pending token displays JOIN; it is bound only in `Confirm()`. Guest calls `ProceedOnce()` without creating a profile.

- [ ] **Step 4: Bind authentic button timelines and touch regions**

Main starts `button_memberlogin`, `button_guest`, and `button_joinnow` in separate actors. Join/keypad states use `button_back`, `button_join`, `keypad_join`, and `keypad_next` as appropriate. Touch regions follow VCE button dummy anchors, and disabled confirmation ignores clicks without substituting a generic grey button.

- [ ] **Step 5: Run the full login verifier**

Run the Task 1 Unity command with log file `t2-login-green.log`.

Expected: exit code 0 and `[T2 Login Test] Authentic LoginState VCE/USB contracts passed.`

### Task 5: Build, deploy, and verify the actual theme artifact

**Files:**
- Generate: `Assets/AssetBundles/default`
- Deploy: `C:/Users/Jen/Downloads/Technika Projects/_TestBuild3/Themes/Technika 2.tmtheme`
- Evidence: `t2-login-build.log`

**Interfaces:**
- Consumes: verified Unity project and generated login assets.
- Produces: a deployed Technika 2 theme whose bytes match the freshly built default bundle.

- [ ] **Step 1: Run verification immediately before building**

Run the Task 1 Unity command with log file `t2-login-prebuild.log`.

Expected: exit code 0 with the authentic LoginState VCE/USB contract marker.

- [ ] **Step 2: Build the AssetBundle in batch mode**

Run:

```powershell
$env:ALLUSERSPROFILE='C:\ProgramData'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\Jen\Downloads\Technika Projects\Techmania MXGG build 2 source\Techmania source\TECHMANIA' -executeMethod BuildAssetBundleWindow.BuildForDefaultPlatform -logFile 'C:\Users\Jen\Downloads\Technika Projects\Techmania MXGG build 2 source\Techmania source\TECHMANIA\t2-login-build.log'
```

Expected: exit code 0 and the project's successful bundle/deployment log markers.

- [ ] **Step 3: Verify source and deployed hashes**

Run:

```powershell
$source = 'C:\Users\Jen\Downloads\Technika Projects\Techmania MXGG build 2 source\Techmania source\TECHMANIA\Assets\AssetBundles\default'
$deployed = 'C:\Users\Jen\Downloads\Technika Projects\_TestBuild3\Themes\Technika 2.tmtheme'
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
$deployedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $deployed).Hash
if ($sourceHash -ne $deployedHash) { throw "Theme deployment hash mismatch" }
Get-Item -LiteralPath $source,$deployed | Select-Object FullName,Length,LastWriteTime
"SHA256=$sourceHash"
```

Expected: identical lengths, identical SHA-256 values, and a fresh deployed timestamp.

- [ ] **Step 4: Refresh the project graph**

Run:

```powershell
graphify update .
```

Expected: graph update completes without invalidating the successful verification/build evidence.
