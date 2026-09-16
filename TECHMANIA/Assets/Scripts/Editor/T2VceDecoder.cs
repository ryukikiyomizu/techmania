using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Decodes DJMAX Technika 2 .vce (VCMF) files and dumps per-layer geometry.
//
// Port of files/vce_parse.py, which was itself derived from the open-source
// VCEConverter (structss.h / main.cpp, GPL-3.0). Lives in an Editor folder so
// it never ships in the theme AssetBundle.
//
// This exists because the geometry work needs a way to read the authored
// layer rects, and the normal route (python in a shell) is unavailable.
public static class T2VceDecoder
{
    // 256-byte repeating XOR table used to mask most .vce/.vci payloads.
    private static readonly byte[] kMask = new byte[]
    {
        247,77,219,74,220,102,240,83,197,127,233,28,138,48,166,5,147,41,191,46,
        184,2,148,55,161,27,141,0,150,44,186,25,143,53,163,50,164,30,136,43,189,
        7,145,100,242,72,222,125,235,81,199,86,192,122,236,79,217,99,245,200,94,
        228,114,209,71,253,107,250,108,214,64,227,117,207,89,172,58,128,22,181,
        35,153,15,158,8,178,36,135,17,171,61,144,6,188,42,137,31,165,51,162,52,
        142,24,187,45,151,1,244,98,216,78,237,123,193,87,198,80,234,124,223,73,
        243,101,88,206,116,226,65,215,109,251,106,252,70,208,115,229,95,201,60,
        170,16,134,37,179,9,159,14,152,34,180,23,129,59,173,32,182,12,154,57,
        175,21,131,18,132,62,168,11,157,39,177,68,210,104,254,93,203,113,231,
        118,224,90,204,111,249,67,213,232,126,196,82,241,103,221,75,218,76,246,
        96,195,85,239,121,140,26,160,54,149,3,185,47,190,40,146,4,167,49,139,29,
        176,38,156,10,169,63,133,19,130,20,174,56,155,13,183,33,212,66,248,110,
        205,91,225,119,230,112,202,92,255,105,211,69,120,238,84,194,97
    };

    // magic[4] version[4] doctype[4] fps[4] maxKey[4] layerNum[4] reserved[12]
    private const int kHeaderSize = 36;
    private const int kTexNameLen = 0x70;
    private const int kTexCoordLen = 0x1c;
    private const int kTexSize = kTexNameLen + kTexCoordLen;
    private const int kAniKeySize = 124;     // see field map in ReadKey

    // The five screens behind the reported scaling regressions.
    private static readonly string[] kTargets = new string[]
    {
        "SongSelect/star/musicslot.vce",
        "SongSelect/pop/musicslot.vce",
        "SongSelect/star/musicinfo_s.vce",
        "SongSelect/star/popup_info.vce",
        "ModeSelect/Count_Star.vce",
        "TimeLimit (Menu Countdown)/Count_Star.vce",
        "TimeLimit (Menu Countdown)/Count_Star2.vce",
        "results/Total_Star/Total_Result_Star.vce",
        "results/Total_Pop/Total_Result_Star.vce",
    };

    // A "crop the oversized bronze trophy to its opaque bounds" helper used to
    // live here. Cropping is the wrong repair: the card and popup quads are
    // fixed and scale-to-fit, so the transparent margin IS the badge's size,
    // and an ink-tight canvas draws bigger than its siblings, not the same.
    // Select Song trophies must be the arcade's 80x80 Record canvas verbatim;
    // T2ReportedRegressionsVerification enforces that.

