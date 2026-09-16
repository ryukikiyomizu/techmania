# Login Runtime Polish and Mode Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the unsupported-mode popup/fallthrough behavior and eliminate neighboring-key fragments from the authentic LoginState keypad glyphs.

**Architecture:** Keep Star and Pop as the complete arcade modes. Make the header an explicit input boundary so unsupported labels are silent, while the main playfield retains touch-to-confirm. Sanitize only the unused right edge of each generated 80 x 64 keypad glyph cell, leaving VCE geometry and feedback animation untouched.

**Tech Stack:** Unity 6000.3.9f1, UI Toolkit UXML, MoonSharp Lua, Python/Pillow asset generator, Unity Editor verification.

## Global Constraints

- Preserve USB scanning, profile creation, LoginState VCE timing, and authored touch geometry.
- Preserve Star and Pop mode selection and confirmation.
- Unsupported Duo, Club, and Crew header touches must be silent.
- Rebuild and deploy `Assets/AssetBundles/default` as `_TestBuild3/Themes/Technika 2.tmtheme`.

---

### Task 1: Mode-select input boundary

**Files:**
- Modify: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`
- Modify: `Assets/UI/Scripts/Select Mode Screen.txt`
- Modify: `Assets/UI/MainTree.uxml`

**Interfaces:**
- Consumes: `selectModeScreen.SelectMode(modeName)`, `selectModeScreen.ConfirmMode()`
- Produces: Star/Pop-only `modeOrder` and a header-band early return before screen-wide confirmation.

- [ ] **Step 1: Write the failing contract**

Require `modeOrder = {"star", "pop"}`, reject `ShowClubUnavailable`, `club-unavailable-popup`, and `MODE NOT AVAILABLE`, and require the root click handler to return for `y <= 78`.

- [ ] **Step 2: Run the focused Unity verifier RED**

Run `T2LoginVceUsbVerification.Run`. Expected: failure naming the unsupported-mode popup/input contract.

- [ ] **Step 3: Implement the minimal mode input change**

Change the cycle order to Star/Pop, remove popup lifecycle and Club special cases, make the Club hitbox stop propagation without action, and add this root-handler guard:

```lua
local x, y = touchTrail.GetEventPosition(event)
if (y >= 0 and y <= 78) then return end
```

Delete `club-unavailable-popup` from UXML.

- [ ] **Step 4: Run the focused Unity verifier GREEN**

Run `T2LoginVceUsbVerification.Run`. Expected: authentic LoginState VCE/USB contracts passed.

### Task 2: Keypad glyph crop integrity

**Files:**
- Modify: `Assets/Scripts/Editor/T2LoginVceUsbVerification.cs`
- Modify: `tools/generate_login_vce_assets.py`
- Regenerate: `Assets/UI/Sprites/Login/T2/VCE/key_text_01.png` through `key_text_38.png`

**Interfaces:**
- Consumes: `export_keypad_glyphs()` and the source `LoginState/keyboard/key_text.png` atlas.
- Produces: 80 x 64 RGBA glyph PNGs with transparent columns 64 through 79.

- [ ] **Step 1: Write the failing glyph-edge contract**

Load every generated glyph PNG in the Unity verifier and fail if any pixel at `x >= 64` has nonzero alpha.

- [ ] **Step 2: Run the focused Unity verifier RED**

Run `T2LoginVceUsbVerification.Run`. Expected: Q and N glyph crops report occupied right-edge pixels.

- [ ] **Step 3: Implement the minimal generator correction**

After each 80 x 64 atlas crop, clear only the spill zone:

```python
glyph = image.crop((left, top, min(left + 80, image.width), top + 64)).convert("RGBA")
glyph.paste((0, 0, 0, 0), (64, 0, glyph.width, glyph.height))
glyph.save(output)
```

Run `python tools/generate_login_vce_assets.py` to regenerate the authentic derived assets and manifest.

- [ ] **Step 4: Run the focused Unity verifier GREEN**

Run `T2LoginVceUsbVerification.Run`. Expected: all keypad edge checks and existing USB contracts pass.

### Task 3: Package and deployment verification

**Files:**
- Generated: `Assets/AssetBundles/default`
- Deploy: `_TestBuild3/Themes/Technika 2.tmtheme`

**Interfaces:**
- Consumes: corrected UXML, Lua, and glyph assets.
- Produces: a freshly deployed test theme with a matching SHA-256 hash.

- [ ] **Step 1: Build the theme**

Run Unity with `BuildAssetBundleWindow.BuildForDefaultPlatform` and require return code 0 plus `Built theme at Assets/AssetBundles/default.`

- [ ] **Step 2: Run package verification**

Run `T2ThemePackageVerification.Run` and require the compact-bundle/tab contracts to pass.

- [ ] **Step 3: Verify deployed artifact identity**

Compute SHA-256 for `Assets/AssetBundles/default` and `_TestBuild3/Themes/Technika 2.tmtheme`; require exact equality.

- [ ] **Step 4: Refresh the project graph**

Run `graphify update .` and retain the updated graph artifacts.
