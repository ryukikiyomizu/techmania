# AIC Pico NFC and Multi-Credential Login Design

## Goal

Add cabinet-safe AIC Pico card authentication to TECHMANIA while retaining the existing USB token login. A local profile can own multiple credentials, including one or more NFC cards and USB tokens. Any linked credential resolves the same profile, and the resolved profile remains latched until the arcade session explicitly logs out.

## Hardware authority

The implementation follows the upstream `whowechina/aic_pico` firmware at commit `5a0dbc9d132218c67f2a3b500e4251c96fcaf2df`.

The Pico exposes two CDC serial interfaces:

- `AIC Pico CLI Port` is the configuration and diagnostic console.
- `AIC Pico AIME Port` is the game-facing reader protocol.

Windows previously assigned COM4 to interface MI_03 (`AIC Pico CLI Port`) and COM12 to interface MI_05 (`AIC Pico AIME Port`) for device VID `CAFF`, PID `400E`. COM numbers are not stable cabinet configuration, so the runtime must discover the interface by its bus-reported descriptor and VID/PID rather than hardcode COM12.

The same composite device also exposes `AIC Pico CardIO`, a vendor-defined HID interface. This is the interface that emits the `CardIO ...` identifiers shown by the reader diagnostics. The device was not physically present during design verification; the Windows interfaces were retained as phantom devices. Runtime connection behavior therefore requires a fresh hardware validation after implementation.

## Supported physical card families

The credential layer supports both card families shown by the AIC Pico:

1. FeliCa / Amusement IC
2. MIFARE, including MIFARE Ultralight and NTAG

The normalized identifier is the Pico's documented eight-byte CardIO identity, encoded as uppercase hexadecimal:

- FeliCa: original eight-byte IDm.
- MIFARE with four-byte UID: `E0 04` + UID + the first two UID bytes.
- MIFARE with seven-byte UID: `E0` + the seven-byte UID.

Examples from the connected reader's diagnostic output:

- FeliCa `01 2E 5C E0 E9 C6 77 78` becomes `012E5CE0E9C67778`.
- MIFARE UID `04 6A B9 A5 6C 26 81` becomes `E0046AB9A56C2681`.

The stored credential key includes its transport and identity kind, for example `nfc:cardio:012E5CE0E9C67778`. USB tokens use `usb:token:<value>`. This prevents a raw NFC value from colliding with a USB token or a future remote credential format.

## Reader transport

The production reader service uses the Pico's CardIO HID interface. This is preferable to the AIME serial interface for TECHMANIA because it provides the exact canonical eight-byte identifiers above and preserves the physical family through HID report IDs: report 1 is MIFARE/e-amusement form and report 2 is FeliCa. The upstream firmware enables CardIO whenever no reader protocol is active and clears CardIO when an AIME or Bandai Namco serial session becomes active. TECHMANIA therefore must not open the AIME port while CardIO scanning is enabled.

Unity Input System 1.18 is already installed and supports custom vendor-defined HID layouts on Windows. The implementation registers a custom layout matching VID `CAFF`, PID `400E`, usage page `FFCA`, usage `01`. It consumes the nine-byte input report (`reportId` plus eight identifier bytes) without adding a native HID library or a polling thread.

The reader service has four isolated responsibilities:

1. Register and discover the `AIC Pico CardIO` HID device.
2. Decode MIFARE, FeliCa, and cleared/removal reports.
3. Debounce card-present, card-removed, reader-connected, and reader-disconnected state.
4. Publish normalized credential observations to the session coordinator.

The CLI port remains available for configuration and diagnostics. The AIME port remains available for games that require Sega/Bandai serial protocol, but TECHMANIA does not open it during its CardIO session. A future optional AIME transport can sit behind the same credential-source interface, but it is outside the first implementation because the Pico's default virtual-AIC mode changes MIFARE identities and would not match the CardIO values shown by the user.

## Credential and profile model

`profile.json` moves from format version 1 to version 2. Version 2 retains the legacy `cardId` field for compatibility and adds:

- `profileId`: stable profile identity, independent of credentials and folder name.
- `credentials`: a list of credential records containing `kind`, `id`, `createdAt`, and optional display metadata.

Loading a version 1 profile performs an in-memory migration. Its existing `cardId` becomes a `usb:token` credential because existing TECHMANIA profiles were created through the USB-token flow. The migration is saved only through the normal profile persistence owner; it does not rewrite every profile at boot.

Credential lookup compares normalized keys using ordinal, case-insensitive comparison after canonicalization. A credential may belong to at most one profile. Duplicate credentials inside one profile are collapsed during migration/save. Conflicts across profiles are reported and never resolved by silently choosing the first directory.

## Authentication and session behavior

The existing Guest/member ownership invariant remains unchanged:

