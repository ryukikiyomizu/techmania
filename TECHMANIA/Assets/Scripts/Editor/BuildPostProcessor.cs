using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

public class BuildPostProcessor
{
    private const long MaxBareboneThemeBytes = 90L * 1024L * 1024L;

    [PostProcessBuild]
    private static void ChangeXcodePlist(
        BuildTarget buildTarget, string pathToBuiltProject)
    {
#if UNITY_IOS
        if (buildTarget == BuildTarget.iOS)
        {
            string plistPath = pathToBuiltProject + "/Info.plist";
            UnityEditor.iOS.Xcode.PlistDocument plist =
                new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            UnityEditor.iOS.Xcode.PlistElementDict rootDict = 
                plist.root;
            rootDict.SetBoolean("UIFileSharingEnabled", true);
            rootDict.SetBoolean("UISupportsDocumentBrowser", true);
            plist.WriteToFile(plistPath);
        }
#endif
    }

    [PostProcessBuild]
    private static void CopyDefaultAssetBundle(
        BuildTarget buildTarget, string pathToBuiltProject)
    {
        string projectRoot = Directory.GetParent(
            Application.dataPath).FullName;
        string bareboneDefaultTheme = Path.Combine(
            projectRoot, "BuildResources", "Default.tmtheme");
        if (!File.Exists(bareboneDefaultTheme))
        {
            throw new FileNotFoundException(
                "The official barebone Default.tmtheme build resource is missing.",
                bareboneDefaultTheme);
        }

        long bareboneBytes = new FileInfo(bareboneDefaultTheme).Length;
        if (bareboneBytes >= MaxBareboneThemeBytes)
        {
            throw new InvalidDataException(
                $"Barebone Default.tmtheme is {bareboneBytes / 1024d / 1024d:F2} MiB; " +
                "it must remain below 90 MiB.");
        }

        string themeFolder = Path.Combine(
            Path.GetDirectoryName(pathToBuiltProject),
            Paths.kThemeFolderName);
        Directory.CreateDirectory(themeFolder);
        File.Copy(
            bareboneDefaultTheme,
            Path.Combine(
                themeFolder,
                Options.kDefaultTheme + Paths.kThemeExtension),
            overwrite: true
        );
        Debug.Log($"Copied barebone Default.tmtheme ({bareboneBytes} bytes) " +
            $"to {themeFolder}.");
    }

    [PostProcessBuild]
    private static void CopyT3AssetBundle(
        BuildTarget buildTarget, string pathToBuiltProject)
    {
        string t3BundlePath = Path.Combine(
            Paths.kAssetBundleFolder, "t3");
        if (!File.Exists(t3BundlePath)) return; // not built yet — skip silently

        string themeFolder = Path.Combine(
            Path.GetDirectoryName(pathToBuiltProject),
            Paths.kThemeFolderName);
        Directory.CreateDirectory(themeFolder);
        File.Copy(
            t3BundlePath,
            Path.Combine(themeFolder, "T3 HD" + Paths.kThemeExtension),
            overwrite: true
        );
    }

    [PostProcessBuild]
    private static void WriteVersionToFile(
        BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.StandaloneWindows ||
            buildTarget == BuildTarget.StandaloneWindows64)
        {
            File.WriteAllText(
                Path.Combine(
                    Path.GetDirectoryName(pathToBuiltProject), 
                    "version"),
                Application.version);
        }
    }
}
