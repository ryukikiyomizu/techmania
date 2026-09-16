using System.Collections.Generic;
using NUnit.Framework;

// The cabinet's operator settings live in the same TECHNIKA 2 theme dictionary
// as the player's own preferences, and PlayerOptions.ApplyTo replaces that
// dictionary on every login. Without the MachineThemeOptions carve-out a card
// login silently reverted the countdown to its 30 s default and turned the
// arcade lifecycle back off.
public class MachineThemeOptionsTests
{
    private const string kTheme = MachineThemeOptions.ThemeName;

    // A cabinet configured by the operator: 45 s clock, arcade on, result
    // timer off, plus one genuine player preference.
    private static Options MachineWithOperatorSettings()
    {
        Options machine = new Options();
        Dictionary<string, string> theme = machine.GetThemeOptions(kTheme);
        theme["CustomClockSeconds"] = "45";
        theme["OverrideClock"] = "True";
        theme["ArcadeMode"] = "True";
        theme["ArcadeResultScreenTimer"] = "False";
        theme["MuteAttractScreen"] = "True";
        theme["BgScalingMode"] = "FillGameArea";
        theme["HumanPlaytester"] = "True";
        theme["DjName"] = "CABINET";
        return machine;
    }

    // A profile written before the carve-out existed: it carries a full copy of
    // the theme dictionary, including defaults for the operator keys.
    private static PlayerOptions StaleProfile()
    {
        PlayerOptions profile = new PlayerOptions();
        // Avoid DiscordController.Start(), which dereferences Options.instance.
        profile.discordRichPresence = false;
        profile.themeOptions[kTheme] = new Dictionary<string, string>
        {
            { "CustomClockSeconds", "30" },
            { "OverrideClock", "False" },
            { "ArcadeMode", "False" },
            { "ArcadeResultScreenTimer", "True" },
            { "MuteAttractScreen", "False" },
            { "BgScalingMode", "FillEntireScreen" },
            { "HumanPlaytester", "False" },
            { "DjName", "PLAYER" },
            { "StarSongSort", "Title" }
        };
        return profile;
    }

    [Test]
    public void CardLoginKeepsTheOperatorClockAndArcadeSettings()
    {
        Options machine = MachineWithOperatorSettings();
        StaleProfile().ApplyTo(machine);

        Dictionary<string, string> theme = machine.GetThemeOptions(kTheme);
        Assert.AreEqual("45", theme["CustomClockSeconds"],
            "A card login reverted the operator's countdown length.");
        Assert.AreEqual("True", theme["OverrideClock"]);
        Assert.AreEqual("True", theme["ArcadeMode"],
            "A card login turned arcade mode back off.");
        Assert.AreEqual("False", theme["ArcadeResultScreenTimer"]);
        Assert.AreEqual("True", theme["MuteAttractScreen"]);
        Assert.AreEqual("FillGameArea", theme["BgScalingMode"]);
        Assert.AreEqual("True", theme["HumanPlaytester"]);
    }

    [Test]
    public void CardLoginStillAppliesGenuinePlayerPreferences()
    {
        Options machine = MachineWithOperatorSettings();
        StaleProfile().ApplyTo(machine);

        Dictionary<string, string> theme = machine.GetThemeOptions(kTheme);
        Assert.AreEqual("PLAYER", theme["DjName"],
            "The profile's own DJ name was not applied.");
        Assert.AreEqual("Title", theme["StarSongSort"],
            "The profile's own song sort was not applied.");
    }

    [Test]
    public void LoginOntoAnUnconfiguredCabinetTakesNothingFromTheProfile()
    {
        // Nothing seeded yet: the profile must not become the source of truth
        // for operator settings just because the cabinet has none.
        Options machine = new Options();
        StaleProfile().ApplyTo(machine);

        Dictionary<string, string> theme = machine.GetThemeOptions(kTheme);
        Assert.IsFalse(theme.ContainsKey("CustomClockSeconds"),
            "An operator key leaked out of a profile onto a fresh cabinet.");
        Assert.IsFalse(theme.ContainsKey("ArcadeMode"));
        Assert.AreEqual("PLAYER", theme["DjName"]);
    }

    [Test]
    public void ProfileSaveDoesNotCaptureOperatorSettings()
    {
        Options machine = MachineWithOperatorSettings();
        PlayerOptions saved = PlayerOptions.ExtractFrom(machine);

        Dictionary<string, string> theme = saved.themeOptions[kTheme];
        foreach (string key in MachineThemeOptions.Keys)
        {
            Assert.IsFalse(theme.ContainsKey(key),
                $"Operator key {key} was written into a profile.");
        }
        Assert.AreEqual("CABINET", theme["DjName"],
            "Saving a profile dropped its own theme options.");
    }

    [Test]
    public void OperatorEditsDuringAMemberSessionReachTheMachineFile()
    {
        // Reproduces Options.SaveToFile()'s member-session path: the live
        // options are cloned, the pre-login machine snapshot is overlaid, and
        // the result becomes the machine file.
        Options machine = MachineWithOperatorSettings();
        PlayerOptions machineSnapshot = PlayerOptions.ExtractFrom(machine);
        machineSnapshot.discordRichPresence = false;

        Options live = MachineWithOperatorSettings();
        live.GetThemeOptions(kTheme)["CustomClockSeconds"] = "60";
        live.GetThemeOptions(kTheme)["ArcadeMode"] = "False";
        machineSnapshot.ApplyTo(live);

        Assert.AreEqual("60", live.GetThemeOptions(kTheme)["CustomClockSeconds"],
            "An operator's clock edit was lost while a member was logged in.");
        Assert.AreEqual("False", live.GetThemeOptions(kTheme)["ArcadeMode"]);
    }

    [Test]
    public void OtherThemesAreUnaffected()
    {
        Options machine = new Options();
        machine.GetThemeOptions("Default")["ArcadeMode"] = "True";

        PlayerOptions profile = new PlayerOptions();
        profile.discordRichPresence = false;
        profile.themeOptions["Default"] = new Dictionary<string, string>
        {
            { "ArcadeMode", "False" }
        };
        profile.ApplyTo(machine);

        Assert.AreEqual("False", machine.GetThemeOptions("Default")["ArcadeMode"],
            "A same-named key in another theme was treated as cabinet-scoped.");
    }
}
