using System.Collections.Generic;

// Cabinet-scoped theme options.
//
// Everything the theme stores under tm.options.GetThemeOptions("TECHNIKA 2")
// is a per-profile preference by default, because PlayerOptions.ApplyTo
// replaces that dictionary wholesale on every login. The keys listed here are
// operator settings that belong to the machine instead: the arcade lifecycle,
// the countdown clock, the attract screen and the F2 preview harness. A card
// login must not revert the cabinet to its defaults, so those keys are kept on
// whatever Options instance is being overlaid, and stripped from the profile
// copy so a stale value written into an old profile options.json can never
// come back.
//
// Genuine player preferences (DjName/DjIcon/CrewName, NoCard, the song sorts,
// StarGuideEnabled) are deliberately absent: those still follow the profile.
public static class MachineThemeOptions
{
    public const string ThemeName = HumanPlaytesterSettings.ThemeName;

    private static readonly HashSet<string> machineKeys =
        new HashSet<string>
    {
        // Background presentation.
        "BgScalingMode",
        // Countdown clock.
        "OverrideClock", "CustomClockSeconds", "StopClock", "RemoveClock",
        // Arcade lifecycle.
        "ArcadeMode", "ArcadeRounds", "ArcadeGameOver",
        "ArcadeAttractGameplay", "ArcadeStarTutorial", "ArcadeLoginScreen",
        "ArcadeModeSelectTimer", "ArcadeResultScreenTimer",
        // Attract screen.
        "MuteAttractScreen",
        // Operator-only diagnostics, all reachable from the F2 menu.
        HumanPlaytesterSettings.OptionKey,
        "AllClearPreviewVariant", "AllClearPreviewBadges",
        "AllClearPreviewNewRecord", "AllClearPreviewHotkeys"
    };

    public static ICollection<string> Keys { get { return machineKeys; } }

    public static bool IsMachineScoped(string themeName, string key)
    {
        return themeName == ThemeName && key != null &&
            machineKeys.Contains(key);
    }

    // Reads the cabinet's own values out of a live Options instance, before
    // ApplyTo clears the dictionary. Returns null when nothing is stored, so
    // Restore does not materialise an empty theme dictionary.
    public static Dictionary<string, string> Capture(Options opts)
    {
        if (opts == null || opts.themeOptions == null) return null;
        Dictionary<string, string> themeDict;
        if (!opts.themeOptions.TryGetValue(ThemeName, out themeDict) ||
            themeDict == null) return null;

        Dictionary<string, string> captured = null;
        foreach (string key in machineKeys)
        {
            string value;
            if (!themeDict.TryGetValue(key, out value)) continue;
            if (captured == null)
                captured = new Dictionary<string, string>();
            captured[key] = value;
        }
        return captured;
    }

    // Writes a Capture() result back over whatever the profile supplied.
    public static void Restore(Options opts,
        Dictionary<string, string> captured)
    {
        if (opts == null || captured == null || captured.Count == 0) return;
        Dictionary<string, string> themeDict = opts.GetThemeOptions(ThemeName);
        foreach (var kv in captured)
        {
            themeDict[kv.Key] = kv.Value;
        }
    }

    // Removes the cabinet keys from a profile-bound copy of one theme's
    // dictionary. A no-op for every theme other than TECHNIKA 2.
    public static void StripFrom(string themeName,
        Dictionary<string, string> themeDict)
    {
        if (themeName != ThemeName || themeDict == null) return;
        foreach (string key in machineKeys)
        {
            themeDict.Remove(key);
        }
    }
}
