using System.IO;
using UnityEngine;

// When Options.checkExternalDriveForScores is on, this polls (every 8s, and
// only while the player is idle) whether the drive holding
// Options.externalRecordsPath is available. On each change it redirects
// record loading AND saving to that external file (creating it if missing)
// or back to the local file, reloads Records.instance, and asks the active
// theme to refresh on-screen scores.
//
// It lives in the C# core (hosted by Startup, for the whole session) rather
// than in theme Lua, so the behavior keeps working when the player switches
// themes. The on/off toggle itself lives in the default theme's options.
public class ExternalRecordsWatcher : MonoBehaviour
{
    private const float kCheckIntervalSeconds = 8f;
    private float timer = 0f;

    // Whether the external store is currently active. We only reload/notify
    // on an actual transition, not on every poll.
    private bool currentlyExternal = false;

    // Called once by Startup, before the first Records load: applies the
    // external override (and creates the file) when applicable, WITHOUT
    // reloading (the caller loads next) or notifying (no theme exists yet).
    public void ApplyInitialSource()
    {
        if (ProfileManager.state == ProfileManager.SessionState.LoggedIn)
        {
            ProfileManager.RestoreActiveStorageRoutes();
            return;
        }
        if (Options.instance == null ||
            !Options.instance.checkExternalDriveForScores) return;
        if (!ExternalDriveAvailable()) return;
        if (!TrySetExternalOverride()) return;
        currentlyExternal = true;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < kCheckIntervalSeconds) return;
        timer = 0f;
        Evaluate();
    }

    private void Evaluate()
    {
        if (ProfileManager.state == ProfileManager.SessionState.LoggedIn)
        {
            currentlyExternal = false;
            ProfileManager.RestoreActiveStorageRoutes();
            return;
        }
        // Feature off: make sure we're back on the local store.
        if (Options.instance == null ||
            !Options.instance.checkExternalDriveForScores)
        {
            if (currentlyExternal) SwitchToLocal();
            return;
        }

        // Never swap while a song is loading or playing; resume once idle.
        if (GameController.instance != null &&
            !GameController.instance.IsIdle)
        {
            return;
        }

        bool available = ExternalDriveAvailable();
        if (available && !currentlyExternal)
        {
            SwitchToExternal();
        }
        else if (!available && currentlyExternal)
        {
            SwitchToLocal();
        }
    }

    private bool ExternalDriveAvailable()
    {
        string path = Options.instance.externalRecordsPath;
        if (string.IsNullOrEmpty(path)) return false;
        string root;
        try
        {
            root = Path.GetPathRoot(path);
        }
        catch (System.ArgumentException)
        {
            return false;  // Malformed path (e.g. invalid characters).
        }
        if (string.IsNullOrEmpty(root)) return false;
        return Directory.Exists(root);
    }

    // Points the records path at the external file, creating an empty one if
    // it doesn't exist. Returns false (and reverts) if creation failed.
    private bool TrySetExternalOverride()
    {
        string path = Options.instance.externalRecordsPath;
        Paths.SetRecordsFilePathOverride(path);
        if (!File.Exists(path))
        {
            try
            {
                // SaveToFile routes through GetRecordsFilePath, which now
                // returns the override, so this creates D:\records.json.
                new Records().SaveToFile();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("ExternalRecordsWatcher: could not create " +
                    "external records file at " + path + ": " + ex);
                Paths.ClearRecordsFilePathOverride();
                return false;
            }
        }
        return true;
    }

    private void SwitchToExternal()
    {
        if (!TrySetExternalOverride()) return;
        Records.RefreshInstance();
        currentlyExternal = true;
        ThemeApi.Techmania.instance?.InvokeRecordsReloaded();
    }

    private void SwitchToLocal()
    {
        ProfileManager.RestoreActiveStorageRoutes();
        Records.RefreshInstance();
        currentlyExternal = false;
        ThemeApi.Techmania.instance?.InvokeRecordsReloaded();
    }
}
