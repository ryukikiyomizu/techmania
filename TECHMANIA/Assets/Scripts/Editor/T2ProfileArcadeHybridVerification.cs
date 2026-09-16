using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class T2ProfileArcadeHybridVerification
{
    [MenuItem("TECHMANIA/Tests/Verify T2 Profile Arcade Hybrid UI")]
    public static void Run()
    {
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/UI/MainTree.uxml");
        Require(asset != null, "MainTree.uxml could not be loaded.");

        TemplateContainer root = asset.Instantiate();
        VisualElement screen = root.Q("profile-editor-screen");
        Require(screen != null, "Profile editor screen is missing.");

        VisualElement stage = screen.Q("dj-info");
        Require(stage != null, "Profile customization stage is missing.");
        Require(stage.ClassListContains("profile-card-panel"),
            "Profile customization stage lost its panel contract.");

        RequireClass(stage, "profile-identity-hero");
        VisualElement grid = RequireClass(stage, "profile-cosmetic-grid");
        RequireClass(grid, "profile-cosmetics-heading");
        RequireClass(stage, "profile-card-state");

        RequireClass(grid, "profile-layer-editor");
        Require(grid.Query<VisualElement>(className: "profile-layer-editor")
                .ToList().Count == 2,
            "Card atelier must expose one DJ banner editor and one crew emblem editor.");

        VisualElement crewSettings = grid.Q("crew-settings");
        VisualElement djSettings = grid.Q("dj-settings");
        Require(crewSettings != null && djSettings != null,
            "Profile editor lost a cosmetic settings scope.");
        foreach (string categoryName in new[] { "crew-plate", "crew-pattern" })
        {
            VisualElement category = crewSettings.Q(categoryName);
            Require(category != null,
                $"Arcade cosmetic grid is missing {categoryName}.");
            Require(category.Q("browse-button") is Button,
                $"Arcade cosmetic tile {categoryName} has no touch action.");
            Require(category.Q("browse-button").ClassListContains(
                    "profile-layer-action"),
                $"Arcade cosmetic layer {categoryName} uses the cramped legacy action.");
            Require(category.Q("value") is Label,
                $"Arcade cosmetic tile {categoryName} has no selected ID.");
            Require(category.Q("thumbnail") != null,
                $"Arcade cosmetic tile {categoryName} has no live thumbnail.");
        }
        foreach (string categoryName in new[] { "dj-icon", "dj-plate", "dj-pattern" })
        {
            VisualElement category = djSettings.Q(categoryName);
            Require(category != null,
                $"Arcade cosmetic grid is missing {categoryName}.");
            Require(category.Q("browse-button") is Button,
                $"Arcade cosmetic tile {categoryName} has no touch action.");
            if (categoryName == "dj-icon")
                Require(category.Q("browse-button").ClassListContains(
                        "profile-tile-action-wide"),
                    "DJ icon editor lost its large arcade action.");
            else
                Require(category.Q("browse-button").ClassListContains(
                        "profile-layer-action"),
                    $"Arcade cosmetic layer {categoryName} uses the cramped legacy action.");
            Require(category.Q("value") is Label,
                $"Arcade cosmetic tile {categoryName} has no selected ID.");
            Require(category.Q("thumbnail") != null,
                $"Arcade cosmetic tile {categoryName} has no live thumbnail.");
        }

        Require(grid.Q("catalog-open-button") == null,
            "DJ Icon exposes two buttons that open the same catalog.");
        Require(crewSettings.parent == grid && djSettings.parent == grid,
            "Cosmetic editors must be siblings in the shared card-parts rail.");
        VisualElement formControls = grid.Q("profile-form-controls");
        Require(formControls != null,
            "Card atelier needs a dedicated full-width form coordinate space.");
        foreach (string fieldName in new[]
        {
            "no-crew-toggle", "crew-name-field", "dj-message-field"
        })
        {
            Require(formControls.Q(fieldName) != null,
                $"Card atelier form field {fieldName} escaped its full-width row.");
        }
        Require(formControls.Q("dj-level-field") == null,
            "DJ level must only use the authentic read-only plate in the live card.");
        TextField crewNameField = formControls.Q<TextField>("crew-name-field");
        TextField messageField = formControls.Q<TextField>("dj-message-field");
        Require(crewNameField.maxLength == 12 && messageField.maxLength == 12,
            "Crew name and DJ message must respect the 12-character T2 protocol fields.");
        Require(formControls.parent == grid,
            "Card atelier form row must be a sibling of both cosmetic editors.");
        RequireClass(stage, "profile-action-bar");
        Require(stage.Q("cancel-button").ClassListContains("profile-command-button") &&
            stage.Q("start-button").ClassListContains("profile-command-button"),
            "Back and Save must share the rationalized command-button geometry.");

        VisualElement catalog = screen.Q("cosmetic-catalog-overlay");
        Require(catalog != null, "Profile editor lost its cosmetic catalog.");
        RequireClass(catalog, "catalog-header-band");
        RequireClass(catalog, "catalog-grid-frame");
        RequireClass(catalog, "catalog-pager");
        RequireClass(catalog, "catalog-action-bar");
        Require(catalog.Query<Button>(className: "catalog-slot").ToList().Count == 12,
            "Arcade catalog must expose one complete 4 x 3 page of 12 items.");
        Require(catalog.Q("catalog-selection-label").parent.ClassListContains(
                "catalog-action-bar"),
            "Catalog selection and actions must share a dedicated footer band.");

        string[] existingBindings =
        {
            "preview", "no-crew-toggle", "no-card-toggle", "crew-name-field",
            "dj-name-field", "dj-message-field",
            "cancel-button", "start-button", "cosmetic-catalog-overlay"
        };
        foreach (string binding in existingBindings)
            Require(screen.Q(binding) != null,
                $"Profile behavior binding {binding} was removed.");

        VisualElement preview = stage.Q("preview");
        Require(preview.Q("crew-plate") != null &&
            preview.Q("dj-icon") != null && preview.Q("dj-plate") != null &&
            preview.Q("level-icon") != null,
            "Live card preview lost an authentic profile layer.");
        Require(preview.Q("dj-name") is Label &&
            preview.Q<Label>(className: "profile-preview-member") != null &&
            preview.Q<Label>(className: "profile-preview-data") != null,
            "Card atelier identity hierarchy is missing from the live card.");
        VisualElement auxiliary = preview.Q<VisualElement>(
            className: "profile-preview-auxiliary");
        Require(auxiliary != null && preview.Q("crew-plate").parent == auxiliary &&
            preview.Q("level-icon").parent == auxiliary,
            "Crew emblem and DJ level plate must share the live-card auxiliary bay.");
        Require(grid.Q("dj-layer-preview") != null &&
            grid.Q("crew-layer-preview") != null,
            "Combined DJ banner and crew emblem layer previews are missing.");
        Require(auxiliary.Query<Label>(className: "profile-preview-aux-title")
                .ToList().Count == 1 &&
            auxiliary.Q<Label>(className: "profile-preview-aux-title").text ==
                "CREW EMBLEM  /  DJ LEVEL" &&
            auxiliary.Q<Label>(className: "profile-level-title") == null,
            "Crew emblem and DJ level must use one unified auxiliary heading.");
        Require(stage.Q<Button>("cancel-button").text == string.Empty,
            "Back text is drawn twice when the baked T2 back asset also has a label.");
        Require(stage.Q<Label>("profile-session-state") != null &&
            stage.Q<Label>("profile-card-state-title") != null &&
            stage.Q<Label>("profile-card-state-detail") != null,
            "Session-state badge labels need stable runtime bindings.");
        Require(stage.Q("profile-card-state") != null,
            "Session-state badge container needs a stable runtime binding.");
        Require(!djSettings.Q("dj-icon").ClassListContains("profile-tile-selected"),
            "Orange selection styling must not be permanently attached to DJ Icon.");
        RequireClass(formControls, "profile-field-group-crew");
        RequireClass(formControls, "profile-field-group-session");
        foreach (string actionName in new[] { "start-button" })
            Require(stage.Q<Button>(actionName).ClassListContains(
                    "profile-atelier-action"),
                $"{actionName} does not use the shared T2 action component.");
        foreach (string categoryName in new[]
        {
            "crew-plate", "crew-pattern", "dj-icon", "dj-plate", "dj-pattern"
        })
        {
            VisualElement scope = categoryName.StartsWith("crew-")
                ? crewSettings : djSettings;
            Require(scope.Q(categoryName).Q<Button>("browse-button")
                    .ClassListContains("profile-atelier-action"),
                $"{categoryName} does not use the shared T2 action component.");
        }

        TextAsset profileScript = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/UI/Scripts/Profile Screen.txt");
        Require(profileScript != null,
            "Profile Screen.txt could not be loaded.");
        Require(profileScript.text.Contains(
                "local formControls = profileEditor.panel.Q(\"profile-form-controls\")") &&
            profileScript.text.Contains(
                "formControls.Q(\"crew-name-field\")") &&
            profileScript.text.Contains(
                "formControls.Q(\"dj-message-field\")"),
            "Profile controller still binds form fields through stale cosmetic parents.");
        Require(profileScript.text.Contains("NormalizeProtocolText = function(value)") &&
            profileScript.text.Contains(
                "profileEditor.NormalizeProtocolText(themeOptions.GetString(\"CrewName\"))") &&
            profileScript.text.Contains(
                "profileEditor.NormalizeProtocolText(themeOptions.GetString(\"DjMessage\"))") &&
            profileScript.text.Contains(
                "local normalized = profileEditor.NormalizeProtocolText(event.newValue)"),
            "Profile protocol fields are not normalized to the authentic 12-character packet limit.");
        Require(!profileScript.text.Contains(
                "formControls.Q(\"dj-level-field\")"),
            "Profile controller still queries the removed DJ-level input.");
        Require(profileScript.text.Contains("RefreshSessionBadge = function()") &&
            profileScript.text.Contains("GUEST SESSION") &&
            profileScript.text.Contains("NO USB CARD") &&
            profileScript.text.Contains("MACHINE RECORDS & OPTIONS"),
            "Profile controller does not bind the approved Guest badge state.");
        Require(profileScript.text.Contains(
                "t2ProfilePath .. \"back_button.png\"") &&
            !profileScript.text.Contains(
                "Q(\"cancel-button\").text"),
            "Authentic Back must remain a baked runtime texture with no injected label.");
        Require(!profileScript.text.Contains("Q(\"catalog-open-button\")"),
            "Profile controller still binds the duplicate DJ Icon catalog action.");

        string styleSource = System.IO.File.ReadAllText(
            "Assets/UI/MainStyle.uss");
        Require(!styleSource.Contains("join_now_hover.png"),
            "Generic profile actions still use the baked JOIN NOW hover label.");
        Require(StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-preview-plate {",
                "width: 306px;", "height: 96px;",
                "-unity-background-scale-mode: scale-to-fit;") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-preview-plate > VisualElement,",
                "-unity-background-scale-mode: scale-to-fit;"),
            "Live DJ and crew plate-pattern layers must preserve authentic asset ratios.");
        Require(StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-tile-action {",
                "profile_action_icon_132x42.png") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-layer-action {",
                "profile_action_layer_160x40.png") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-crew-editor .profile-layer-action {",
                "profile_action_crew_layer_173x40.png") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-save-button {",
                "profile_action_save_250x45.png"),
            "Every T2 action geometry must use its matching pre-rasterized background.");
        Require(!StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-atelier-action {",
                "background-image:") &&
            !StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-atelier-action {",
                "-unity-slice-left:"),
            "Shared T2 actions must not rescale one source bitmap at runtime.");
        RequireButtonSprite(
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_icon_132x42.png",
            132, 42);
        RequireButtonSprite(
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_layer_160x40.png",
            160, 40);
        RequireButtonSprite(
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_crew_layer_173x40.png",
            173, 40);
        RequireButtonSprite(
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_save_250x45.png",
            250, 45);
        string[] styleDependencies = AssetDatabase.GetDependencies(
            "Assets/UI/MainStyle.uss", false);
        foreach (string dependency in new[]
        {
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_icon_132x42.png",
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_layer_160x40.png",
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_crew_layer_173x40.png",
            "Assets/UI/Sprites/Login/T2/ProfileActions/profile_action_save_250x45.png"
        })
        {
            Require(Array.Exists(styleDependencies, candidate =>
                    string.Equals(candidate, dependency,
                        StringComparison.OrdinalIgnoreCase)),
                $"MainStyle.uss did not resolve generated action dependency: {dependency}");
        }
        Require(StyleBlockContains(styleSource,
                "#profile-editor-screen .identity-control > Label {",
                "width: 80px;", "min-width: 80px;", "max-width: 80px;") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .identity-control #unity-text-input {",
                "flex-grow: 1;", "min-width: 0;", "height: 34px;",
                "padding-top: 4px;", "padding-bottom: 4px;", "font-size: 13px;") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .message-control {",
                "width: 430px;"),
            "Profile text fields must reserve a readable input region and body size.");
        Require(StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-preview-icon {",
                "left: 51px;", "width: 104px;") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-preview-name {",
                "left: 167px;", "width: 190px;") &&
            StyleBlockContains(styleSource,
                "#profile-editor-screen .profile-preview-plate {",
                "left: 51px;", "width: 306px;"),
            "DJ icon/name lockup and DJ plate must share one centered 306px card column.");

        Debug.Log(
            "[T2 Profile UI Test] Arcade hybrid profile contract passed.");
    }

    private static VisualElement RequireClass(VisualElement root,
        string className)
    {
        VisualElement element = root.Q<VisualElement>(className: className);
        Require(element != null,
            $"Profile editor is missing required {className} composition.");
        return element;
    }

    private static bool StyleBlockContains(string source, string selector,
        params string[] declarations)
    {
        int selectorIndex = source.IndexOf(selector, StringComparison.Ordinal);
        if (selectorIndex < 0) return false;
        int blockStart = source.IndexOf('{', selectorIndex);
        int blockEnd = blockStart < 0 ? -1 : source.IndexOf('}', blockStart);
        if (blockStart < 0 || blockEnd < 0) return false;
        string block = source.Substring(blockStart, blockEnd - blockStart + 1);
        foreach (string declaration in declarations)
            if (!block.Contains(declaration)) return false;
        return true;
    }

    private static void RequireButtonSprite(string path, int width, int height)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Require(texture != null, $"Generated T2 action sprite is missing: {path}");
        Require(texture.width == width && texture.height == height,
            $"Generated T2 action sprite {path} must be {width}x{height}, " +
            $"but is {texture.width}x{texture.height}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
