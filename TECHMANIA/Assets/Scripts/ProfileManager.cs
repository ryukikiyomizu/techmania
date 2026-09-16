using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

// ProfileManager — session owner for the T3HD profile system.
//
// Placement: static class in Assets\Scripts\ (not a MonoBehaviour).
//   - No frame-tick or coroutine needed; USB detection at LOGIN is Lua-driven.
//   - Callable from C# and (via P3 Techmania.cs) from Lua.
//
// Session invariant: there is ALWAYS an active record/stat owner.
//   Guest uses the cabinet's machine options plus Profiles\Guest records/stats.
//   A logged-in member uses that profile's records/stats/player options.
//
// Login flow:
//   Theme calls hasPendingToken() at LOGIN scene. If true, call loginByToken().
//   Otherwise show "Tap card / Plug USB" UI, scan for token, call loginByToken().
//   On first-time profile: createProfile(name) generates cardId + folder, then login().
//
// Logout flow:
//   logout() → restore machine options, refresh records+stats from Guest folder.
//   C# enforces "no session at SPLASHHOME/LOGIN" — theme should call logout()
//   before transitioning to SPLASHHOME.

public static class ProfileManager
{
    // ── Session state ─────────────────────────────────────────────────────────

    public enum SessionState { Guest, LoggedIn }

    public static SessionState state { get; private set; }

    // Null when state == Guest.
    public static string profileName   { get; private set; }
    public static string profileFolder { get; private set; }
    private static ProfileData activeProfileData;
    private static long guestUserExp;

    // ── Events ────────────────────────────────────────────────────────────────

    // Fired on every login/logout (including boot-time Guest init).
    // Themes register here to refresh profile-display UI.
    // Correctness does NOT depend on a handler being registered.
    public static event Action profileChanged;

    // ── Machine options snapshot ──────────────────────────────────────────────
    // Captured at Initialize() before any player login contaminates the
    // machine options.json. Restored on logout so Guest always gets clean
    // machine defaults, not the last player's settings.
    private static PlayerOptions machineSnapshot;

    // ── Boot-time USB token ───────────────────────────────────────────────────

    // USB drive access can stall for seconds on arcade cabinets, so only one
    // background scan may run at a time. Results are collected on Unity's
    // thread by the public polling methods before they touch session state.
    private static readonly object usbScanLock = new object();
    private static Task<string> usbScanTask;
    private static string usbScanExpectedToken;
    private static string pendingCardId;
    private static string presentedCardId;
    // Credential dismissed with BACK on the first-time registration screen.
    // The same physical card/USB stays ignored, but a different credential is
    // accepted immediately without requiring an empty-reader frame.
    private static string dismissedCredentialId;
    // Authenticated identity is detached from the physical drive. Removal does
    // not end a session; this binding is cleared only by logout/new login.
    private static string sessionCardId;
    private static bool sessionCardPresent;
    private static NfcReaderService nfcReaderService;
    private static bool credentialLinkMode;
    private static string credentialLinkStatus;

