using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Startup : MonoBehaviour
{
    public TextAsset stringTable;
    public AudioManager audioManager;
    public BootScreen bootScreen;

    private static void LoadRuleset()
    {
        if (Options.instance.ruleset != Options.Ruleset.Custom)
        {
            return;
        }

        try
        {
            Ruleset.LoadCustomRuleset();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("An error occurred when loading custom ruleset, reverting to standard ruleset: " + ex.ToString());
            // Silently ignore errors.
            Options.instance.ruleset = Options.Ruleset.Standard;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Input.simulateMouseWithTouches = false;
        // Enable EnhancedTouch so GameInputManager can read per-touch event
        // timestamps for sub-frame tap timing under the Input System.
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        Paths.PrepareFolders();
        // Move legacy records/stats into the Guest profile before any
        // Refresh*Instance call. Ordering is load-bearing: refreshing
        // first would load a blank instance from the empty legacy path
        // and overwrite the migrated Guest data on the first periodic
        // save (StatsMaintainer saves every 30 seconds).
        Paths.MigrateLegacyDataToGuestProfile();
        Options.RefreshInstance();
        Statistics.RefreshInstance();
        Statistics.instance.timesAppLaunched++;
        GetComponent<StatsMaintainer>().BeginWorking();

        FmodManager.instance.Initialize(
            Options.instance.audioBufferSize,
            Options.instance.numAudioBuffers);

        Options.instance.SetDefaultResolutionIfInvalid();
        Options.instance.ApplyGraphicSettings();
        audioManager.ApplyVolume();
        Options.instance.ApplyAsio();
        LoadRuleset();
        
        L10n.Initialize(stringTable.text, L10n.Instance.System);
        L10n.SetLocale(Options.instance.locale, L10n.Instance.System);

        SpriteSheet.PrepareEmptySpriteSheet();
        // Redirect records to the external drive before the first load, if
        // the feature is enabled and the drive is available; the watcher then
        // keeps the source in sync (checked every 8s while idle) for the whole
        // session, including under other themes.
        ExternalRecordsWatcher watcher = gameObject
            .AddComponent<ExternalRecordsWatcher>();
        watcher.ApplyInitialSource();
        Records.RefreshInstance();

        // ProfileManager initialises after everything is loaded.
        // It scans for a USB token if present and stores the pending cardId
        // for the theme to pick up via tm.profile.hasPendingToken().
        ProfileManager.Initialize();

        DiscordController.Start();

#if UNITY_ANDROID
        AndroidUtility.CheckVersion();
        // Ask for storage permission before loading resource
        // if custom data location is set.
        if (Options.instance.customDataLocation)
        {
            StartCoroutine(AndroidUtility.AskForPermissions(
                callback: () =>
                {
                    // Turn off custom data location and reset skins
                    // if user denied permission.
                    // Otherwise, there will be an error while loading skins.
                    if (!AndroidUtility.HasStoragePermissions())
                    {
                        Options.instance.ResetCustomDataLocation();
                    }
                    StartBooting();
                }));
        }
        else
        {
            StartBooting();
        }
#else
        StartBooting();
#endif
    }

    private void StartBooting()
    {
        Paths.ApplyCustomDataLocation();
        BetterStreamingAssets.Initialize();
        bootScreen.StartBooting();
    }

    private void OnApplicationQuit()
    {
        ProfileManager.Shutdown();
    }
}
