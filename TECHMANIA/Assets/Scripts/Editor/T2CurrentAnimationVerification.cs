using System;
using UnityEditor;
using UnityEngine;

public static class T2CurrentAnimationVerification
{
    public static void Run()
    {
        try
        {
            T2AnimationPlanTests.Run();
            T2GameplayVisualRegressionVerification.Run();
            T2AttractPolishVerification.Run();
            T2TrackSortingContract.Run();
            T2ResultSkipFinalizationTests.Run();
            T2AllClearArcadeVerification.Run();
            string[] waveFailures = T2TouchWaveInputTests.Evaluate();
            if (waveFailures.Length > 0)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, waveFailures));
            }

            Debug.Log("[T2 Test] Current animation verification passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
