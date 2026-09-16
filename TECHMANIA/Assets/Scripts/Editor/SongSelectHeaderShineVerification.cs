using System;
using System.IO;
using UnityEngine;

// musicselect_userinfo.vce is byte-identical in resource/SongSelect/star and
// /pop: fps 60, maxkey 100, and L5 holds light_bg.jpg additive over frames
// 20-70 while UV X scrolls 0.7 -> -0.5 and alpha falls 255 -> 150. It is
// sandwiched between the logo plate (L1, head.png (256,112,512,224) at (20,0))
// and the long bar (L6, head.png (0,0,1024,112) at (0,0)), so both arcade modes
// draw the header as two quads with the shine clipped in between.
public static class SongSelectHeaderShineVerification
{
    public static void RunChecks()
    {
        string ui = Path.Combine(Application.dataPath, "UI");
        string mainTree = File.ReadAllText(Path.Combine(ui, "MainTree.uxml"));
        string lua = File.ReadAllText(Path.Combine(ui, "Scripts",
            "Select Song Screen.txt")).Replace("\r\n", "\n");

        int back = mainTree.IndexOf("name=\"mode-header-back\"", StringComparison.Ordinal);
        int shine = mainTree.IndexOf("name=\"mode-header-shine-clip\"", StringComparison.Ordinal);
        int front = mainTree.IndexOf("name=\"mode-header-front\"", StringComparison.Ordinal);
        Require(back >= 0 && shine > back && front > shine,
            "Song Select header layers are not ordered back, shine, front.");
        Require(mainTree.Contains(
            "name=\"mode-header-shine-clip\" picking-mode=\"Ignore\" style=\"position: absolute; left: 20px; top: 0; width: 256px; height: 112px; overflow: hidden;\""),
            "The shine clip does not match the VCE 256x112 logo canvas.");
        Require(mainTree.Contains(
            "name=\"mode-header-shine\" picking-mode=\"Ignore\" style=\"position: absolute; left: -130px; top: -11px; width: 512px; height: 128px;"),
            "The shine texture is not fixed at the VCE quad (-110,-11) 512x128.");

        // Both arcade modes get the sandwich; pop's pair also carries the rest
        // of that screen's chrome, star's is only the 112px header strip.
        Require(lua.Contains("arcadeHeaderHeight = { pop = 768, star = 112 }"),
            "The arcade header sandwich is not declared for both pop and star.");
        Require(lua.Contains("headerBack.style.height = StyleLength(sandwichHeight)") &&
            lua.Contains("headerFront.style.height = StyleLength(sandwichHeight)") &&
            lua.Contains("modePath .. \"header_back.png\"") &&
            lua.Contains("modePath .. \"header_front.png\""),
            "Song Select does not bind the per-mode header layers.");
        Require(lua.Contains(
            "local sandwichHeight = selectSongScreen.arcadeHeaderHeight[") &&
            lua.Contains("if (sandwichHeight != nil) then"),
            "The header sandwich is still limited to one hardcoded mode.");

        string shineFunction = Block(lua, "StartHeaderShine = function()",
            "Show = function(onReady)");
        Require(!shineFunction.Contains("selectModeScreen.selectedMode ~= \"pop\"") &&
            shineFunction.Contains(
                "if (selectSongScreen.arcadeHeaderHeight[selectModeScreen.selectedMode] == nil) then"),
            "The header shine is not driven by the arcade sandwich table.");
        Require(!lua.Contains("shineTimeScale"),
            "The header shine still rescales the arcade 100-frame loop.");
        Require(shineFunction.Contains("selectModeScreen.selectedMode ..") &&
            shineFunction.Contains("\"/light_bg.jpg\""),
            "The shine does not load the per-mode light_bg.jpg.");

        // 100 frames at fps 60. The streak centre is 152 + translate, and the
        // arcade puts it at -84 on frame 30 and 347 on frame 65.
        Require(shineFunction.Contains("{ time = 0, values = {-236, 0} }") &&
            shineFunction.Contains("{ time = 30 / 60, values = {-236, 0} }") &&
            shineFunction.Contains("{ time = 65 / 60, values = {195, 0} }") &&
            shineFunction.Contains("{ time = 100 / 60, values = {195, 0} }"),
            "The UV sweep is not the measured arcade trajectory at arcade speed.");
        Require(shineFunction.Contains("{ time = 28 / 60, values = {0} }") &&
            shineFunction.Contains("{ time = 30 / 60, values = {234 / 255} }") &&
            shineFunction.Contains("{ time = 65 / 60, values = {160.5 / 255} }") &&
            shineFunction.Contains("{ time = 67 / 60, values = {0} }") &&
            shineFunction.Contains("{ time = 100 / 60, values = {0} }"),
            "The shine opacity does not follow the arcade 255 -> 150 alpha ramp.");
        Require(lua.Contains(
            "Hide = function()\n        selectSongScreen.screen.display = false") &&
            lua.Contains("selectSongScreen.StopHeaderShine()"),
            "Song Select does not stop the shine during Hide.");

        VerifySize("pop", 768);
        VerifySize("star", 112);
        VerifyStarSplitRebuildsShell();
        Debug.Log("SongSelectHeaderShineVerification checks passed.");
    }

