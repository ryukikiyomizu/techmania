using System;
using System.Collections.Generic;

// options.json inside a profile folder — player-side preferences only.
// Machine settings (graphics, audio hw, data paths, latency) stay in
// Documents\TECHMANIA\options.json and are never overlaid by this class.
//
// At login:  PlayerOptions.ApplyTo(Options.instance) overlays these fields.
// At save:   PlayerOptions.ExtractFrom(Options.instance) captures changes.
// At logout: Options.RefreshInstance() reloads the machine file (no overlay).

[Serializable]
[FormatVersion(PlayerOptions.kVersion, typeof(PlayerOptions), isLatest: true)]
public class PlayerOptionsBase : SerializableClass<PlayerOptionsBase>
{
    // Path is determined by ProfileManager; no SaveToFile(string) needed here.
}

[Serializable]
[MoonSharp.Interpreter.MoonSharpUserData]
public class PlayerOptions : PlayerOptionsBase
{
    public const string kVersion = "1";

    // ── Audio volumes ─────────────────────────────────────────────────────────
    public int masterVolumePercent;
    public int musicVolumePercent;
    public int keysoundVolumePercent;
    public int sfxVolumePercent;

    // ── Appearance / skins ────────────────────────────────────────────────────
    public string locale;
    public string noteSkin;
    public string vfxSkin;
    public string comboSkin;
    public string gameUiSkin;
    public bool reloadSkinsWhenLoadingPattern;
    public Options.NoteSize noteSize;

    // ── Timing ────────────────────────────────────────────────────────────────
    public int touchOffsetMs;
    public int touchLatencyMs;
    public int keyboardMouseOffsetMs;
    public int keyboardMouseLatencyMs;

    // ── Gameplay preferences ──────────────────────────────────────────────────
    public Options.Ruleset ruleset;
    public bool discordRichPresence;
    public Modifiers modifiers;

    // Per-track and per-theme options
    public Dictionary<string, PerTrackOptions> perTrackOptions;
    public Dictionary<string, Dictionary<string, string>> themeOptions;

    public PlayerOptions()
    {
        version = kVersion;
        locale = L10n.kDefaultLocale;
        noteSkin = Options.kDefaultSkin;
        vfxSkin = Options.kDefaultSkin;
        comboSkin = Options.kDefaultSkin;
        gameUiSkin = Options.kDefaultSkin;
        noteSize = Options.NoteSize.Big;
        masterVolumePercent = 100;
        musicVolumePercent = 80;
        keysoundVolumePercent = 100;
        sfxVolumePercent = 100;
        ruleset = Options.Ruleset.Standard;
        discordRichPresence = true;
        modifiers = new Modifiers();
        perTrackOptions = new Dictionary<string, PerTrackOptions>();
        themeOptions = new Dictionary<string, Dictionary<string, string>>();
    }

    // Extract player-side fields from a live Options instance.
    public static PlayerOptions ExtractFrom(Options opts)
    {
        PlayerOptions p = new PlayerOptions();
        p.masterVolumePercent = opts.masterVolumePercent;
        p.musicVolumePercent = opts.musicVolumePercent;
        p.keysoundVolumePercent = opts.keysoundVolumePercent;
        p.sfxVolumePercent = opts.sfxVolumePercent;
        p.locale = opts.locale;
        p.noteSkin = opts.noteSkin;
        p.vfxSkin = opts.vfxSkin;
        p.comboSkin = opts.comboSkin;
        p.gameUiSkin = opts.gameUiSkin;
        p.reloadSkinsWhenLoadingPattern = opts.reloadSkinsWhenLoadingPattern;
        p.noteSize = opts.noteSize;
        p.touchOffsetMs = opts.touchOffsetMs;
        p.touchLatencyMs = opts.touchLatencyMs;
        p.keyboardMouseOffsetMs = opts.keyboardMouseOffsetMs;
        p.keyboardMouseLatencyMs = opts.keyboardMouseLatencyMs;
        p.ruleset = opts.ruleset;
        p.discordRichPresence = opts.discordRichPresence;
        p.modifiers = opts.modifiers;

        p.perTrackOptions = new Dictionary<string, PerTrackOptions>();
        if (opts.inMemoryPerTrackOptions != null)
        {
            foreach (var kv in opts.inMemoryPerTrackOptions)
                p.perTrackOptions[kv.Key] = kv.Value;
        }

        p.themeOptions = new Dictionary<string, Dictionary<string, string>>();
        if (opts.themeOptions != null)
        {
            foreach (var kv in opts.themeOptions)
                p.themeOptions[kv.Key] =
                    new Dictionary<string, string>(kv.Value);
        }

        return p;
    }

    // Overlay player fields onto a live Options instance.
    // Call AudioManager.instance.ApplyVolume() and L10n.SetLocale() after this
    // if the change needs to take immediate effect.
    public void ApplyTo(Options opts)
    {
        ApplyTo(opts, true);
    }

    internal void ApplyTo(Options opts, bool applySideEffects)
    {
        opts.masterVolumePercent = masterVolumePercent;
        opts.musicVolumePercent = musicVolumePercent;
        opts.keysoundVolumePercent = keysoundVolumePercent;
        opts.sfxVolumePercent = sfxVolumePercent;
        opts.locale = locale;
        opts.noteSkin = noteSkin;
        opts.vfxSkin = vfxSkin;
        opts.comboSkin = comboSkin;
        opts.gameUiSkin = gameUiSkin;
        opts.reloadSkinsWhenLoadingPattern = reloadSkinsWhenLoadingPattern;
        opts.noteSize = noteSize;
        opts.touchOffsetMs = touchOffsetMs;
        opts.touchLatencyMs = touchLatencyMs;
        opts.keyboardMouseOffsetMs = keyboardMouseOffsetMs;
        opts.keyboardMouseLatencyMs = keyboardMouseLatencyMs;
        opts.ruleset = ruleset;
        if (applySideEffects)
        {
            // Use the proper mutators so Discord presence takes effect immediately.
            if (discordRichPresence) opts.TurnOnDiscordRichPresence();
            else opts.TurnOffDiscordRichPresence();
        }
        else
        {
            opts.discordRichPresence = discordRichPresence;
        }
        opts.modifiers = modifiers;

        opts.inMemoryPerTrackOptions = new Dictionary<string, PerTrackOptions>(
            perTrackOptions);

        // Replace entirely — clear stale keys before merging so the previous
        // player's theme options don't bleed into this session.
        if (opts.themeOptions == null)
            opts.themeOptions = new Dictionary<string, Dictionary<string, string>>();
        opts.themeOptions.Clear();
        foreach (var kv in themeOptions)
            opts.themeOptions[kv.Key] =
                new Dictionary<string, string>(kv.Value);
    }
}
