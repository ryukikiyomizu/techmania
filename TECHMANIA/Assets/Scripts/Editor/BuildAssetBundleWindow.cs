using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Text;
using UnityEditor.Build;

public class BuildAssetBundleWindow : EditorWindow
{
    [Serializable]
    private class SessionRecordFile
    {
        public List<SessionRecordEntry> records;
    }

    [Serializable]
    private class SessionRecordEntry
    {
        public string guid;
        public string fingerprint;
        public int score;
        public int maxCombo;
        public PerformanceMedal medal;
    }

    private BuildTarget buildTarget;
    private bool noCompression;

    [MenuItem("Window/TECHMANIA Theme/Build now")]
    public static void BuildForDefaultPlatform()
    {
        BuildAssetBundle(EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("Window/TECHMANIA Theme/Build...")]
    private static void Init()
    {
        BuildAssetBundleWindow window =
            GetWindow<BuildAssetBundleWindow>();
        window.buildTarget = EditorUserBuildSettings.activeBuildTarget;
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Target platform:");
        buildTarget = (BuildTarget)EditorGUILayout.EnumPopup(
            buildTarget);
        noCompression = EditorGUILayout.ToggleLeft("Don't compress images (overrides import settings)", noCompression);

        if (GUILayout.Button("Build"))
        {
            BuildAssetBundle(buildTarget, noCompression);
        }
    }

    private static void BuildAssetBundle(BuildTarget target,
        bool noCompression = false)
    {
        if (noCompression)
        {
            EditorUserBuildSettings.overrideTextureCompression
                = OverrideTextureCompression.ForceUncompressed;
        }
        Debug.Log($"Building theme for {target} ...");
        GenerateSessionRecordSnapshot();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        AssignT3Bundle.Assign();
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Paths.kAssetBundleFolder);
        AssetBundleManifest manifest =
            BuildPipeline.BuildAssetBundles(
                Paths.kAssetBundleFolder,
                BuildAssetBundleOptions.ForceRebuildAssetBundle, target);
        if (manifest == null)
        {
            throw new System.Exception("AssetBundle build failed before Unity returned a manifest.");
        }
        CopyDefaultBundleToSelectedTechnikaTheme();
        foreach (string bundleName in manifest.GetAllAssetBundles())
        {
            Debug.Log($"Built theme at {Path.Combine(Paths.kAssetBundleFolder, bundleName)}.");
        }
        if (noCompression)
        {
            EditorUserBuildSettings.overrideTextureCompression
                = OverrideTextureCompression.NoOverride;
        }
    }

    // The June _TestBuild3 runtime predates Record.maxCombo in the Lua API.
    // Package a read-only snapshot of every local profile's real records so
    // the T2 popup can still show BEST / MY / MAX COMBO accurately.
    private static void GenerateSessionRecordSnapshot()
    {
        string outputPath = Path.Combine(
            Application.dataPath, "UI", "Data", "session_records.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        StringBuilder output = new StringBuilder();
        output.AppendLine("# profile\tguid\tfingerprint\tscore\tmaxCombo\tmedal");

        // Paths.dataFolder is initialized by the running game, not by this
        // editor window. Build the default test-runtime location explicitly.
        string profilesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TECHMANIA", Paths.kProfilesFolderName);
        if (Directory.Exists(profilesFolder))
        {
            string[] profileFolders = Directory.GetDirectories(profilesFolder);
            Array.Sort(profileFolders, StringComparer.OrdinalIgnoreCase);
            foreach (string profileFolder in profileFolders)
            {
                string recordsPath = Path.Combine(profileFolder, "records.json");
                if (!File.Exists(recordsPath)) continue;

                SessionRecordFile recordFile;
                try
                {
                    recordFile = Json.Deserialize<SessionRecordFile>(
                        File.ReadAllText(recordsPath));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Skipped session record snapshot for " +
                        $"{profileFolder}: {ex.Message}");
                    continue;
                }
                if (recordFile?.records == null) continue;

                string profileName = Path.GetFileName(profileFolder)
                    .Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                recordFile.records.Sort((a, b) =>
                    StringComparer.Ordinal.Compare(a.guid, b.guid));
                foreach (SessionRecordEntry record in recordFile.records)
                {
                    if (record == null || string.IsNullOrEmpty(record.guid))
                        continue;
                    output.Append(profileName).Append('\t')
                        .Append(record.guid).Append('\t')
                        .Append(record.fingerprint ?? "").Append('\t')
                        .Append(record.score).Append('\t')
                        .Append(record.maxCombo).Append('\t')
                        .Append((int)record.medal).AppendLine();
                }
            }
        }

        string newContent = output.ToString();
        if (!File.Exists(outputPath) ||
            File.ReadAllText(outputPath) != newContent)
        {
            File.WriteAllText(outputPath, newContent, new UTF8Encoding(false));
        }
        Debug.Log($"Prepared T2 session record snapshot at {outputPath}.");
    }

    private static void CopyDefaultBundleToSelectedTechnikaTheme()
    {
        string defaultBundle = Path.Combine(
            Paths.kAssetBundleFolder, Paths.kDefaultBundleName);
        if (!File.Exists(defaultBundle))
        {
            return;
        }

        string projectTheme = Path.Combine(
            Paths.kThemeFolderName, "Technika 2" + Paths.kThemeExtension);

        // Keep the standalone test build synchronized with every successful
        // Build now, alongside the project's normal Themes copy.
        string projectRoot = Directory.GetParent(
            Application.dataPath).FullName;
        string testBuildThemes = Path.GetFullPath(Path.Combine(
            projectRoot, "..", "..", "..", "_TestBuild3", "Themes"));
        Directory.CreateDirectory(testBuildThemes);
        string testBuildTheme = Path.Combine(
            testBuildThemes, "Technika 2" + Paths.kThemeExtension);

        if (File.Exists(projectTheme))
        {
            File.Copy(defaultBundle, projectTheme, true);
            File.SetLastWriteTimeUtc(projectTheme,
                File.GetLastWriteTimeUtc(defaultBundle));
            Debug.Log($"Copied rebuilt default bundle to {projectTheme}. " +
                $"Timestamp={File.GetLastWriteTime(projectTheme):yyyy-MM-dd HH:mm:ss}");
        }

        File.Copy(defaultBundle, testBuildTheme, true);
        File.SetLastWriteTimeUtc(testBuildTheme,
            File.GetLastWriteTimeUtc(defaultBundle));
        Debug.Log($"Copied rebuilt default bundle to {testBuildTheme}. " +
            $"Timestamp={File.GetLastWriteTime(testBuildTheme):yyyy-MM-dd HH:mm:ss}");
    }
}
