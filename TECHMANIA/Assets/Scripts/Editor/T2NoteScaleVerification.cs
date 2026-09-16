using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class T2NoteScaleVerification
{
    [MenuItem("TECHMANIA/Tests/Verify T2 Note Scale")]
    public static void Run()
    {
        MethodInfo method = typeof(GameLayout).GetMethod(
            "GetNoteScaleCompensation",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(int) },
            null);
        if (method == null)
            throw new Exception("GameLayout has no lane-aware note scale compensation.");

        Func<int, float> scale = playableLanes =>
            (float)method.Invoke(null, new object[] { playableLanes });

        RequireClose(scale(3), 1f,
            "Star Mixing must retain its current note scale.");
        RequireClose(scale(4), 1.15f,
            "Pop Mixing must use the T3 source note scale.");
        RequireClose(scale(2), 1f,
            "Non-Pop lane counts must retain the authored note scale.");

        Debug.Log("[T2 Test] Pop uses T3 note scale while Star is unchanged.");
    }

    private static void RequireClose(float actual, float expected,
        string message)
    {
        if (Mathf.Abs(actual - expected) > 0.0001f)
            throw new Exception($"{message} Expected {expected}, got {actual}.");
    }
}
