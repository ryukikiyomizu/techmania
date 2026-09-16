using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildThemeWhenSourcesChange
{
    private const string SessionKey = "TECHMANIA.AutoBuildThemeWhenSourcesChange.LastBuildAttemptUtc";

    static AutoBuildThemeWhenSourcesChange()
    {
        EditorApplication.delayCall += BuildIfThemeSourcesAreNewer;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void BuildIfThemeSourcesAreNewer()
    {
        BuildIfThemeSourcesAreNewer(false);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            BuildIfThemeSourcesAreNewer(true);
        }
    }

    private static void BuildIfThemeSourcesAreNewer(bool allowWhileEnteringPlayMode)
    {
        if (!allowWhileEnteringPlayMode && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string defaultBundlePath = Path.Combine(
            Paths.kAssetBundleFolder, Paths.kDefaultBundleName);
        DateTime bundleWriteTime = File.Exists(defaultBundlePath)
            ? File.GetLastWriteTimeUtc(defaultBundlePath)
            : DateTime.MinValue;

        DateTime newestSourceWriteTime = DateTime.MinValue;
        foreach (string folder in new[] { "Assets/UI", "Assets/T3" })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".uxml" && extension != ".uss" && extension != ".txt" &&
                    extension != ".png" && extension != ".jpg" && extension != ".mp4" &&
                    extension != ".ogg" && extension != ".wav")
                {
                    continue;
                }

                DateTime writeTime = File.GetLastWriteTimeUtc(file);
                if (writeTime > newestSourceWriteTime)
                {
                    newestSourceWriteTime = writeTime;
                }
            }
        }

        if (newestSourceWriteTime <= bundleWriteTime)
        {
            return;
        }

        string lastAttemptRaw = SessionState.GetString(SessionKey, "");
        if (DateTime.TryParse(lastAttemptRaw, out DateTime lastAttempt) &&
            DateTime.UtcNow - lastAttempt < TimeSpan.FromSeconds(15))
        {
            return;
        }
        SessionState.SetString(SessionKey, DateTime.UtcNow.ToString("O"));

        Debug.Log($"[Theme] Source files are newer than default bundle. Rebuilding theme now. Source={newestSourceWriteTime:O}, Bundle={bundleWriteTime:O}");
        BuildAssetBundleWindow.BuildForDefaultPlatform();
    }
}
