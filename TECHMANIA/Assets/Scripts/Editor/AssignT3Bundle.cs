using UnityEditor;
using UnityEngine;

public static class AssignT3Bundle
{
    [MenuItem("Window/TECHMANIA Theme/Assign T3 bundle name")]
    public static void Assign()
    {
        // Walk every asset inside Assets/T3 and assign it to the "t3" bundle.
        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets/T3" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null) continue;
            if (importer.assetBundleName == "t3") continue; // already set

            importer.SetAssetBundleNameAndVariant("t3", "");
            count++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AssignT3Bundle] Set assetBundleName=t3 on {count} assets.");
    }
}
