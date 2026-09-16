using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class T2ThemePackageVerification
{
    private const long MaxThemeBytes = 90L * 1024L * 1024L;

    public static void Run()
    {
        var failures = Evaluate();
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
                Debug.LogError("[T2 Theme Package Test] " + failure);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[T2 Theme Package Test] Compact bundle and left/right tab contracts passed.");
        EditorApplication.Exit(0);
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string bundlePath = Path.Combine(Application.dataPath, "AssetBundles", "default");
        string barebonePath = Path.Combine(
            projectRoot, "BuildResources", "Default.tmtheme");
        string themesFolder = Path.GetFullPath(Path.Combine(
            projectRoot, "..", "..", "..", "_TestBuild3", "Themes"));

        Check(File.Exists(bundlePath), "Missing customized Technika 2 source bundle.", failures);
        CheckCompactFile(barebonePath, "barebone Default build resource", failures);
        CheckCompactFile(Path.Combine(themesFolder, "Default.tmtheme"),
            "deployed Default.tmtheme", failures);
        Check(File.Exists(Path.Combine(themesFolder, "Technika 2.tmtheme")),
            "Missing deployed Technika 2.tmtheme.", failures);

        if (!File.Exists(bundlePath))
            return failures;

        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            failures.Add("The source default bundle cannot be loaded by Unity.");
            return failures;
        }

        try
        {
            string[] names = bundle.GetAllAssetNames();
            foreach (string layer in new[]
            {
                "plate", "pressbase", "pressflash", "base", "glow"
            })
            {
                string left = FindAsset(names, "tab_l_" + layer + ".png");
                string right = FindAsset(names, "tab_r_" + layer + ".png");
                if (left == null || right == null)
                {
                    failures.Add($"Bundle omitted the distinct tab_l_{layer}/tab_r_{layer} pair.");
                    continue;
                }

                Texture2D leftTexture = bundle.LoadAsset<Texture2D>(left);
                Texture2D rightTexture = bundle.LoadAsset<Texture2D>(right);
                Check(leftTexture != null && rightTexture != null,
                    $"Unity could not load the tab_l_{layer}/tab_r_{layer} texture pair.",
                    failures);
                Check(!string.Equals(left, right, StringComparison.OrdinalIgnoreCase) &&
                      !ReferenceEquals(leftTexture, rightTexture),
                    $"Right-side tab layer {layer} aliases the left-side asset.", failures);
            }
        }
        finally
        {
            bundle.Unload(true);
        }

        if (File.Exists(bundlePath))
        {
            string technikaHash = Sha256(bundlePath);
            string deployedTechnika = Path.Combine(themesFolder, "Technika 2.tmtheme");
            if (File.Exists(deployedTechnika))
                Check(Sha256(deployedTechnika) == technikaHash,
                    "Technika 2.tmtheme is stale or differs from its customized source bundle.",
                    failures);
        }

        string deployedDefault = Path.Combine(themesFolder, "Default.tmtheme");
        if (File.Exists(barebonePath) && File.Exists(deployedDefault))
        {
            Check(Sha256(deployedDefault) == Sha256(barebonePath),
                "Default.tmtheme differs from the dedicated barebone build resource.",
                failures);
            Check(!File.Exists(bundlePath) ||
                  Sha256(deployedDefault) != Sha256(bundlePath),
                "Default.tmtheme was overwritten by the customized Technika 2 bundle.",
                failures);
        }

        return failures;
    }

    private static string FindAsset(IEnumerable<string> names, string fileName)
    {
        return names.FirstOrDefault(name =>
            name.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static void CheckCompactFile(string path, string label,
        ICollection<string> failures)
    {
        if (!File.Exists(path))
        {
            failures.Add("Missing " + label + ": " + path);
            return;
        }

        long bytes = new FileInfo(path).Length;
        Check(bytes < MaxThemeBytes,
            $"{label} is {bytes / 1024d / 1024d:F2} MiB; it must be below 90 MiB.",
            failures);
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static void Check(bool condition, string failure,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
