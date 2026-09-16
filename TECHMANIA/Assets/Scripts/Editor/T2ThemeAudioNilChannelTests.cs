using System;
using System.IO;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

public static class T2ThemeAudioNilChannelTests
{
    [MenuItem("TECHMANIA/Tests/T2 Theme Audio Nil Channel")]
    public static void Run()
    {
        var script = new Script(CoreModules.Preset_SoftSandbox);
        script.DoString(@"
tm = {
    audio = {
        PlayMusic = function(sound, startTime) return nil end
    },
    options = {}
}
themeOptions = {
    GetBoolWithDefault = function(key, fallback) return fallback end,
    SetBool = function(key, value) end
}
");

        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "UI", "Scripts", "Theme Audio.txt"));
        script.DoString(source, null, "Theme Audio.txt");

        DynValue result = script.DoString(@"
local channel = themeAudio.PlayMusic({ name = 'loaded-preview' }, 0)
return channel != nil, channel.loop == false,
    type(channel.Stop) == 'function',
    type(channel.SetLoopPoints) == 'function'
");

        Require(result.Tuple[0].Boolean && result.Tuple[1].Boolean &&
                result.Tuple[2].Boolean && result.Tuple[3].Boolean,
            "A failed FMOD music allocation did not return a safe silent channel.");
        Debug.Log("[T2 Audio Test] Nil FMOD music channels fall back safely.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
