# Login Runtime Polish and Mode Input Design

## Scope

Use the 2026-08-11 runtime capture as evidence for two focused corrections: remove the unsupported-mode popup path, and eliminate neighboring-glyph fragments from the authentic LoginState keypad export. Preserve the established USB state machine, VCE animation timing, authored touch geometry, and Star/Pop gameplay flow.

## Mode Select Input

- Star and Pop remain the only playable arcade modes and the only entries in arrow cycling.
- Club, Duo, and Crew header regions are consumed silently. They do not confirm the current mode, change the selected mode, or show a popup.
- The full header band is guarded from the screen-wide touch-to-confirm fallback.
- Remove the `MODE NOT AVAILABLE` overlay and its timer/coroutine lifecycle entirely.

## Login Keypad Rendering

- Continue exporting the 38 glyphs from the authentic `LoginState/keyboard/key_text.png` atlas.
- Retain the authored 80 x 64 cell canvas and positions.
- Clear only the unused right-side spill area, columns 64 through 79. Runtime evidence and alpha-bound inspection show the real glyphs end at column 61; the visible fragments begin at columns 73 and 77 on Q and N.
- Preserve VCE key feedback chrome, cursor, input text, NEXT/X controls, USB polling, and profile creation behavior.

## Verification

- A regression verifier must reject any `MODE NOT AVAILABLE` popup/handler and require Star/Pop-only cycling plus a consumed header band.
- The login verifier must decode all generated glyph PNGs and reject nontransparent pixels in columns 64 through 79.
- Run the focused Unity verifiers, rebuild the AssetBundle, deploy it to `_TestBuild3/Themes/Technika 2.tmtheme`, and compare source/deployed SHA-256 hashes.
