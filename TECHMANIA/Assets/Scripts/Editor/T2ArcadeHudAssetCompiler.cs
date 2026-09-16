using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class T2ArcadeHudAssetCompiler
{
    private const string SourceRoot =
        @"C:\Users\Jen\Downloads\Technika Projects\t2\resource\MainGame\panel";

    public static void Run()
    {
        foreach (string mode in new[] { "pop", "star" })
            CompileMode(mode);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string mode in new[] { "pop", "star" })
            ConfigureGeneratedAssets(mode);

        AssetDatabase.SaveAssets();
        Debug.Log("[T2 Arcade HUD Compiler] Generated authentic synchronized " +
            "rainbow and rear-light frames for Pop and Star Mixing.");
    }

    private static void CompileMode(string mode)
    {
        string sourceFolder = Path.Combine(SourceRoot,
            mode + "_ingame_info");
        string outputFolder = Path.Combine(Application.dataPath, "UI",
            "Sprites", "Main Game", mode, "Arcade HUD");
        Directory.CreateDirectory(outputFolder);

        Texture2D barAtlas = LoadPng(Path.Combine(sourceFolder, "bar1.png"));
        try
        {
            RequireSize(barAtlas, 512, 256, mode + " bar1.png");
            WriteCrop(barAtlas, outputFolder, "bar_base.png", 0, 210, 434,
                20, false);
            for (int frame = 0; frame < 10; frame++)
                WriteCrop(barAtlas, outputFolder,
                    $"bar_anim_{frame:00}.png", 0, frame * 20, 434, 20,
                    false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(barAtlas);
        }

        Texture2D lightAtlas = LoadPng(Path.Combine(sourceFolder,
            "back_light.png"));
        try
        {
            RequireSize(lightAtlas, 1140, 94, mode + " back_light.png");
            for (int frame = 0; frame < 10; frame++)
                WriteCrop(lightAtlas, outputFolder,
                    $"back_light_{frame:00}.png", frame * 114, 0, 114, 94,
                    false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightAtlas);
        }
    }

    private static Texture2D LoadPng(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Missing authentic T2 HUD atlas.",
                path);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path), false))
            throw new InvalidOperationException("Could not decode " + path);
        return texture;
    }

    private static void RequireSize(Texture2D texture, int width, int height,
        string name)
    {
        if (texture.width != width || texture.height != height)
            throw new InvalidOperationException($"{name} is {texture.width}x" +
                $"{texture.height}; expected {width}x{height}.");
    }

    private static void WriteCrop(Texture2D atlas, string outputFolder,
        string fileName, int x, int yFromTop, int width, int height,
        bool flipVertically)
    {
        Color32[] pixels = atlas.GetPixels32();
        var output = new Color32[width * height];
        for (int outputY = 0; outputY < height; outputY++)
        {
            int sourceYFromTop = flipVertically
                ? yFromTop + outputY
                : yFromTop + height - 1 - outputY;
            int sourceY = atlas.height - 1 - sourceYFromTop;
            Array.Copy(pixels, sourceY * atlas.width + x, output,
                outputY * width, width);
        }

        var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels32(output);
        cropped.Apply(false, false);
        File.WriteAllBytes(Path.Combine(outputFolder, fileName),
            cropped.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(cropped);
    }

    private static void ConfigureGeneratedAssets(string mode)
    {
        string folder = $"Assets/UI/Sprites/Main Game/{mode}/Arcade HUD";
        foreach (string assetPath in AssetDatabase.FindAssets("t:Texture2D",
                     new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(assetPath);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
