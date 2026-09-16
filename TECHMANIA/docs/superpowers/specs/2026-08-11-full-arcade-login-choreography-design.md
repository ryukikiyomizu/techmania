# Full Arcade Login Choreography Design

## Goal

Make the Technika 2 login behave like the DMT2 arcade login while using a USB token as the physical membership card. The extracted `DMT2/ReverseEngineering/login_reference_extract/LoginState` resources and their original VCE timelines are the authority for composition, timing, additive rendering, hit areas, and sound feedback.

## Scope

This replaces the shortened USB-first choreography. It does not emulate the retired online membership service, add an ID/password form, change the profile file format, or alter gameplay. The existing asynchronous `ProfileManager` USB scanner remains the backend so inserting slow media cannot block the Unity thread.

## State choreography

The login opens in `main`, playing `inputcard_main` with the persistent `inputcard_head`, `inputcard_panel`, and `banner_image` actors. MEMBER LOGIN starts card presentation rather than opening a desktop form. GUEST follows the original guest button feedback and exits without a token-bound profile. DATA TRANSFER remains visible and routes to the local transfer explanation without pretending that a remote service exists.

When a USB is detected, the state sequence is:

- Known token: `main -> login -> loading -> identify1 -> identify2 -> proceed`.
- Unknown token: `main -> login -> loading -> join -> nameKeypad -> joinok -> identify1 -> identify2 -> proceed`.
- Read, persistence, or profile failure: active state `-> error -> main`.
- BACK, keypad X, or Escape from join/keypad/transfer: cancel to `main` and suppress reopening for that same presented token until it is removed or the player deliberately selects MEMBER LOGIN again.

State transitions wait for the authoritative VCE clip duration instead of arbitrary generic fades. Button timelines play once per press. The original LoginState audio clips provide click, keypad, confirm, back, quit, and warning feedback at their corresponding transitions.

## Presentation

All visible panels, buttons, keyboard keys, pressed-key chrome, cursor, and additive effects come from the extracted LoginState assets. VCE geometry determines placement at the 1280x720 reference canvas. Normal and additive layers remain separate, and persistent actors are not reset when the state actor changes.

The animated login background plays beneath the VCE composition and begins from its intended first frame whenever the login screen is shown. No generic Unity `Button`, `TextField`, grey fallback panel, or manually redrawn replacement is visible.

The arcade keypad uses the 38 authored touch rectangles and their original character ordering. Only the selected key's feedback chrome is overlaid; the atlas's first glyph is never rendered as part of feedback. The stable NEXT and X controls use the authored button art and hitboxes. The DJ-name display is a single non-editable arcade-aligned label with a blinking cursor and a 12-character limit.

## USB and profile behavior

The USB represents a card, not a complete profile. Its token locates a local TECHMANIA profile. A known token fills the arcade identification presentation from the bound profile, and an unknown token can create one new profile. DATA TRANSFER preserves the arcade panel and local guidance only; a two-card profile-rebinding workflow is outside this login choreography and must not pretend that the retired remote service exists.

USB scanning stays single-flight and asynchronous. Token values are never displayed or logged. A rejected duplicate name remains on the keypad with an arcade-styled message so the player can edit it. Creation clears the pending token only after profile persistence succeeds.

## Input and recovery

Touch is the primary arcade input. Physical keyboard characters, Backspace, Enter, Numpad Enter, and Escape mirror the touch actions for service/testing without changing focus. Every state has a deterministic exit, and no failure can trap the player on Welcome, keypad, loading, or error screens.

If a USB disappears during login, the current operation ends in the authored error state and returns to `main`. Repeated scan polls never start overlapping drive scans. A failed or slow device does not prevent other mounted USB devices from being checked.

## Verification and deployment

Regression verification must cover the full ordered state paths, clip-duration-driven transitions, authentic audio mapping, VCE geometry, additive routing, keypad behavior, cancellation suppression, token removal, duplicate-name retry, and single-flight USB scanning. A live attached-USB test must prove polling remains under 100 ms when hardware is available.

Completion requires a successful Unity player build and theme AssetBundle build, a startup smoke test from `_TestBuild3/TECHMANIA.exe`, matching source/deployed Technika 2 bundle hashes, preservation of the separate sub-90 MiB barebone Default theme, and `graphify update .`.
