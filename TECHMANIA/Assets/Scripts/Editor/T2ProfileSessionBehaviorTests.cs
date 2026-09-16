using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

public static class T2ProfileSessionBehaviorTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("TECHMANIA/Tests/T2 Profile Session Behavior")]
    public static void Run()
    {
        ThemeOptionsFollowTheActiveProfile();
        GuestUsesMachineOptionsAndMemberUsesProfileOptions();
        LegacyGuestOptionsMigrateToMachineOnlyOnce();
        GuestBaselineIsCapturedAfterLegacyMigration();
        RepeatedSessionsKeepDistinctStorageOwners();
        CachedSongSelectRecordsFollowTheActiveProfile();
        SongSelectRecordPresentationFollowsTheActiveProfile();
        ExternalRecordFallbackPreservesTheActiveProfile();
        RemovedCardDoesNotEndTheAuthenticatedSession();
        UsbCandidatePolicyAcceptsReadyRemovableMedia();
        Debug.Log("[T2 Profile Test] Active profile, Guide preference, score routing, and card-session contracts passed.");
    }

    private static void GuestBaselineIsCapturedAfterLegacyMigration()
    {
        MethodInfo prepare = typeof(ProfileManager).GetMethod(
            "PrepareGuestMachineOptions", PrivateStatic);
        Require(prepare != null,
            "Guest's in-memory baseline can be captured before legacy migration.");
        if (prepare == null) return;

        string folder = Path.Combine(Path.GetTempPath(),
            "t2-guest-baseline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string legacyPath = Path.Combine(folder, "legacy.json");
        string machinePath = Path.Combine(folder, "machine.json");
        string markerPath = Path.Combine(folder, "marker");
        try
        {
            new PlayerOptions
            {
                musicVolumePercent = 42,
                noteSkin = "GUEST-BASELINE"
            }.SaveToFile(legacyPath);
            var contaminated = new Options
            {
                musicVolumePercent = 7,
                noteSkin = "PREVIOUS-CARD"
            };
            PlayerOptions baseline = prepare.Invoke(null, new object[]
            {
                contaminated, legacyPath, machinePath, markerPath
            }) as PlayerOptions;
            Require(baseline != null && baseline.musicVolumePercent == 42 &&
                    baseline.noteSkin == "GUEST-BASELINE",
                "The running cabinet retained the previous card as Guest's baseline.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static void LegacyGuestOptionsMigrateToMachineOnlyOnce()
    {
        MethodInfo migrate = typeof(ProfileManager).GetMethod(
            "TryMigrateLegacyGuestOptions", PrivateStatic);
        Require(migrate != null,
            "A contaminated cabinet has no one-time Guest option migration.");
        if (migrate == null) return;

        string folder = Path.Combine(Path.GetTempPath(),
            "t2-option-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string legacyPath = Path.Combine(folder, "legacy-guest.json");
        string machinePath = Path.Combine(folder, "machine.json");
        string markerPath = Path.Combine(folder, "migration.done");
        try
        {
            var legacy = new PlayerOptions
            {
                musicVolumePercent = 63,
                noteSkin = "GUEST-NOTES",
                themeOptions = new Dictionary<string,
                    Dictionary<string, string>>
                {
                    ["TECHNIKA 2"] = new Dictionary<string, string>
                    {
                        ["NoCard"] = "True"
                    }
                }
            };
            legacy.SaveToFile(legacyPath);
            var machine = new Options
            {
                musicVolumePercent = 17,
                noteSkin = "CARD-A-NOTES"
            };

            bool migrated = (bool)migrate.Invoke(null, new object[]
            {
                machine, legacyPath, machinePath, markerPath
            });
            Options saved = OptionsBase.LoadFromFile(machinePath) as Options;
            Require(migrated && File.Exists(markerPath) && saved != null &&
                    saved.musicVolumePercent == 63 &&
                    saved.noteSkin == "GUEST-NOTES" &&
                    saved.GetThemeOptions("TECHNIKA 2")["NoCard"] == "True",
                "Legacy Guest options did not become the cabinet baseline.");

            legacy.musicVolumePercent = 5;
            legacy.SaveToFile(legacyPath);
            bool repeated = (bool)migrate.Invoke(null, new object[]
            {
                machine, legacyPath, machinePath, markerPath
            });
            Options unchanged = OptionsBase.LoadFromFile(machinePath) as Options;
            Require(!repeated && unchanged.musicVolumePercent == 63,
                "Legacy Guest migration overwrote a cabinet more than once.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static void RepeatedSessionsKeepDistinctStorageOwners()
    {
        Paths.PrepareFolders();
        FieldInfo stateField = typeof(ProfileManager).GetField(
            "<state>k__BackingField", PrivateStatic);
        FieldInfo nameField = typeof(ProfileManager).GetField(
            "<profileName>k__BackingField", PrivateStatic);
        Require(stateField != null && nameField != null,
            "Repeated arcade sessions cannot select distinct storage owners.");
        if (stateField == null || nameField == null) return;

        try
        {
            stateField.SetValue(null, ProfileManager.SessionState.Guest);
            nameField.SetValue(null, null);
            ProfileManager.RestoreActiveStorageRoutes();
            Require(Paths.GetRecordsFilePath() ==
                    Paths.GetProfileRecordsFilePath(Paths.kGuestProfileName),
                "Guest did not reclaim its record store after a member session.");

            stateField.SetValue(null, ProfileManager.SessionState.LoggedIn);
            nameField.SetValue(null, "__T2_Card_A__");
            ProfileManager.RestoreActiveStorageRoutes();
            Require(Paths.GetRecordsFilePath() ==
                    Paths.GetProfileRecordsFilePath("__T2_Card_A__"),
                "Card A did not receive its own record store.");

            stateField.SetValue(null, ProfileManager.SessionState.Guest);
            nameField.SetValue(null, null);
            ProfileManager.RestoreActiveStorageRoutes();
            Require(Paths.GetRecordsFilePath() ==
                    Paths.GetProfileRecordsFilePath(Paths.kGuestProfileName),
                "Guest retained Card A's record store.");

            stateField.SetValue(null, ProfileManager.SessionState.LoggedIn);
            nameField.SetValue(null, "__T2_Card_B__");
            ProfileManager.RestoreActiveStorageRoutes();
            Require(Paths.GetRecordsFilePath() ==
                    Paths.GetProfileRecordsFilePath("__T2_Card_B__"),
                "Card B reused the previous session's record store.");
        }
        finally
        {
            stateField.SetValue(null, ProfileManager.SessionState.Guest);
            nameField.SetValue(null, null);
            ProfileManager.RestoreActiveStorageRoutes();
        }
    }

    private static void SongSelectRecordPresentationFollowsTheActiveProfile()
    {
        string path = Path.Combine(Application.dataPath, "UI", "Scripts",
            "Active Profile Records.txt");
        Require(File.Exists(path),
            "Song Select has no active-profile record presentation resolver.");
        if (!File.Exists(path)) return;

        var script = new Script(CoreModules.Preset_SoftSandbox);
        script.DoString(File.ReadAllText(path), null,
            "Active Profile Records.txt");
        Table resolver = script.Globals.Get("activeProfileRecords").Table;

        var snapshots = new Table(script);
        var guest = new Table(script);
        var cardA = new Table(script);
        var cardB = new Table(script);
        guest["chart"] = Snapshot(script, 286063, 323, 0);
        cardA["chart"] = Snapshot(script, 295561, 518, 1);
        cardB["chart"] = Snapshot(script, 271111, 222, 0);
        snapshots["Guest"] = guest;
        snapshots["CARD_A"] = cardA;
        snapshots["CARD_B"] = cardB;

        DynValue emptyPattern = DynValue.NewTable(new Table(script));
        Table a = script.Call(resolver.Get("Resolve"), emptyPattern,
            DynValue.NewTable(snapshots), DynValue.NewString("CARD_A"),
            DynValue.NewString("chart")).Table;
        Table returnedGuest = script.Call(resolver.Get("Resolve"), emptyPattern,
            DynValue.NewTable(snapshots), DynValue.NewString("Guest"),
            DynValue.NewString("chart")).Table;
        Table b = script.Call(resolver.Get("Resolve"), emptyPattern,
            DynValue.NewTable(snapshots), DynValue.NewString("CARD_B"),
            DynValue.NewString("chart")).Table;

        Require((int)a.Get("score").Number == 295561 &&
                (int)returnedGuest.Get("score").Number == 286063 &&
                (int)b.Get("score").Number == 271111,
            "Song Select reused another profile's score during an in-process session swap.");
        string trophy = script.Call(resolver.Get("TrophyType"),
            DynValue.NewTable(a)).String;
        Require(trophy == "allcombo",
            "A member medal no longer produces its Song Select trophy.");

        Table missing = script.Call(resolver.Get("Resolve"), emptyPattern,
            DynValue.NewTable(snapshots), DynValue.NewString("CARD_A"),
            DynValue.NewString("guest-only-chart")).Table;
        Require((int)missing.Get("score").Number == 0,
            "Guest's best score leaked into a member's BEST RECORD.");
    }

    private static DynValue Snapshot(Script script, int score, int combo,
        int medal)
    {
        var table = new Table(script);
        table["score"] = score;
        table["maxCombo"] = combo;
        table["medal"] = medal;
        return DynValue.NewTable(table);
    }

    private static void CachedSongSelectRecordsFollowTheActiveProfile()
    {
        const string guid = "t2-profile-record-contract";
        const string fingerprint = "member-record-fingerprint";
        Records guestRecords = RecordsWith(
            guid, fingerprint, 111111, 111, PerformanceMedal.NoMedal);
        Record cachedGuestRecord = guestRecords.records[0];
        Records memberRecords = RecordsWith(
            guid, fingerprint, 299999, 999, PerformanceMedal.AllCombo);

        MethodInfo identityLookup = typeof(Records).GetMethod(
            "GetRecordByIdentity", BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(string), typeof(Options.Ruleset) },
            null);
        Require(identityLookup != null,
            "Song Select cannot resolve a cached chart against the active Records instance.");
        if (identityLookup == null) return;

        Record activeRecord = identityLookup.Invoke(memberRecords, new object[]
        {
            guid, fingerprint, Options.Ruleset.Standard
        }) as Record;
        Require(cachedGuestRecord != null && cachedGuestRecord.score == 111111 &&
                activeRecord != null && activeRecord.score == 299999,
            "Song Select record resolution stayed attached to the Guest record object.");
    }

    private static void GuestUsesMachineOptionsAndMemberUsesProfileOptions()
    {
        Paths.PrepareFolders();
        FieldInfo stateField = typeof(ProfileManager).GetField(
            "<state>k__BackingField", PrivateStatic);
        FieldInfo nameField = typeof(ProfileManager).GetField(
            "<profileName>k__BackingField", PrivateStatic);
        MethodInfo savePath = typeof(ProfileManager).GetMethod(
            "GetActivePlayerOptionsSavePath", PrivateStatic);
        Require(stateField != null && nameField != null && savePath != null,
            "Guest and member option ownership is not explicitly separated.");
        if (stateField == null || nameField == null || savePath == null) return;

        try
        {
            stateField.SetValue(null, ProfileManager.SessionState.Guest);
            nameField.SetValue(null, null);
            Require(savePath.Invoke(null, null) == null,
                "Guest options were routed into a profile instead of the machine file.");

            const string member = "__T2_Options_Owner_Test__";
            stateField.SetValue(null, ProfileManager.SessionState.LoggedIn);
            nameField.SetValue(null, member);
            Require((string)savePath.Invoke(null, null) ==
                    Paths.GetProfileOptionsFilePath(member),
                "Member options were not routed to the active profile folder.");
        }
        finally
        {
            stateField.SetValue(null, ProfileManager.SessionState.Guest);
            nameField.SetValue(null, null);
        }
    }

    private static Records RecordsWith(string guid, string fingerprint,
        int score, int maxCombo, PerformanceMedal medal)
    {
        var records = new Records();
        records.records.Add(new Record
        {
            guid = guid,
            fingerprint = fingerprint,
            ruleset = Options.Ruleset.Standard,
            score = score,
            maxCombo = maxCombo,
            medal = medal
        });
        MethodInfo initialize = typeof(Records).GetMethod(
            "InitAfterDeserialize", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(initialize != null, "Records deserialization initializer is unavailable.");
        initialize?.Invoke(records, null);
        return records;
    }

    private static void ThemeOptionsFollowTheActiveProfile()
    {
        var guest = new Dictionary<string, string>
        {
            ["StarGuideEnabled"] = "False"
        };
        var member = new Dictionary<string, string>
        {
            ["StarGuideEnabled"] = "True"
        };
        int activeProfile = 0;

        UserData.RegisterType<Dictionary<string, string>>();
        var script = new Script(CoreModules.Preset_SoftSandbox);
        var tm = new Table(script);
        var options = new Table(script);
        options["GetThemeOptions"] = DynValue.NewCallback((_, __) =>
            DynValue.FromObject(script, activeProfile == 0 ? guest : member));
        options["SaveToFile"] = DynValue.NewCallback((_, __) => DynValue.Nil);
        tm["options"] = options;
        script.Globals["tm"] = tm;

        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "UI", "Scripts", "Theme Options.txt"));
        script.DoString(source, null, "Theme Options.txt");
        Table themeOptions = script.Globals.Get("themeOptions").Table;

        activeProfile = 1;
        bool loaded = script.Call(themeOptions.Get("GetBoolWithDefault"),
            DynValue.NewString("StarGuideEnabled"), DynValue.False).Boolean;
        Require(loaded,
            "Theme options still read the detached Guest dictionary after member login.");

        script.Call(themeOptions.Get("SetBool"),
            DynValue.NewString("StarGuideEnabled"), DynValue.False);
        Require(member["StarGuideEnabled"] == "False" &&
                guest["StarGuideEnabled"] == "False",
            "Guide changes were not written to the active member profile.");
    }

    private static void ExternalRecordFallbackPreservesTheActiveProfile()
    {
        Paths.PrepareFolders();
        const string profile = "__T2_Profile_Route_Test__";
        FieldInfo stateField = typeof(ProfileManager).GetField(
            "<state>k__BackingField", PrivateStatic);
        FieldInfo nameField = typeof(ProfileManager).GetField(
            "<profileName>k__BackingField", PrivateStatic);
        Require(stateField != null && nameField != null,
            "ProfileManager session backing fields are unavailable to the contract.");

        GameObject host = null;
        try
        {
            stateField.SetValue(null, ProfileManager.SessionState.LoggedIn);
            nameField.SetValue(null, profile);
            Paths.SetRecordsFilePathOverride(Path.Combine(
                Path.GetTempPath(), "external-records.json"));

            host = new GameObject("T2 Profile Route Contract");
            ExternalRecordsWatcher watcher =
                host.AddComponent<ExternalRecordsWatcher>();
            MethodInfo switchToLocal = typeof(ExternalRecordsWatcher).GetMethod(
                "SwitchToLocal", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(switchToLocal != null,
                "ExternalRecordsWatcher local fallback is unavailable.");
            switchToLocal.Invoke(watcher, null);

            Require(Paths.GetRecordsFilePath() ==
                    Paths.GetProfileRecordsFilePath(profile),
                "External record fallback discarded the authenticated profile route.");
        }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            stateField?.SetValue(null, ProfileManager.SessionState.Guest);
            nameField?.SetValue(null, null);
            Paths.ClearRecordsFilePathOverride();
            Paths.ClearStatsFilePathOverride();
        }
    }

    private static void RemovedCardDoesNotEndTheAuthenticatedSession()
    {
        FieldInfo stateField = typeof(ProfileManager).GetField(
            "<state>k__BackingField", PrivateStatic);
        FieldInfo cardField = typeof(ProfileManager).GetField(
            "sessionCardId", PrivateStatic);
        FieldInfo presentField = typeof(ProfileManager).GetField(
            "sessionCardPresent", PrivateStatic);
        MethodInfo hasSessionCard = typeof(ProfileManager).GetMethod(
            "hasSessionCard", BindingFlags.Public | BindingFlags.Static);
        MethodInfo pollSessionCardPresence = typeof(ProfileManager).GetMethod(
            "pollSessionCardPresence", BindingFlags.Public | BindingFlags.Static);

        Require(cardField != null && presentField != null &&
                hasSessionCard != null && pollSessionCardPresence != null,
            "ProfileManager does not expose a detached authenticated card session.");
        if (cardField == null || presentField == null ||
            hasSessionCard == null || stateField == null) return;

        try
        {
            stateField.SetValue(null, ProfileManager.SessionState.LoggedIn);
            cardField.SetValue(null, "verification-card");
            presentField.SetValue(null, false);
            bool authenticated = (bool)hasSessionCard.Invoke(null, null);
            Require(authenticated,
                "Removing the physical USB also removed the authenticated card identity.");
        }
        finally
        {
            cardField.SetValue(null, null);
            presentField.SetValue(null, false);
            stateField?.SetValue(null, ProfileManager.SessionState.Guest);
        }
    }

    private static void UsbCandidatePolicyAcceptsReadyRemovableMedia()
    {
        MethodInfo policy = typeof(ProfileManager).GetMethod(
            "IsUsableUsbVolume", PrivateStatic);
        Require(policy != null,
            "USB discovery has no testable ready-volume policy.");
        if (policy == null) return;

        bool Ready(DriveType type, bool ready, bool usbBus)
        {
            return (bool)policy.Invoke(null, new object[]
            {
                type, ready, usbBus
            });
        }

        Require(Ready(DriveType.Removable, true, false),
            "A ready removable USB still depends on a low-level bus query.");
        Require(!Ready(DriveType.Removable, false, true),
            "An unreadable RAW/not-ready USB was treated as a usable profile volume.");
        Require(Ready(DriveType.Fixed, true, true),
            "A ready fixed-type USB enclosure was rejected.");
        Require(!Ready(DriveType.Fixed, true, false),
            "An internal fixed disk was treated as a profile card.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
