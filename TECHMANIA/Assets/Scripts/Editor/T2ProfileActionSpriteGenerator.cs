using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class T2ProfileActionSpriteGenerator
{
    private const string SourcePath =
        "Assets/UI/Sprites/Login/T2/InputCard/join_now_up.png";
    private const string OutputDirectory =
        "Assets/UI/Sprites/Login/T2/ProfileActions";

    private readonly struct SpriteSpec
    {
        public SpriteSpec(string fileName, int width, int height)
        {
            FileName = fileName;
            Width = width;
            Height = height;
        }

        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
    }

    [MenuItem("TECHMANIA/Theme/Generate T2 Profile Action Sprites")]
    public static void Run()
    {
        string sourceAbsolutePath = ToAbsolutePath(SourcePath);
        Require(File.Exists(sourceAbsolutePath),
            $"Authentic T2 action source is missing: {SourcePath}");

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Require(source.LoadImage(File.ReadAllBytes(sourceAbsolutePath), false),
            $"Could not decode authentic T2 action source: {SourcePath}");

        Directory.CreateDirectory(ToAbsolutePath(OutputDirectory));
        SpriteSpec[] specs =
        {
            new SpriteSpec("profile_action_icon_132x42.png", 132, 42),
            new SpriteSpec("profile_action_layer_160x40.png", 160, 40),
            new SpriteSpec("profile_action_crew_layer_173x40.png", 173, 40),
            new SpriteSpec("profile_action_catalog_484x43.png", 484, 43),
            new SpriteSpec("profile_action_save_250x45.png", 250, 45)
        };

        foreach (SpriteSpec spec in specs)
        {
            Texture2D generated = ExtendMiddleWithoutWarping(
                source, spec.Width, spec.Height);
            string assetPath = $"{OutputDirectory}/{spec.FileName}";
            File.WriteAllBytes(ToAbsolutePath(assetPath), generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);
        }

        UnityEngine.Object.DestroyImmediate(source);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (SpriteSpec spec in specs)
            ConfigureImporter($"{OutputDirectory}/{spec.FileName}");

        AssetDatabase.ImportAsset("Assets/UI/MainStyle.uss",
            ImportAssetOptions.ForceUpdate |
            ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        Debug.Log(
            "[T2 Profile Actions] Generated five exact-size button backgrounds " +
            "from the authentic blank T2 art without stretching the endcaps.");
    }

    private static Texture2D ExtendMiddleWithoutWarping(Texture2D source,
        int targetWidth, int targetHeight)
    {
        int proportionalWidth = Mathf.Max(1,
            Mathf.RoundToInt(source.width * (targetHeight / (float)source.height)));
        Color32[] proportional = ResizeBilinear(
            source.GetPixels32(), source.width, source.height,
            proportionalWidth, targetHeight);
        Color32[] output = new Color32[targetWidth * targetHeight];

        int sourceMiddle = proportionalWidth / 2;
        int targetMiddle = targetWidth / 2;
        int insertedWidth = targetWidth - proportionalWidth;

        for (int y = 0; y < targetHeight; y++)
        {
            int sourceRow = y * proportionalWidth;
            int outputRow = y * targetWidth;
            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX;
                if (insertedWidth >= 0)
                {
                    if (x < sourceMiddle)
                        sourceX = x;
                    else if (x < sourceMiddle + insertedWidth)
                        sourceX = sourceMiddle;
                    else
                        sourceX = x - insertedWidth;
                }
                else
                {
                    int removedWidth = -insertedWidth;
                    sourceX = x < targetMiddle ? x : x + removedWidth;
                }

                sourceX = Mathf.Clamp(sourceX, 0, proportionalWidth - 1);
                output[outputRow + x] = proportional[sourceRow + sourceX];
            }
        }

        Texture2D result = new Texture2D(
            targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(output);
        result.Apply(false, false);
        return result;
    }

    private static Color32[] ResizeBilinear(Color32[] source, int sourceWidth,
        int sourceHeight, int targetWidth, int targetHeight)
    {
        Color32[] output = new Color32[targetWidth * targetHeight];
        for (int y = 0; y < targetHeight; y++)
        {
            float sourceY = ((y + 0.5f) * sourceHeight / targetHeight) - 0.5f;
            int y0 = Mathf.Clamp(Mathf.FloorToInt(sourceY), 0, sourceHeight - 1);
            int y1 = Mathf.Min(y0 + 1, sourceHeight - 1);
            float yLerp = Mathf.Clamp01(sourceY - y0);
            for (int x = 0; x < targetWidth; x++)
            {
                float sourceX = ((x + 0.5f) * sourceWidth / targetWidth) - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, sourceWidth - 1);
                int x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                float xLerp = Mathf.Clamp01(sourceX - x0);

                Color top = Color.Lerp(source[y0 * sourceWidth + x0],
                    source[y0 * sourceWidth + x1], xLerp);
                Color bottom = Color.Lerp(source[y1 * sourceWidth + x0],
                    source[y1 * sourceWidth + x1], xLerp);
                output[y * targetWidth + x] = Color.Lerp(top, bottom, yLerp);
            }
        }
        return output;
    }

    private static void ConfigureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath)
            as TextureImporter;
        Require(importer != null, $"Could not import generated sprite: {assetPath}");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
