# T2 Profile Editor — Arcade Hybrid Design

## Goal

Redesign the working profile editor so it reads as an authentic DJMAX Technika 2 arcade card-customization scene rather than a generic two-column settings form. Preserve the existing profile session, USB-card, cosmetic catalog, save, and back behavior.

## Approved Direction

Direction C: Arcade Hybrid.

The active DJ card is the hero of the screen. Authentic, information-dense arcade controls remain visible and touch-friendly, while low-priority text fields move into a compact identity strip below the card preview.

## Composition

- Retain the existing T2 LoginState background, Platinum Crew crest animation, header chrome, poster, separators, and authentic button assets.
- Use one unified right-side customization stage instead of two visually equal form columns.
- Top-left of the stage: live arcade-card preview containing DJ icon, name, plate, pattern, level, card/member state, crew emblem, and crew name.
- Top-right of the stage: six large cosmetic tiles arranged as a 3-by-2 grid: DJ icon, DJ plate, DJ pattern, crew plate, crew pattern, and catalog entry.
- Bottom strip: DJ message, crew name, no-crew state, back, and save profile.
- Keep the poster as supporting atmosphere. It must not compete with the active card preview.

## Visual Hierarchy

1. Active DJ identity and arcade card.
2. Cosmetic category tiles and their selected IDs/previews.
3. Save profile action.
4. Editable text and crew state.
5. Supporting instructions and back action.

## Styling

- Follow T2's cool silver, cyan, white, and charcoal palette with the existing LoginState textures.
- Use layered silver panels, fine cyan borders, dotted separators, beveled controls, and restrained additive highlights.
- Preserve arcade typography: outlined uppercase headings, compact metadata, and large legible action labels.
- Avoid flat web-dashboard cards, modern pill controls, excessive empty space, and unrelated pixel-art styling.
- Selected catalog items use the authentic warm yellow/orange highlight; normal controls remain silver/cyan.
- Keep body copy short enough to remain readable at 1280x720.

## Interaction

- Selecting a cosmetic tile opens the existing authentic catalog overlay for that category.
- The tile reflects the currently selected catalog thumbnail and numeric ID before saving.
- Catalog changes continue to preview immediately and commit only through Save Profile.
- Back restores the captured profile-option snapshot and exits as it does now.
- Save persists to the active card session, not guest/machine storage.
- Disabled crew controls visually dim when No Crew is active without changing their geometry.
- Touch targets must remain stable and must not animate or shift under the pointer.

## Implementation Constraints

- Preserve all existing queried element names used by `Profile Screen.txt`, or update the script and its regression tests atomically.
- Prefer UXML/USS restructuring over behavioral rewrites.
- Reuse assets already supplied by the T2 VCE/LoginState source and current theme bundle.
- The catalog overlay must remain above all editor layers and retain paging, selection, cancel, and apply behavior.
- The design must fit the fixed 1280x720 arcade viewport without clipping.

## Verification

- Run the profile session/option snapshot and Login VCE USB regression tests.
- Build the theme AssetBundle successfully.
- Deploy `Assets/AssetBundles/default` as `_TestBuild3/Themes/Technika 2.tmtheme` and confirm matching hashes.
- Verify in the fresh test runtime at 1280x720: profile editor opens, every cosmetic category opens, selections preview, cancel restores, save persists to the active card session, and back exits.
- Inspect `Player.log` for profile/Lua/UI errors after the full route.
