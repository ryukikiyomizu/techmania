# Arcade-Accurate USB Login Design

## Goal

Replace the reconstructed TECHMANIA login form with the original Technika 2 `LoginState` VCE composition and touch behavior while retaining TECHMANIA's local USB-profile backend. A first-time USB asks only for a DJ name; an existing USB signs in automatically.

## Authority and scope

The visual and animation authority is `C:\Users\Jen\Downloads\Technika Projects\t2\resource\LoginState`, cross-checked against `DMT2\ReverseEngineering\analysis\manifests\LoginState.csv` and the arcade client's `Game::CLoginState` resource references. The implementation must use the existing VCE timelines and their referenced textures. Generic Unity form controls are not acceptable replacements for the original keypad, buttons, or panels.

The USB backend remains `ProfileManager` through `ThemeProfileApi`. The design does not emulate a remote Technika membership server, fabricate an ID/password login, or change the stored profile format.

## Screen composition

The login root is a 1280x720 composition with independent actors so simultaneous VCE timelines do not replace each other:

- Persistent actors: `inputcard_head`, `inputcard_panel`, and `banner_image`.
- State actor: one of `inputcard_main`, `inputcard_login`, `inputcard_loading`, `inputcard_error`, `inputcard_identify1`, `inputcard_identify2`, `inputcard_join`, `inputcard_joinok`, `inputcard_transfer`, or `inputcard_transfering`.
- Button actors: the relevant original `button_*` VCE timelines for the current state.
- Keypad actors: `keypad`, `key`, `cursor`, `keypad_join`, and `keypad_next`.

Each actor has normal and additive render pools. Blend pairs `(5,6)` and `(2,1)` use the normal pool; `(5,2)` and `(2,2)` use the additive material. Blend selection is stored per keyframe rather than per layer because a timeline may change blend mode during playback.

## State flow

`main` shows the original main panel and MEMBER LOGIN, GUEST, and JOIN NOW controls. USB scanning continues while this state is active.

- Existing USB: `main -> loading -> identify2 -> proceed`.
- Unknown USB: `main -> join -> nameKeypad -> joinok -> identify2 -> proceed`.
- Guest: `main -> proceed` without binding a profile.
- Failed USB/profile operation: current state `-> error -> main`.
- Back from join or keypad: return to `main` without creating a profile.

The arcade's two-field ID/password flow is intentionally adapted to a single DJ-name entry. The displayed keypad, key feedback, JOIN/NEXT confirmation, panels, and transition animations remain authentic.

## Touch keypad

The keypad uses the original 38 dummy-layer rectangles from `keypad.vce` as touch targets. The character rows use the arcade character map `1234567890QWERTYUIOPASDFGHJKL  ZXCVBNM`. Empty entries in the map become action/toggle positions rather than printable spaces. Pressing a character plays `key.vce` at the target rectangle and updates a VCE-aligned DJ-name label. Backspace removes one character. Confirmation is disabled for an empty or whitespace-only name. The stored name is trimmed and limited to the visible arcade field length.

No `TextField` receives focus and no desktop keyboard is required for the primary flow. A hardware keyboard may mirror character/backspace/confirm input only if it does not alter touch behavior.

## USB behavior

`tm.profile.rescanForToken()` decides whether a USB token is present. `tm.profile.loginWithPendingToken()` handles an existing profile. `tm.profile.createProfileWithPendingToken(name)` binds a first-time token only after a valid name is confirmed. The pending token is not cleared until persistence succeeds. USB tokens and card identifiers must never be printed into verification output.

## Generated assets

`tools/generate_login_vce_assets.py` owns the generated sprite cutouts and `Login VCE Manifest.txt`. It exports all required clips, retains dummy hitbox metadata for the keypad, and emits key tuples with a discrete blend class. Generated files are not hand-edited.

## Verification and deployment

`T2LoginVceUsbVerification` must fail before the overhaul and pass afterward. It checks authoritative clips, independent actor pools, per-key blend routing, the 38 keypad targets and character map, removal of the generic name `TextField`, complete state transitions, Lua parsing, and preservation of USB API calls.

After verification, build the AssetBundle through Unity batch mode. A successful build must copy/rename `Assets\AssetBundles\default` to `_TestBuild3\Themes\Technika 2.tmtheme`. Completion requires a successful Unity log marker plus matching SHA-256 hashes for the source bundle and deployed theme.
