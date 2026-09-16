using System;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class T2DemoPlayableNoteFilterTests
{
    public static void Run()
    {
        Pattern pattern = new Pattern();
        pattern.patternMetadata.playableLanes = 4;
        pattern.patternMetadata.bps = 4;

        int pulsesPerScan = Pattern.pulsesPerBeat *
            pattern.patternMetadata.bps;

        pattern.notes.Add(Basic(scan: 4, lane: 0, pulsesPerScan));
        pattern.notes.Add(Basic(scan: 10, lane: 1, pulsesPerScan));
        pattern.notes.Add(Basic(scan: 14, lane: 2, pulsesPerScan));
        pattern.notes.Add(Basic(scan: 15, lane: 3, pulsesPerScan));
        pattern.notes.Add(Basic(scan: 4, lane: 64, pulsesPerScan));

        // GameSetup's runtime constructor registers itself with the live
        // GameController singleton. This editor test exercises its filtering
        // behavior without manufacturing a gameplay scene.
        ThemeApi.GameSetup setup = (ThemeApi.GameSetup)
            FormatterServices.GetUninitializedObject(
                typeof(ThemeApi.GameSetup));
        setup.patternAfterModifier = pattern;
        setup.demoPlayableNoteWindowEnabled = true;
        setup.demoPlayableNoteFirstScan = 10;
        setup.demoPlayableNoteLastScanExclusive = 15;
        setup.ApplyDemoPlayableNoteWindow();

        Note[] playable = pattern.notes
            .Where(note => !pattern.IsHidden(note.lane))
            .ToArray();
        int[] playableScans = playable
            .Select(note => note.GetScanNumber(pattern.patternMetadata.bps))
            .ToArray();

        Require(playableScans.SequenceEqual(new[] { 10, 14 }),
            "Only playable notes inside [10, 15) should remain.");
        Require(pattern.notes.Any(note => note.lane == 64),
            "Hidden backing/keysound notes must remain so demo audio timing is unchanged.");

        Pattern holdPattern = NewPattern();
        int holdPulsesPerScan = Pattern.pulsesPerBeat *
            holdPattern.patternMetadata.bps;
        holdPattern.notes.Add(new HoldNote
        {
            type = NoteType.Hold,
            pulse = 14 * holdPulsesPerScan,
            lane = 0,
            duration = 2 * holdPulsesPerScan,
            sound = ""
        });
        DemoPlayableNoteFilter.Apply(holdPattern, 10, 15);
        Require(!holdPattern.notes.Any(),
            "A hold crossing into the note-free tail must be removed entirely.");

        Pattern chainPattern = NewPattern();
        int chainPulsesPerScan = Pattern.pulsesPerBeat *
            chainPattern.patternMetadata.bps;
        chainPattern.notes.Add(new Note
        {
            type = NoteType.ChainHead,
            pulse = 9 * chainPulsesPerScan,
            lane = 0,
            sound = ""
        });
        chainPattern.notes.Add(new Note
        {
            type = NoteType.ChainNode,
            pulse = 10 * chainPulsesPerScan,
            lane = 1,
            sound = ""
        });
        DemoPlayableNoteFilter.Apply(chainPattern, 10, 15);
        Require(!chainPattern.notes.Any(note =>
            !chainPattern.IsHidden(note.lane)),
            "Removing a chain head must not leave orphan chain nodes.");

        VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/UI/MainTree.uxml");
        TemplateContainer root = tree.CloneTree();
        VisualElement gameScreen = root.Q<VisualElement>("game-screen");
        VisualElement guideLayer = root.Q<VisualElement>("star-guide-layer");
        VisualElement vfxLayer = root.Q<VisualElement>("vfx-layer");

        Require(guideLayer.parent == vfxLayer.parent,
            "Star guide and VFX must share a draw-order parent.");
        Require(guideLayer.parent.IndexOf(guideLayer) >
            guideLayer.parent.IndexOf(vfxLayer),
            "Star guide must be ordered after the VFX layer.");

        VisualElement topBar = root.Q<VisualElement>("top-bar-bg");
        VisualElement demoMeterClip = root.Q<VisualElement>(
            "demo-meter-background-clip");
        VisualElement demoMeterBackground = root.Q<VisualElement>(
            "demo-meter-background");
        Require(demoMeterClip != null && demoMeterClip.parent == topBar,
            "Demo Play needs a dedicated clipped meter-background layer.");
        Require(demoMeterBackground != null &&
            demoMeterBackground.parent == demoMeterClip,
            "The demo meter artwork must be clipped without resizing the source image.");

        Debug.Log("[T2 Test] Demo notes, guide layering, and meter clipping passed.");
    }

    private static Note Basic(int scan, int lane, int pulsesPerScan)
    {
        return new Note
        {
            type = NoteType.Basic,
            pulse = scan * pulsesPerScan,
            lane = lane,
            sound = ""
        };
    }

    private static Pattern NewPattern()
    {
        Pattern pattern = new Pattern();
        pattern.patternMetadata.playableLanes = 4;
        pattern.patternMetadata.bps = 4;
        return pattern;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