    // Reports the native pixel size of the theme sprites that the VCE rects
    // are supposed to match, so authored-vs-shipped mismatches are visible.
    [MenuItem("Window/TECHMANIA Theme/Dump theme sprite sizes")]
    public static void DumpSpriteSizes()
    {
        string[] folders = new string[]
        {
            "Assets/UI/Sprites/Select Song",
            "Assets/UI/Sprites/Select Song/Trophies",
        };

        StringBuilder report = new StringBuilder();
        report.AppendLine("Theme sprite native sizes");
        report.AppendLine();

        foreach (string folder in folders)
        {
            report.AppendLine("--- " + folder);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets recurses; keep each texture under its own heading.
                if (Path.GetDirectoryName(assetPath).Replace('\\', '/') != folder) continue;
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null) continue;
                report.AppendLine(string.Format("    {0,-34} {1}x{2}",
                    Path.GetFileName(assetPath), tex.width, tex.height));
            }
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.GetFullPath(Path.Combine(
            projectRoot, "..", "..", "..", "files", "theme_sprite_sizes.txt"));
        File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
        Debug.Log("Theme sprite sizes written to " + outputPath);
    }

    [MenuItem("Window/TECHMANIA Theme/Decode T2 VCE geometry")]
    public static void DecodeTargets()
    {
        // projectRoot/../../.. == the "Technika Projects" workspace root,
        // matching the convention already used by BuildAssetBundleWindow.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string workspace = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", ".."));
        string resourceRoot = Path.Combine(workspace, "t2", "resource");
        string outputPath = Path.Combine(workspace, "files", "vce_geometry_report.txt");

        StringBuilder report = new StringBuilder();
        report.AppendLine("T2 VCE geometry dump");
        report.AppendLine("resourceRoot=" + resourceRoot);
        report.AppendLine();

        foreach (string relative in kTargets)
        {
            string path = Path.Combine(resourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            report.AppendLine("================================================================");
            report.AppendLine("FILE " + relative);
            report.AppendLine("================================================================");
            if (!File.Exists(path))
            {
                report.AppendLine("  MISSING: " + path);
                report.AppendLine();
                continue;
            }
            try
            {
                DumpFile(path, report);
            }
            catch (Exception ex)
            {
                report.AppendLine("  DECODE ERROR: " + ex.Message);
            }
            report.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
        Debug.Log("T2 VCE geometry written to " + outputPath);
    }

    private static void DumpFile(string path, StringBuilder report)
    {
        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length < kHeaderSize || raw[0] != 'V' || raw[1] != 'C' || raw[2] != 'M')
        {
            report.AppendLine("  not a VCM file");
            return;
        }

        // Bytes 0-7 (magic + version) are never masked; the mask index runs
        // continuously from the start of the file rather than resetting.
        bool masked = raw[13] != 0;
        byte[] data = (byte[])raw.Clone();
        if (masked)
        {
            for (int i = 8; i < data.Length; i++) data[i] ^= kMask[i % 256];
        }

        uint fps = BitConverter.ToUInt32(data, 12);
        uint maxKey = BitConverter.ToUInt32(data, 16);
        uint layerNum = BitConverter.ToUInt32(data, 20);
        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  masked={0} fps={1} maxKey={2} layers={3} duration={4:F2}s",
            masked, fps, maxKey, layerNum, fps > 0 ? maxKey / (float)fps : 0f));

        int off = kHeaderSize;
        for (int li = 0; li < layerNum; li++)
        {
            uint texCount = BitConverter.ToUInt32(data, off);
            off += 4;

            List<string> texInfo = new List<string>();
            for (int ti = 0; ti < texCount; ti++)
            {
                string name = ReadCString(data, off, kTexNameLen);
                short x1 = BitConverter.ToInt16(data, off + kTexNameLen + 16);
                short y1 = BitConverter.ToInt16(data, off + kTexNameLen + 18);
                short x2 = BitConverter.ToInt16(data, off + kTexNameLen + 20);
                short y2 = BitConverter.ToInt16(data, off + kTexNameLen + 22);
                texInfo.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} crop=({1},{2})-({3},{4}) {5}x{6}",
                    name, x1, y1, x2, y2, x2 - x1, y2 - y1));
                off += kTexSize;
            }

            uint keyCount = BitConverter.ToUInt32(data, off);
            off += 4;
            int keyBlockStart = off;
            off += (int)keyCount * kAniKeySize;

            report.AppendLine(string.Format("  --- layer {0}: {1} texture(s), {2} key(s)", li, texCount, keyCount));
            foreach (string t in texInfo) report.AppendLine("        tex " + t);
            if (keyCount == 0) continue;

            DumpKey(data, keyBlockStart, "first", report);
            if (keyCount > 1)
            {
                DumpKey(data, keyBlockStart + ((int)keyCount - 1) * kAniKeySize, "last ", report);
            }
        }

        report.AppendLine(string.Format("  consumed {0} of {1} bytes{2}",
            off, data.Length, off == data.Length ? "" : "  [!] size mismatch - treat with suspicion"));
    }

    // AniKey field map (all little-endian):
    //   0x00 u32 frameTime      0x04 u32 frameType
    //   0x08 f32[2] pos         0x10 f32[2] uv     0x18 f32[2] uvSize
    //   0x20 f32[2] uv2         0x28 f32[2] uv2Size
    //   0x30 f32[8] quadPoints  0x50 f32 texId     0x54 u32 aniType
    //   0x58 f32 aniDelta       0x5C f32 rotation  0x60 f32[4] color
    //   0x70 u32[2] blend       0x78 u32 multiTexMode
    private static void DumpKey(byte[] data, int off, string label, StringBuilder report)
    {
        uint frameTime = BitConverter.ToUInt32(data, off);
        float px = BitConverter.ToSingle(data, off + 0x08);
        float py = BitConverter.ToSingle(data, off + 0x0C);

        // quadPoints is stored as four X components followed by four Y
        // components (x0..x3, y0..y3), NOT as interleaved (x,y) pairs.
        // Verified against ModeSelect/Count_Star.vce, where the resulting
        // 68x87 quad matches that layer's 68x87 texture crop exactly.
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        StringBuilder quad = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            float qx = BitConverter.ToSingle(data, off + 0x30 + i * 4);
            float qy = BitConverter.ToSingle(data, off + 0x30 + 16 + i * 4);
            minX = Mathf.Min(minX, qx); maxX = Mathf.Max(maxX, qx);
            minY = Mathf.Min(minY, qy); maxY = Mathf.Max(maxY, qy);
            quad.AppendFormat(CultureInfo.InvariantCulture, "({0:F1},{1:F1})", qx, qy);
        }

        float a = BitConverter.ToSingle(data, off + 0x60 + 12);
        float rot = BitConverter.ToSingle(data, off + 0x5C);

        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "        {0} f={1,-4} pos=({2:F1},{3:F1}) quad={4} bbox={5:F1}x{6:F1} at ({7:F1},{8:F1}) rot={9:F2} alpha={10:F3}",
            label, frameTime, px, py, quad, maxX - minX, maxY - minY, minX, minY, rot, a));
    }

    private static string ReadCString(byte[] data, int off, int maxLen)
    {
        int len = 0;
        while (len < maxLen && data[off + len] != 0) len++;
        return Encoding.GetEncoding("iso-8859-1").GetString(data, off, len);
    }
}
