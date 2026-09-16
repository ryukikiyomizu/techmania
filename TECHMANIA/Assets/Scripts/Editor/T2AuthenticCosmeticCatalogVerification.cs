using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class T2AuthenticCosmeticCatalogVerification
{
    private sealed class CatalogContract
    {
        public string listFile;
        public string imageFolder;
        public int expectedCount;

        public CatalogContract(string listFile, string imageFolder,
            int expectedCount)
        {
            this.listFile = listFile;
            this.imageFolder = imageFolder;
            this.expectedCount = expectedCount;
        }
    }

    [MenuItem("TECHMANIA/Tests/Verify T2 Authentic Cosmetic Catalog")]
    public static void Run()
    {
        const string root = "Assets/UI/Profile Catalog";
        CatalogContract[] contracts =
        {
            new CatalogContract("iconList.json", "dmt-icon", 138),
            new CatalogContract("titlePlateList.json", "dmt-title-plate", 206),
            new CatalogContract("titlePatternList.json", "dmt-title-pattern", 106),
            new CatalogContract("teamPlateList.json", "dmt-team-plate", 53),
            new CatalogContract("teamPatternList.json", "dmt-team-pattern", 167)
        };

        foreach (CatalogContract contract in contracts)
        {
            string listPath = $"{root}/config/dmt2/{contract.listFile}";
            TextAsset listAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                listPath);
            Require(listAsset != null, $"Missing authentic catalog {listPath}.");

            List<int> ids = ParseIds(listAsset.text);
            Require(ids.Count == contract.expectedCount,
                $"{contract.listFile} expected {contract.expectedCount} IDs, got {ids.Count}.");
            Require(new HashSet<int>(ids).Count == ids.Count,
                $"{contract.listFile} contains duplicate IDs.");

            foreach (int id in ids)
            {
                string texturePath =
                    $"{root}/images/{contract.imageFolder}/{id}.png";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    texturePath);
                Require(texture != null,
                    $"Catalog ID {id} has no authentic texture at {texturePath}.");
            }
        }

        VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/UI/MainTree.uxml");
        Require(tree != null, "MainTree.uxml could not be loaded.");
        TemplateContainer ui = tree.Instantiate();
        Require(ui.Q("cosmetic-catalog-overlay") != null,
            "Profile editor has no cosmetic catalog overlay.");
        Require(ui.Q("catalog-page-label") != null,
            "Cosmetic catalog has no page indicator.");
        Require(ui.Q("catalog-previous-button") != null &&
            ui.Q("catalog-next-button") != null,
            "Cosmetic catalog must support paging through the authentic inventory.");
        Require(ui.Q("catalog-apply-button") != null &&
            ui.Q("catalog-cancel-button") != null,
            "Cosmetic catalog must have explicit apply and cancel actions.");
        for (int i = 1; i <= 12; i++)
        {
            Require(ui.Q($"catalog-slot-{i:00}") != null,
                $"Cosmetic catalog is missing visible slot {i:00}.");
        }

        Debug.Log("[T2 Test] Authentic server cosmetic catalog is complete and selectable.");
    }

    private static List<int> ParseIds(string json)
    {
        List<int> ids = new List<int>();
        foreach (Match match in Regex.Matches(json, @"\d+"))
            ids.Add(int.Parse(match.Value));
        return ids;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