- Guest owns machine options and Guest records/statistics.
- An authenticated profile owns its records, statistics, cosmetics, progression, and player options.

At the login screen, USB and NFC observations feed one pending-credential coordinator. The first stable credential wins. A stable NFC observation requires repeated matching reads across the debounce window; a disappearing or noisy tap cannot start two login transitions.

If the credential matches an existing profile, that profile logs in. If it is unknown, the existing first-time name flow creates a profile and binds the pending credential. After login, the session stores the stable `profileId`, profile name, authentication credential key, and source kind. Physical removal changes only the presence flag; it never changes record routes, options, cosmetics, or identity.

At the result/ending boundary, the removal popup checks the presence of the credential that authenticated the session. A USB-authenticated session asks for its USB device to be removed. An NFC-authenticated session asks for the card to be removed if it is still on the reader. If the player linked a second credential during the session, its presence does not replace the authentication credential used by the removal check.

## Linking NFC cards and USB tokens

Linking is explicit and available only in an authenticated profile context. The Profile/Card Customize screen opens a bounded `Link Card / USB` capture state. During this state:

1. Existing session identity remains active.
2. The scanner waits for a credential different from the authentication credential.
3. If the credential is already linked to this profile, the UI reports that it is already linked and makes no change.
4. If it belongs to another profile, linking is rejected without exposing that profile's private data.
5. If it is unclaimed, the UI asks for confirmation and then persists it atomically.
6. Cancel or timeout leaves the profile unchanged.

An unrecognized card tapped during ordinary gameplay or by another player is ignored. Automatic linking is forbidden because it could attach a bystander's card to the active player's profile.

At least one credential must remain linked to a member profile. Unlinking the credential that authenticated the current session is permitted only after another credential is confirmed, and affects the next login rather than the current latched session.

## Future server API boundary

Hardware discovery, credential reading, credential normalization, profile resolution, and session activation remain separate components. Local resolution initially scans local profile data through a `CredentialResolver` boundary. A future HTTP resolver can map any credential key to the same stable `profileId` without changing the reader or the theme scripts.

The existing NeverEndingTechnika source models a card ID as the direct owner of a game reference. It does not currently model multiple cards for one player. TECHMANIA therefore stores aliases locally now; future server work must introduce a credential-to-profile mapping instead of creating a second player for every linked credential.

Raw USB tokens and NFC IDs are never printed in ordinary logs. Diagnostics log the source kind, reader state, and a short non-reversible fingerprint only.

## Failure handling

- Missing Pico: USB scanning and Guest login continue normally.
- Reader unplug: Input System device removal publishes disconnected and clears only physical presence.
- Reader replug: Input System device addition is matched again by VID/PID and HID capabilities; COM reassignment is irrelevant.
- Unsupported/malformed HID report: discard it without changing session identity.
- Repeated identical reads: emit one card-present event until removal is observed.
- USB and NFC appear together: the first debounced pending credential wins; the other is ignored until the flow returns to idle.
- Credential conflict: reject login/linking with a generic conflict message and preserve all profile files.
- Persistence failure: do not clear the pending credential or mutate the active credential list.
- Application shutdown/domain reload: cancel the worker and close the serial port deterministically.

## Implementation boundaries

The implementation will add small focused runtime units rather than expanding `ProfileManager` into a serial driver:

- AIC Pico CardIO custom Input System device layout.
- HID report decoder and presence/debounce state machine.
- Credential normalization/value types.
- Credential resolver and profile migration/linking methods.
- A coordinator that exposes pending/present/session state to `ProfileManager` and the theme API.

`ProfileManager` remains the owner of Guest/member routing and session latching. `ThemeProfileApi` remains the Lua boundary. The login VCE flow consumes unified pending credentials without handling card bytes or COM ports directly.

## Verification

Automated tests must cover:

- CardIO HID report IDs, exact eight-byte decoding, zero/removal reports, and malformed report rejection.
- FeliCa and four-/seven-byte MIFARE normalization.
- Version 1 profile migration and version 2 round-trip serialization.
- Duplicate and cross-profile credential conflict detection.
- First credential wins when USB and NFC arrive together.
- Removal does not log out or reroute profile records/options.
- Logout returns to Guest and clears the latched session.
- Reader disconnect/reconnect does not block or replace the active session.
- Link, already-linked, conflict, cancel, timeout, and persistence-failure paths.

Cabinet validation must then use the physical AIC Pico with one FeliCa card and one MIFARE/NTAG card, plus a USB profile. Each credential must log into the same linked profile in separate sessions, while Guest retains machine-owned settings. The fresh Windows player build and deployed Technika 2 theme must be verified from logs and hashes. Existing note skins, note effects, combo effects, and gameplay UI assets are outside this change and must remain byte-identical.