    private const uint kIoctlStorageQueryProperty = 0x002D1400;
    private const byte kBusTypeUsb = 7;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName,
        uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device,
        uint controlCode, byte[] input, int inputSize, byte[] output,
        int outputSize, out int bytesReturned, IntPtr overlapped);

    public static bool hasPendingToken()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        lock (usbScanLock)
            return !string.IsNullOrEmpty(pendingCardId);
    }

    // Preferred theme-facing poll. AIC Pico and USB both feed the same
    // pending credential without exposing raw device access to Lua.
    public static bool pollForCredential()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        lock (usbScanLock)
        {
            if (!string.IsNullOrEmpty(pendingCardId)) return true;
        }
        if (state == SessionState.Guest || credentialLinkMode) StartUsbScan();
        return false;
    }

    // Starts a fresh cabinet login cycle. Login is a session boundary: the
    // previous player's routes/options must be released before another USB or
    // NFC credential can be accepted. A card already resting on the reader is
    // re-latched by PumpNfcEvents; USB discovery is restarted asynchronously.
    public static void beginLoginCredentialLoop()
    {
        string previousSessionCard = null;
        bool previousSessionCardPresent = false;
        if (state == SessionState.LoggedIn)
        {
            lock (usbScanLock)
            {
                previousSessionCard = sessionCardId;
                previousSessionCardPresent = sessionCardPresent;
            }
            logout();

            // Returning to Title while the member's credential is still on
            // the reader must not immediately authenticate that same member.
            // Keep the dismissal only for a credential known to be present;
            // the removal edge clears it and permits a later re-present.
            if (previousSessionCardPresent &&
                !string.IsNullOrEmpty(previousSessionCard))
            {
                lock (usbScanLock)
                    dismissedCredentialId = previousSessionCard;
            }
        }
        else
        {
            lock (usbScanLock)
            {
                pendingCardId = null;
                presentedCardId = null;
                credentialLinkMode = false;
                credentialLinkStatus = string.Empty;
            }
        }

        PumpNfcEvents();
        StartUsbScan();
        Debug.Log("[ProfileManager] Login credential loop armed for NFC and USB.");
    }

    public static void dismissPendingCredential()
    {
        lock (usbScanLock)
        {
            string target = !string.IsNullOrEmpty(pendingCardId)
                ? pendingCardId
                : presentedCardId;
            // Only overwrite the dismissal when a credential is actually
            // presented. Backing out of a page that was opened with nothing on
            // the reader used to store null here, releasing the dismissal that
            // returning to Title had just recorded, so the previous member's
            // still-held card was re-accepted by the next scan and logged that
            // player straight back in.
            if (!string.IsNullOrEmpty(target)) dismissedCredentialId = target;
            pendingCardId = null;
            presentedCardId = null;
        }
        Debug.Log("[ProfileManager] Current credential dismissed; waiting for a different NFC card or USB profile.");
    }

    // The explicit counterpart to dismissPendingCredential. MEMBER LOGIN means
    // "authenticate whatever is on the reader right now", so it has to be able
    // to re-admit a dismissed credential. Automatic scanning must keep ignoring
    // one -- that is what stops Back and the walk home from logging a still-held
    // card straight back in -- but a card that never physically leaves the
    // reader emits no removal edge, so without this the player could not use it
    // again for the rest of the session: after backing out of the sign-up page
    // the card page would sit there asking for an insertion that had already
    // happened. This is also the escape hatch for any removal edge that never
    // reaches here, whatever the reason.
    public static bool requestPresentedCredential()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        lock (usbScanLock)
        {
            if (!string.IsNullOrEmpty(pendingCardId)) return true;
            dismissedCredentialId = null;
        }

        // Re-latch from the reader's level state: a card that was already
        // resting there when it was dismissed produces no new present edge.
        PumpNfcEvents();
        lock (usbScanLock)
        {
            if (!string.IsNullOrEmpty(pendingCardId)) return true;
        }
        if (state == SessionState.Guest || credentialLinkMode) StartUsbScan();
        Debug.Log("[ProfileManager] Explicit credential request: nothing is presented yet.");
        return false;
    }

    public static string pendingCredentialKind()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        lock (usbScanLock)
            return ProfileCredentials.Kind(pendingCardId);
    }

    // Convenience: login using the boot-time USB token without exposing the cardId to Lua.
    // Returns true if a local profile matched. Returns false if unknown card (caller should createProfile).
    public static bool loginWithPendingToken()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        string cardId;
        lock (usbScanLock)
            cardId = pendingCardId;
        if (string.IsNullOrEmpty(cardId)) return false;

        string name = FindProfileNameByToken(cardId);
        if (string.IsNullOrEmpty(name)) return false;

        login(name);
        AttachSessionCard(cardId);
        lock (usbScanLock)
        {
            if (pendingCardId == cardId) pendingCardId = null;
        }
        return true;
    }

    // Polls the single background USB scan without doing drive I/O on Unity's
    // main thread. Returns null while scanning and the cardId when ready.
    public static string rescanForToken()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        lock (usbScanLock)
        {
            if (!string.IsNullOrEmpty(pendingCardId)) return pendingCardId;
        }

        StartUsbScan();
        return null;
    }

    // Polls whether the card which opened the current login flow is still
    // physically mounted. Until the worker completes, the last known-present
    // result is retained so a slow USB never causes a false removal.
    public static bool pollPendingTokenPresence()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        string expectedToken;
        lock (usbScanLock)
            expectedToken = presentedCardId;
        if (string.IsNullOrEmpty(expectedToken)) return false;
        if (ProfileCredentials.Kind(expectedToken) == "nfc")
        {
            lock (usbScanLock)
                return string.Equals(presentedCardId, expectedToken,
                    StringComparison.Ordinal) && nfcReaderService != null &&
                    nfcReaderService.CardPresent;
        }

        StartUsbScan(expectedToken);
        lock (usbScanLock)
            return !string.IsNullOrEmpty(presentedCardId);
    }

    public static bool hasSessionCard()
    {
        PumpNfcEvents();
        lock (usbScanLock)
            return state == SessionState.LoggedIn &&
                !string.IsNullOrEmpty(sessionCardId);
    }

    public static string sessionCredentialKind()
    {
        lock (usbScanLock)
            return ProfileCredentials.Kind(sessionCardId);
    }

    // Presence is an exit-safety signal only. It never changes the logged-in
    // profile, score route, options, or the session card identifier.
    public static bool pollSessionCardPresence()
    {
        PumpNfcEvents();
        CompleteUsbScanIfReady();
        string expectedToken;
        lock (usbScanLock)
            expectedToken = sessionCardId;
        if (state != SessionState.LoggedIn ||
            string.IsNullOrEmpty(expectedToken)) return false;
        if (ProfileCredentials.Kind(expectedToken) == "nfc")
        {
            lock (usbScanLock)
                return sessionCardPresent;
        }

        StartUsbScan(expectedToken);
        lock (usbScanLock)
            return sessionCardPresent;
    }

    private static void StartUsbScan(string expectedToken = null)
    {
        if (ProfileCredentials.Kind(expectedToken) == "nfc") return;
        lock (usbScanLock)
        {
            if (usbScanTask != null) return;
            if (string.IsNullOrEmpty(expectedToken) &&
                (!string.IsNullOrEmpty(pendingCardId) ||
                 !string.IsNullOrEmpty(presentedCardId))) return;

            usbScanExpectedToken = expectedToken;
            usbScanTask = string.IsNullOrEmpty(expectedToken)
                ? Task.Run(FindUsbToken)
                : Task.Run(() => FindPresentedToken(expectedToken));
        }
    }

    private static void CompleteUsbScanIfReady()
    {
        Task<string> completed;
        string expectedToken;
        lock (usbScanLock)
        {
            if (usbScanTask == null || !usbScanTask.IsCompleted) return;
            completed = usbScanTask;
            expectedToken = usbScanExpectedToken;
            usbScanTask = null;
            usbScanExpectedToken = null;
        }

        string found = null;
        bool scanFailed = false;
        try
        {
            found = completed.GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(found))
                found = ProfileCredentials.NormalizeUsb(found);
        }
        catch (Exception ex)
        {
            scanFailed = true;
            Debug.LogWarning($"[ProfileManager] Background USB scan failed: {ex.Message}");
        }

        lock (usbScanLock)
        {
            if (!string.IsNullOrEmpty(expectedToken))
            {
                if (string.Equals(expectedToken, sessionCardId,
                    StringComparison.Ordinal))
                {
                    sessionCardPresent = string.Equals(found, expectedToken,
                        StringComparison.Ordinal);
                    return;
                }
                if (!string.Equals(found, expectedToken,
                    StringComparison.Ordinal))
                {
                    if (pendingCardId == expectedToken) pendingCardId = null;
                    if (presentedCardId == expectedToken) presentedCardId = null;
                }
                return;
            }

            if (string.IsNullOrEmpty(found))
            {
                // A clean scan that found nothing is a USB drive's removal
                // edge: it has no event stream, so PumpNfcEvents' card-removed
                // release has no counterpart here. Without this a drive that
                // was dismissed on the way back to Title could never log in
                // again for the rest of the process. A failed scan proves
                // nothing and must leave the dismissal alone.
                if (!scanFailed &&
                    ProfileCredentials.Kind(dismissedCredentialId) == "usb")
                    dismissedCredentialId = null;
                return;
            }
            TryAcceptPresentedCredential(found);
        }
        Debug.Log("[ProfileManager] USB token detected asynchronously.");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Called from Startup.cs after Records.RefreshInstance().
    // Sets session to Guest, then scans USB drives for a token.
    public static void Initialize()
    {
        state = SessionState.Guest;
        profileName = null;
        profileFolder = null;
        activeProfileData = null;
        guestUserExp = 0;

        lock (usbScanLock)
        {
            pendingCardId = null;
            presentedCardId = null;
            dismissedCredentialId = null;
            sessionCardId = null;
            sessionCardPresent = false;
            usbScanTask = null;
            usbScanExpectedToken = null;
            credentialLinkMode = false;
            credentialLinkStatus = string.Empty;
        }

        nfcReaderService?.Dispose();
        nfcReaderService = new NfcReaderService();
        if (!nfcReaderService.Start())
            Debug.LogWarning("[ProfileManager] NFC reader unavailable; USB and Guest remain active.");
        else
            Debug.Log("[ProfileManager] NFC hook started; CardIO discovery is running off the main thread.");

        machineSnapshot = PrepareGuestMachineOptions(Options.instance,
            Paths.GetProfileOptionsFilePath(Paths.kGuestProfileName),
            Paths.GetOptionsFilePath(),
            Path.Combine(Paths.GetProfilesFolder(),
                ".guest-options-migrated-v3"));

        // Guest owns the cabinet's machine options directly. Snapshot only
        // the player-facing fields so a member overlay can be removed on
        // logout without ever routing Guest through a profile folder.

        Debug.Log($"[ProfileManager] Guest machine options active — " +
            $"Volume={Options.instance.masterVolumePercent}, " +
            $"Locale={Options.instance.locale}, NoteSkin={Options.instance.noteSkin}");

        StartUsbScan();
        Debug.Log("[ProfileManager] Initialized — Guest session; background USB scan started.");
    }

    public static void Shutdown()
    {
        nfcReaderService?.Dispose();
        nfcReaderService = null;
    }

    private static void PumpNfcEvents()
    {
        NfcReaderService reader = nfcReaderService;
        if (reader == null) return;
        reader.Poll();

        while (reader.TryDequeue(out CredentialObservation observation))
        {
            string key = ProfileCredentials.NormalizeNfc(
                observation.CredentialKey);
            if (key == null) continue;

            lock (usbScanLock)
            {
                if (observation.EventType == CredentialEventType.Present)
                {
                    if (string.Equals(sessionCardId, key,
                        StringComparison.Ordinal)) sessionCardPresent = true;
                    if (state == SessionState.Guest || credentialLinkMode)
                        TryAcceptPresentedCredential(key);
                    continue;
                }

                if (string.Equals(sessionCardId, key,
                    StringComparison.Ordinal)) sessionCardPresent = false;
                if (string.Equals(presentedCardId, key,
                    StringComparison.Ordinal)) presentedCardId = null;
                if (string.Equals(pendingCardId, key,
                    StringComparison.Ordinal)) pendingCardId = null;
                if (string.Equals(dismissedCredentialId, key,
                    StringComparison.Ordinal)) dismissedCredentialId = null;
            }
        }

        // Entering Login can occur while a card is already resting on the
        // reader, so there may be no new Present edge after the session reset.
        // Re-latch the reader's current level state as the pending credential.
        if (reader.CardPresent &&
            (state == SessionState.Guest || credentialLinkMode))
        {
            string currentKey = ProfileCredentials.NormalizeNfc(
                reader.CurrentCredentialKey);
            if (!string.IsNullOrEmpty(currentKey))
                TryAcceptPresentedCredential(currentKey);
        }

        lock (usbScanLock)
        {
            if (ProfileCredentials.Kind(sessionCardId) == "nfc")
                sessionCardPresent = reader.CardPresent &&
                    string.Equals(reader.CurrentCredentialKey, sessionCardId,
                        StringComparison.Ordinal);
            // A dismissal only means "ignore the credential that is still
            // resting on the reader", so it must not outlive the fact it
            // describes. The matching Removed observation normally releases it,
            // but the observation queue is cleared wholesale whenever the
            // service is reset or disposed, and a queue that is cleared after
            // the card is gone would otherwise leave that card suppressed for
            // the rest of the process. The reader's level state proves the same
            // thing the rescan proves for USB in CompleteUsbScanIfReady: once it
            // no longer reports the dismissed card, the dismissal has nothing
            // left to suppress. Note EventCardRemoved keeps CurrentCredentialKey
            // and only drops CardPresent, so both halves of the test are needed.
            if (ProfileCredentials.Kind(dismissedCredentialId) == "nfc" &&
                (!reader.CardPresent || !string.Equals(
                    reader.CurrentCredentialKey, dismissedCredentialId,
                    StringComparison.Ordinal)))
                dismissedCredentialId = null;
            if (ProfileCredentials.Kind(presentedCardId) == "nfc" &&
                (!reader.CardPresent || !string.Equals(
                    reader.CurrentCredentialKey, presentedCardId,
                    StringComparison.Ordinal)))
            {
                if (pendingCardId == presentedCardId) pendingCardId = null;
                presentedCardId = null;
            }
        }
    }

    private static bool TryAcceptPresentedCredential(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        lock (usbScanLock)
        {
            if (string.Equals(dismissedCredentialId, key,
                StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(dismissedCredentialId))
                dismissedCredentialId = null;
            if (string.IsNullOrEmpty(pendingCardId))
            {
                pendingCardId = key;
                presentedCardId = key;
                return true;
            }
            if (string.Equals(pendingCardId, key, StringComparison.Ordinal))
            {
                presentedCardId = key;
                return true;
            }
            return false;
        }
    }

    // Runs only on the worker task. First token found wins; failures on one
    // drive never prevent another attached device from being checked.
    private static string FindUsbToken()
    {
        try
        {
            string[] drives = Environment.GetLogicalDrives();
            var usbDrives = new List<string>();
            foreach (string drive in drives)
            {
                try
                {
                    if (!IsUsbDrive(drive)) continue;
                    usbDrives.Add(drive);
                    if (TryReadToken(drive, out string cardId)) return cardId;
                }
                catch { /* An unavailable drive must not abort the scan. */ }
            }

            // Provisioned first-time USB token: create it only on an actual USB
            // bus device, including fixed-type USB SSD/HDD enclosures.
            foreach (string drive in usbDrives)
            {
                try
                {
                    string cardFolder = Path.Combine(
                        drive.TrimEnd('\\', '/'), "TECHMANIA", "Card");
                    string tokenFile = Path.Combine(cardFolder, "token.tmtoken");
                    Directory.CreateDirectory(cardFolder);
                    if (!File.Exists(tokenFile))
                        File.WriteAllText(tokenFile, Guid.NewGuid().ToString("N"));

                    string cardId = File.ReadAllText(tokenFile).Trim();
                    if (!string.IsNullOrEmpty(cardId)) return cardId;
                }
                catch { /* Try the next USB device. */ }
            }
        }
        catch { /* Drive enumeration can fail transiently during hot-plug. */ }
        return null;
    }

    private static bool TryMigrateLegacyGuestOptions(Options machine,
        string legacyGuestPath, string machinePath, string markerPath)
    {
        if (machine == null || File.Exists(markerPath) ||
            !File.Exists(legacyGuestPath)) return false;
        try
        {
            PlayerOptions legacy = PlayerOptionsBase.LoadFromFile(
                legacyGuestPath) as PlayerOptions;
            if (legacy == null) return false;
            legacy.ApplyTo(machine, false);
            machine.SaveToFile(machinePath);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath,
                "Guest options migrated to cabinet options v2.");
            Debug.Log("[ProfileManager] Migrated legacy Guest options to " +
                "the cabinet options file; future sessions use split ownership.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ProfileManager] Legacy Guest option migration " +
                $"was not completed: {ex.Message}");
            return false;
        }
    }

    private static PlayerOptions PrepareGuestMachineOptions(Options machine,
        string legacyGuestPath, string machinePath, string markerPath)
    {
        TryMigrateLegacyGuestOptions(machine, legacyGuestPath, machinePath,
            markerPath);
        return PlayerOptions.ExtractFrom(machine);
    }

    // Worker-only presence probe. It never provisions media and returns only
    // the already-known token, so another mounted card cannot replace the one
    // that opened the current login flow.
    private static string FindPresentedToken(string expectedToken)
    {
        if (string.IsNullOrEmpty(expectedToken)) return null;
        try
        {
            foreach (string drive in Environment.GetLogicalDrives())
            {
                try
                {
                    if (IsUsbDrive(drive) &&
                        TryReadToken(drive, out string cardId) &&
                        string.Equals(ProfileCredentials.NormalizeUsb(cardId),
                            expectedToken,
                            StringComparison.Ordinal))
                        return expectedToken;
                }
                catch { /* A disappearing drive counts as not present. */ }
            }
        }
        catch { /* A failed enumeration is reported as not present. */ }
        return null;
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    // Called by themes after reading a USB token or NFC tap.
    // Scans local Profiles\ folder for a matching cardId.
    // If found: login(). If not found: returns false (caller should createProfile).
    public static bool loginByToken(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("[ProfileManager] loginByToken called with empty cardId.");
            return false;
        }

        string name = FindProfileNameByToken(cardId);
        if (string.IsNullOrEmpty(name)) return false;

        login(name);
        AttachSessionCard(cardId);
        lock (usbScanLock)
        {
            if (pendingCardId == cardId) pendingCardId = null;
        }
        return true;
    }

    // Logs into an existing profile by name.
    // Creates the folder if it was externally removed (recovery path).
    public static void login(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("[ProfileManager] login() called with empty name.");
            return;
        }

        string folder = Paths.GetProfileFolder(name);

        // Ensure folder exists (recovery: folder may have been deleted externally).
        Directory.CreateDirectory(folder);

        state = SessionState.LoggedIn;
        profileName = name;
        profileFolder = folder;
        activeProfileData = LoadProfileData(name);

        lock (usbScanLock)
        {
            sessionCardId = null;
            sessionCardPresent = false;
        }

        RestoreActiveStorageRoutes();

        // Refresh data from profile folder.
        try { Records.RefreshInstance(); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] records refresh failed for {name}: {ex.Message}");
        }
        try { Statistics.RefreshInstance(); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] stats refresh failed for {name}: {ex.Message}");
        }

        // Overlay player options (volumes, locale, skins, timing, modifiers, etc.)
        string optsPath = Paths.GetProfileOptionsFilePath(name);
        if (File.Exists(optsPath))
        {
            try
            {
                PlayerOptions player = PlayerOptionsBase.LoadFromFile(optsPath) as PlayerOptions;
                if (player != null)
                {
                    player.ApplyTo(Options.instance);
                    // Re-apply side effects that depend on the overlaid values.
                    AudioManager.instance.ApplyVolume();
                    L10n.SetLocale(Options.instance.locale, L10n.Instance.System);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileManager] player options load failed for {name}: {ex.Message}");
            }
        }

        Debug.Log($"[ProfileManager] Logged in as \"{name}\" — " +
            $"Records={Paths.GetRecordsFilePath()}, " +
            $"Options={optsPath}, NoteSkin={Options.instance.noteSkin}.");
        profileChanged?.Invoke();
    }

    // ── Create profile ────────────────────────────────────────────────────────

    // Creates a new profile folder and files, then calls login().
    // Caller is responsible for validating the name (length, illegal chars) first.
    public static void createProfile(string name)
    {
        string normalizedName = NormalizeProfileName(name);
        if (string.IsNullOrEmpty(normalizedName))
        {
            Debug.LogError("[ProfileManager] createProfile() called with an invalid name.");
            return;
        }

        string cardId = GenerateCardId();
        if (!TryCreateProfile(normalizedName, cardId)) return;
        login(normalizedName);
    }

    // Creates a first-time profile bound to the USB token currently presented
    // at Login. Unknown tokens remain pending after lookup and after failed
    // creation so the player can correct their name and retry safely.
    public static bool createProfileWithPendingToken(string name)
    {
        string normalizedName = NormalizeProfileName(name);
        string cardId;
        lock (usbScanLock)
            cardId = pendingCardId;
        if (string.IsNullOrEmpty(normalizedName) || string.IsNullOrEmpty(cardId))
            return false;

        if (!TryCreateProfile(normalizedName, cardId)) return false;

        lock (usbScanLock)
        {
            if (pendingCardId == cardId)
                pendingCardId = null; // clear only after persistence succeeds
        }
        login(normalizedName);
        AttachSessionCard(cardId);
        return true;
    }

    public static bool createProfileWithPendingCredential(string name)
    {
        return createProfileWithPendingToken(name);
    }

    // Linking is explicit: a random tap during gameplay can never attach a
    // credential to the active player.
    public static bool beginCredentialLink()
    {
        if (state != SessionState.LoggedIn || activeProfileData == null)
            return false;
        lock (usbScanLock)
        {
            credentialLinkMode = true;
            credentialLinkStatus = "waiting";
            pendingCardId = null;
            presentedCardId = null;
        }
        StartUsbScan();
        return true;
    }

    public static bool linkPendingCredential()
    {
        if (state != SessionState.LoggedIn || activeProfileData == null ||
            !credentialLinkMode) return false;
        PumpNfcEvents();
        CompleteUsbScanIfReady();

        string key;
        lock (usbScanLock) key = pendingCardId;
        if (string.IsNullOrEmpty(key)) return false;

        string owner = FindProfileNameByToken(key);
        if (!string.IsNullOrEmpty(owner) && !string.Equals(owner, profileName,
            StringComparison.OrdinalIgnoreCase))
        {
            credentialLinkStatus = "owned-by-another-profile";
            return false;
        }
        if (!ProfileCredentials.TryLink(activeProfileData, key))
        {
            credentialLinkStatus = "already-linked";
            return false;
        }

        try
        {
            activeProfileData.SaveToFile(
                Paths.GetProfileDataFilePath(profileName));
        }
        catch (Exception ex)
        {
            activeProfileData.credentials.RemoveAll(item => item.key == key);
            credentialLinkStatus = "save-failed";
            Debug.LogWarning("[ProfileManager] Credential link save failed: " +
                ex.Message);
            return false;
        }

        lock (usbScanLock)
        {
            credentialLinkMode = false;
            credentialLinkStatus = "linked";
            pendingCardId = null;
            presentedCardId = null;
        }
        return true;
    }

    public static void cancelCredentialLink()
    {
        lock (usbScanLock)
        {
            credentialLinkMode = false;
            credentialLinkStatus = "cancelled";
            pendingCardId = null;
            presentedCardId = null;
        }
    }

    public static string currentCredentialLinkStatus()
    {
        lock (usbScanLock) return credentialLinkStatus ?? string.Empty;
    }

    public static bool loginWithPendingCredential()
    {
        return loginWithPendingToken();
    }

    private static void AttachSessionCard(string cardId)
    {
        lock (usbScanLock)
        {
            sessionCardId = cardId;
            sessionCardPresent = !string.IsNullOrEmpty(cardId);
            dismissedCredentialId = null;
        }
    }

    private static bool TryCreateProfile(string name, string cardId)
    {
        string folder = Paths.GetProfileFolder(name);
        if (Directory.Exists(folder) || string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning($"[ProfileManager] Profile \"{name}\" already exists or has no card token.");
            return false;
        }

        try
        {
            Directory.CreateDirectory(folder);
            ProfileData data = new ProfileData
            {
                name = name,
                cardId = LegacyCardIdFor(cardId),
                createdAt = DateTime.UtcNow.ToString("o"),
                userExp = 0
            };
            ProfileCredentials.TryLink(data, cardId, data.createdAt);
            data.SaveToFile(Paths.GetProfileDataFilePath(name));

            // Records and stats are created on their first save.
            PlayerOptions defaultPlayer = new PlayerOptions();
            defaultPlayer.SaveToFile(Paths.GetProfileOptionsFilePath(name));

            Debug.Log($"[ProfileManager] Created token-bound profile \"{name}\".");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Failed to create profile \"{name}\": {ex.Message}");
            try
            {
                // The folder was confirmed absent before this operation, so
                // anything here belongs exclusively to this failed attempt.
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception cleanupEx)
            {
                Debug.LogWarning($"[ProfileManager] Failed to clean incomplete profile \"{name}\": {cleanupEx.Message}");
            }
            return false;
        }
    }

    private static bool TryReadToken(string drive, out string cardId)
    {
        cardId = null;
        string tmFolder = Path.Combine(drive.TrimEnd('\\', '/'), "TECHMANIA");
        if (!Directory.Exists(tmFolder)) return false;
        foreach (string dir in Directory.GetDirectories(tmFolder))
        {
            string tokenFile = Path.Combine(dir, "token.tmtoken");
            if (!File.Exists(tokenFile)) continue;
            string candidate = File.ReadAllText(tokenFile).Trim();
            if (string.IsNullOrEmpty(candidate)) continue;
            cardId = candidate;
            return true;
        }
        return false;
    }

    private static bool IsUsbDrive(string drive)
    {
        try
        {
            var info = new DriveInfo(drive);
            if (!info.IsReady) return false;

            bool usbBus = info.DriveType == DriveType.Fixed &&
                IsUsbBusDevice(drive);
            return IsUsableUsbVolume(info.DriveType, true, usbBus);
        }
        catch
        {
            return false;
        }
    }

    // Removable media is already classified by Windows and must not depend on
    // a device-control query which can be denied on locked-down cabinets.
    // Fixed disks are accepted only when the storage descriptor confirms USB.
    private static bool IsUsableUsbVolume(DriveType driveType, bool isReady,
        bool usbBus)
    {
        if (!isReady) return false;
        return driveType == DriveType.Removable ||
            (driveType == DriveType.Fixed && usbBus);
    }

    private static bool IsUsbBusDevice(string drive)
    {
        try
        {
            string devicePath = @"\\.\" + drive.TrimEnd('\\', '/');
            using (SafeFileHandle device = CreateFile(devicePath, 0, 3,
                IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (device.IsInvalid) return false;
                byte[] query = new byte[12];
                byte[] descriptor = new byte[1024];
                if (!DeviceIoControl(device, kIoctlStorageQueryProperty,
                    query, query.Length, descriptor, descriptor.Length,
                    out int returned, IntPtr.Zero) || returned <= 28)
                    return false;
                return descriptor[28] == kBusTypeUsb;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string normalized = name.Trim();
        if (normalized.Length > 24 ||
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        return normalized;
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    // Ends the current session and returns to Guest.
    // Restores machine options, reroutes records+stats to Guest folder.
    public static void logout()
    {
        if (state == SessionState.Guest) return;

        string name = profileName;
        state = SessionState.Guest;
        profileName = null;
        profileFolder = null;
        activeProfileData = null;
        guestUserExp = 0;

        lock (usbScanLock)
        {
            sessionCardId = null;
            sessionCardPresent = false;
            pendingCardId = null;
            presentedCardId = null;
            dismissedCredentialId = null;
            credentialLinkMode = false;
            credentialLinkStatus = string.Empty;
        }

        // Clear path overrides → GetRecordsFilePath/GetStatisticsFilePath
        // fall back to Profiles\Guest\ automatically.
        RestoreActiveStorageRoutes();

        // Reload records and stats for Guest.
        try { Records.RefreshInstance(); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Guest records refresh failed on logout: {ex.Message}");
        }
        try { Statistics.RefreshInstance(); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Guest stats refresh failed on logout: {ex.Message}");
        }

        // Restore the cabinet options file. Guest never uses a profile option
        // overlay, so removing the member session is a plain machine reload.
        Options.RefreshInstance();
        machineSnapshot = PlayerOptions.ExtractFrom(Options.instance);
        AudioManager.instance.ApplyVolume();
        L10n.SetLocale(Options.instance.locale, L10n.Instance.System);

        Debug.Log($"[ProfileManager] Logged out from \"{name}\". Guest session active.");
        profileChanged?.Invoke();
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    // Returns the active profile name, or "Guest" if no session.
    public static string currentProfile()
    {
        return state == SessionState.LoggedIn ? profileName : Paths.kGuestProfileName;
    }

    // Single owner for the process-global record/stat routes. A future API
    // token resolver can establish the same session without changing callers.
    public static void RestoreActiveStorageRoutes()
    {
        if (state == SessionState.LoggedIn && !string.IsNullOrEmpty(profileName))
        {
            Paths.SetRecordsFilePathOverride(
                Paths.GetProfileRecordsFilePath(profileName));
            Paths.SetStatsFilePathOverride(
                Paths.GetProfileStatsFilePath(profileName));
            return;
        }
        Paths.ClearRecordsFilePathOverride();
        Paths.ClearStatsFilePathOverride();
    }

    // Returns all profile names found in Profiles\ (excluding Guest).
    public static List<string> listProfiles()
    {
        List<string> result = new List<string>();
        string profilesFolder = Paths.GetProfilesFolder();
        if (!Directory.Exists(profilesFolder)) return result;

        foreach (string dir in Directory.GetDirectories(profilesFolder))
        {
            string name = Path.GetFileName(dir);
            if (name == Paths.kGuestProfileName) continue;
            if (File.Exists(Path.Combine(dir, "profile.json")))
                result.Add(name);
        }
        return result;
    }

    public static long currentDjExp()
    {
        return state == SessionState.LoggedIn && activeProfileData != null
            ? Math.Max(0, activeProfileData.userExp)
            : Math.Max(0, guestUserExp);
    }

    public static int currentDjLevel()
    {
        return DjProgression.LevelForExp(currentDjExp());
    }

    public static long nextDjLevelExp()
    {
        return DjProgression.MinimumExpForNextLevel(currentDjExp());
    }

    // Called once by GameController at the stage's terminal state transition.
    // Logged-in sessions persist to profile.json; Guest remains session-only.
    public static int AwardDjExperience(int chartLevel, string rank)
    {
        int award = DjProgression.ExpForResult(chartLevel, rank);
        if (award <= 0) return 0;

        if (state != SessionState.LoggedIn || activeProfileData == null)
        {
            guestUserExp = DjProgression.ApplyAward(guestUserExp, chartLevel, rank);
            return award;
        }

        activeProfileData.userExp = DjProgression.ApplyAward(
            activeProfileData.userExp, chartLevel, rank);
        try
        {
            activeProfileData.SaveToFile(Paths.GetProfileDataFilePath(profileName));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Could not save DJ EXP for {profileName}: {ex.Message}");
        }
        return award;
    }

    // Profile folders are case-insensitive on the supported Windows arcade
    // host. Expose the collision before creation so Login can keep the player
    // on the keypad and explain what needs changing.
    public static bool profileNameExists(string name)
    {
        string normalizedName = NormalizeProfileName(name);
        if (string.IsNullOrEmpty(normalizedName)) return false;

        string profilesFolder = Paths.GetProfilesFolder();
        if (!Directory.Exists(profilesFolder)) return false;
        foreach (string dir in Directory.GetDirectories(profilesFolder))
        {
            if (string.Equals(Path.GetFileName(dir), normalizedName,
                StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── Options save hook (called from Options.SaveToFile override) ───────────

    // Extracts and persists player-side options for an authenticated member.
    // Guest uses the machine options file directly and has no options overlay.
    internal static void OnOptionsSaved(Options opts)
    {
        string savePath = GetActivePlayerOptionsSavePath();
        if (string.IsNullOrEmpty(savePath)) return;
        try
        {
            PlayerOptions player = PlayerOptions.ExtractFrom(opts);
            player.SaveToFile(savePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Could not save player options for {profileName}: {ex.Message}");
        }
    }

    private static string GetActivePlayerOptionsSavePath()
    {
        return state == SessionState.LoggedIn && !string.IsNullOrEmpty(profileName)
            ? Paths.GetProfileOptionsFilePath(profileName)
            : null;
    }

    internal static PlayerOptions GetMachinePlayerOptionsSnapshot()
    {
        return machineSnapshot;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FindProfileNameByToken(string cardId)
    {
        string profilesFolder = Paths.GetProfilesFolder();
        if (!Directory.Exists(profilesFolder)) return null;

        foreach (string dir in Directory.GetDirectories(profilesFolder))
        {
            string dataPath = Path.Combine(dir, "profile.json");
            if (!File.Exists(dataPath)) continue;

            try
            {
                ProfileData data = ProfileDataBase.LoadFromFile(
                    dataPath, out bool upgraded) as ProfileData;
                if (data != null && upgraded) data.SaveToFile(dataPath);
                if (ProfileCredentials.Contains(data, cardId))
                    return data.name;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileManager] Could not read profile.json at {dataPath}: {ex.Message}");
            }
        }

        Debug.Log("[ProfileManager] No local profile matched the presented token.");
        return null;
    }

    private static ProfileData LoadProfileData(string name)
    {
        string path = Paths.GetProfileDataFilePath(name);
        if (!File.Exists(path)) return null;
        try
        {
            ProfileData data = ProfileDataBase.LoadFromFile(
                path, out bool upgraded) as ProfileData;
            if (data != null && data.userExp < 0) data.userExp = 0;
            if (data != null && upgraded) data.SaveToFile(path);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Could not load profile data for {name}: {ex.Message}");
            return null;
        }
    }

    // Generates an e-amusement-style cardId: "E00401" + 10 random hex chars (16 total).
    private static string GenerateCardId()
    {
        byte[] bytes = new byte[5]; // 5 bytes = 10 hex chars
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return "E00401" + BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    private static string LegacyCardIdFor(string credentialKey)
    {
        if (string.IsNullOrEmpty(credentialKey)) return credentialKey;
        if (credentialKey.StartsWith(ProfileCredentials.UsbPrefix,
            StringComparison.OrdinalIgnoreCase))
            return credentialKey.Substring(ProfileCredentials.UsbPrefix.Length);
        return credentialKey;
    }
}