    private static string SpritePath(string mode, string name)
    {
        return Path.Combine(Application.dataPath, "UI", "Sprites", "Select Song",
            mode, name);
    }

    private static void VerifySize(string mode, int height)
    {
        foreach (string name in new[] { "header_back.png", "header_front.png" })
        {
            Texture2D texture = Load(SpritePath(mode, name));
            Require(texture.width == 1280 && texture.height == height,
                $"{mode}/{name} is {texture.width}x{texture.height}, not 1280x{height}.");
        }
        Require(File.Exists(SpritePath(mode, "light_bg.jpg")),
            $"Missing {mode}/light_bg.jpg for the header shine.");
    }

    // The split may only open the L5 slot: compositing star's front over its
    // back has to reproduce the flattened userinfo_shell.png it replaced.
    private static void VerifyStarSplitRebuildsShell()
    {
        Color32[] back = Load(SpritePath("star", "header_back.png")).GetPixels32();
        Color32[] front = Load(SpritePath("star", "header_front.png")).GetPixels32();
        Color32[] shell = Load(SpritePath("star", "userinfo_shell.png")).GetPixels32();
        Require(back.Length == shell.Length && front.Length == shell.Length,
            "The star header layers and userinfo_shell are not the same size.");

        int mismatches = 0;
        for (int i = 0; i < shell.Length; i++)
        {
            if (shell[i].a == 0) continue;
            float fa = front[i].a / 255f;
            float ba = back[i].a / 255f;
            float outAlpha = fa + ba * (1f - fa);
            if (outAlpha <= 0f || Math.Abs(outAlpha * 255f - shell[i].a) > 1.5f ||
                !Matches(front[i].r, back[i].r, fa, ba, outAlpha, shell[i].r) ||
                !Matches(front[i].g, back[i].g, fa, ba, outAlpha, shell[i].g) ||
                !Matches(front[i].b, back[i].b, fa, ba, outAlpha, shell[i].b))
            {
                mismatches++;
            }
        }
        Require(mismatches == 0,
            $"The star header split changes {mismatches} visible pixels of userinfo_shell.");
    }

    private static bool Matches(byte f, byte b, float fa, float ba, float outAlpha,
        byte expected)
    {
        float value = (f * fa + b * ba * (1f - fa)) / outAlpha;
        return Math.Abs(value - expected) <= 1.5f;
    }

    private static Texture2D Load(string path)
    {
        Require(File.Exists(path), $"Missing header asset {path}.");
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Require(texture.LoadImage(File.ReadAllBytes(path)),
            $"Could not decode header asset {path}.");
        return texture;
    }

    private static string Block(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
