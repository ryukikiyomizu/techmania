using System;
using System.IO;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

public static class T2ThemeOptionSnapshotTests
{
    [MenuItem("TECHMANIA/Tests/T2 Active Theme Option Snapshot")]
    public static void Run()
    {
        var script = new Script(CoreModules.Preset_SoftSandbox);
        script.DoString(@"
function MakeDict(values)
    values.ContainsKey = function(key) return values[key] != nil end
    return values
end

activeDict = MakeDict({ NoCard = 'False', DjName = 'ALPHA' })
legacyDict = MakeDict({})
tm = { options = {
    GetThemeOptions = function(name)
        if name == 'TECHNIKA 2' then return activeDict end
        return legacyDict
    end
} }
net = { int = { TryParse = function(value) return false, 0 end } }
");

        string source = File.ReadAllText(Path.Combine(Application.dataPath,
            "UI", "Scripts", "Theme Options.txt"));
        script.DoString(source, null, "Theme Options.txt");

        DynValue result = script.DoString(@"
local snapshot = themeOptions.CaptureSnapshot(
    {'NoCard', 'DjName', 'DjMessage'},
    function(key)
        if key == 'NoCard' then return 'True' end
        return ''
    end)

activeDict = MakeDict({ NoCard = 'True', DjName = 'BETA', DjMessage = 'changed' })
themeOptions.RestoreSnapshot(snapshot)

return snapshot.NoCard, snapshot.DjName, snapshot.DjMessage,
    activeDict.NoCard, activeDict.DjName, activeDict.DjMessage
");

        Require(result.Tuple[0].String == "False", "NoCard was not captured.");
        Require(result.Tuple[1].String == "ALPHA", "DJ name was not captured.");
        Require(result.Tuple[2].String == "", "Missing option did not use its default.");
        Require(result.Tuple[3].String == "False" &&
                result.Tuple[4].String == "ALPHA" &&
                result.Tuple[5].String == "",
            "Snapshot restore did not target the currently active profile dictionary.");

        Debug.Log("[T2 Profile Test] Theme option snapshots follow the active profile dictionary.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
