using UnityEditor;
using UnityEngine;

public static class T2ProfileCardAtelierBuild
{
    public static void VerifyAndBuildTheme()
    {
        T2ProfileArcadeHybridVerification.Run();
        T2AuthenticCosmeticCatalogVerification.Run();
        BuildAssetBundleWindow.BuildForDefaultPlatform();
        Debug.Log("[T2 Profile Atelier] Verification and theme build completed.");
    }
}
