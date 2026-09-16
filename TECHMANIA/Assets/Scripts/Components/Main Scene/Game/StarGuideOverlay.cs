using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Replays the authored T2 MainGame/guide hand states over Star notes.
// The theme owns the ten T2 textures; this class only applies VCE timing,
// orientation and note-state semantics.
public sealed class StarGuideOverlay
{
    private sealed class HandVisual
    {
        public VisualElement root;
        public readonly List<VisualElement> rightFrames =
            new List<VisualElement>();
        public readonly List<VisualElement> leftFrames =
            new List<VisualElement>();

        public NoteElements trackedNote;
        public Vector2 anchor;
        public bool pointsLeft;
    }

    // Evidence: t2/resource/MainGame/guide/*.vce.
    private const float T2FramesPerSecond = 60f;
    private const float GuideLeadSeconds = 0.5f;
    private const float TapTextureDelta = 0.25f;
    private const float RepeatTextureDelta = 0.5f;
    private const float HoldEntryFrames = 17f;      // touch_03(_left).vce
    private const float HoldTextureDelta = 0.25f;
    private const float HandSize = 128f;
    // touch_03.vce places the sprite root away from the note center so that
    // the fingertip—not the middle of the 128px texture—lands on the note.
    // This socket applies to every T2 hand state, including taps and chains.
    private const float RightTouchOffsetX = 40f;
    private const float RightTouchOffsetY = 10f;
    private const float LeftTouchOffsetX = -39f;
    private const float LeftTouchOffsetY = 6f;

    private readonly VisualElement container;
    private readonly NoteManager noteManager;
    private readonly GameTimer timer;
    private readonly List<HandVisual> hands = new List<HandVisual>();

    public StarGuideOverlay(VisualElement container, NoteManager noteManager,
        GameTimer timer)
    {
        this.container = container;
        this.noteManager = noteManager;
        this.timer = timer;

        if (container == null) return;
        foreach (VisualElement root in container.Children())
        {
            if (root == null ||
                !root.name.StartsWith("star-guide-hand-",
                    System.StringComparison.Ordinal)) continue;

            HandVisual hand = new HandVisual
            {
                root = root
            };
            for (int frame = 1; frame <= 5; frame++)
            {
                hand.rightFrames.Add(root.Q<VisualElement>(
                    $"frame-{frame}-right"));
                hand.leftFrames.Add(root.Q<VisualElement>(
                    $"frame-{frame}-left"));
            }
            hands.Add(hand);
            Hide(hand, clearTracking: true);
        }
    }

    public void Update(bool enabled)
    {
        if (container == null || !enabled)
        {
            HideAll();
            return;
        }

        foreach (HandVisual hand in hands)
        {
            if (RenderTracked(hand)) continue;
            Hide(hand, clearTracking: true);
        }

        var candidates = new List<NoteElements>();
        CollectCandidates(candidates);
        foreach (NoteElements candidate in candidates)
        {
            if (IsTracked(candidate)) continue;
            HandVisual hand = FindFreeHand();
            if (hand == null) break;
            Track(hand, candidate);
            if (!RenderTracked(hand)) Hide(hand, clearTracking: true);
        }
    }

    private void CollectCandidates(List<NoteElements> candidates)
    {
        foreach (NoteList lane in noteManager.notesInLane)
        {
            lane?.ForEachActive(holder =>
            {
                if (!(holder is NoteElements note)) return;
                NoteElements owner = GuideOwner(note);
                if (IsReadyForGuide(owner) && !candidates.Contains(owner))
                    candidates.Add(owner);
            });
        }
        candidates.Sort((left, right) =>
            left.note.time.CompareTo(right.note.time));
    }

    private static NoteElements GuideOwner(NoteElements note)
    {
        if (note is ChainNodeElements node && node.GuideChainHead != null)
            return node.GuideChainHead;
        return note;
    }

    private bool IsTracked(NoteElements note)
    {
        foreach (HandVisual hand in hands)
            if (hand.trackedNote == note) return true;
        return false;
    }

    private HandVisual FindFreeHand()
    {
        foreach (HandVisual hand in hands)
            if (hand.trackedNote == null) return hand;
        return null;
    }

    private bool IsReadyForGuide(NoteElements note)
    {
        if (note == null || note.noteImage == null) return false;
        if (note is ChainElementsBase chain)
        {
            if (note.state == NoteElements.State.Inactive) return false;
            ChainElementsBase last = chain;
            while (last.NextChainNodeForGuide != null)
                last = last.NextChainNodeForGuide;
            return StarGuideTimingPolicy.IsChainVisible(timer.gameTime,
                       last.note.time) &&
                (note.note.time - timer.gameTime <= GuideLeadSeconds ||
                    timer.gameTime >= note.note.time);
        }
        if (note.state == NoteElements.State.Inactive ||
            note.state == NoteElements.State.Resolved)
        {
            return false;
        }
        if (IsSustained(note))
        {
            return !IsPastSustainEnd(note) &&
                (note.note.time - timer.gameTime <= GuideLeadSeconds ||
                    note.state == NoteElements.State.Ongoing);
        }
        return note.note.time - timer.gameTime <= GuideLeadSeconds &&
            StarGuideTimingPolicy.IsTapVisible(timer.gameTime,
                note.note.time,
                note.state == NoteElements.State.Resolved);
    }

    private void Track(HandVisual hand, NoteElements note)
    {
        hand.trackedNote = note;
        NoteElements target = TargetNote(note);
        hand.pointsLeft = IsLeftFacing(target);
        UpdateAnchor(hand, target);
    }

    private bool RenderTracked(HandVisual hand)
    {
        NoteElements note = hand.trackedNote;
        if (note == null) return false;

        if (note is ChainElementsBase chain)
            return RenderChain(hand, chain);

        NoteElements target = TargetNote(note);
        UpdateAnchor(hand, target);

        if (IsSustained(note))
        {
            // The scan line has finished this sustain. Do not play the old
            // touch_03_2 release tail—the guide must disappear this frame.
            if (IsPastSustainEnd(note)) return false;

            if (note.state == NoteElements.State.Ongoing)
            {
                RenderHand(hand, frameIndex: 4, animatedAlpha: 1f);
                return true;
            }

            if (note.state == NoteElements.State.Resolved) return false;

            return RenderHoldApproach(hand, note);
        }

        return RenderTap(hand, note,
            note.note.type == NoteType.Repeat);
    }

    private bool RenderTap(HandVisual hand, NoteElements note, bool repeat)
    {
        if (!StarGuideTimingPolicy.IsTapVisible(timer.gameTime,
                note.note.time,
                note.state == NoteElements.State.Resolved))
        {
            return false;
        }

        float textureDelta = repeat
            ? RepeatTextureDelta : TapTextureDelta;
        float pressFrames = 4f / textureDelta;
        float timelineFrame = (timer.gameTime -
            (note.note.time - pressFrames / T2FramesPerSecond)) *
            T2FramesPerSecond;

        if (timelineFrame < 0f)
        {
            Hide(hand, clearTracking: false);
            return true;
        }
        int frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(timelineFrame * textureDelta), 0, 4);
        RenderHand(hand, frameIndex, animatedAlpha: 1f);
        return true;
    }

    private bool RenderChain(HandVisual hand, ChainElementsBase chain)
    {
        // Before the first node, retain the authored touch_01 press-in. Once
        // touched, hold t_05 and glide continuously through linked node
        // centers instead of restarting a tap animation for every node.
        if (timer.gameTime < chain.note.time)
            return RenderTap(hand, chain, repeat: false);

        ChainElementsBase segment = chain;
        while (segment.NextChainNodeForGuide != null &&
            timer.gameTime >= segment.NextChainNodeForGuide.note.time)
        {
            segment = segment.NextChainNodeForGuide;
        }

        hand.pointsLeft = IsLeftFacing(segment);
        ChainElementsBase next = segment.NextChainNodeForGuide;
        if (next == null)
        {
            if (!StarGuideTimingPolicy.IsChainVisible(timer.gameTime,
                    segment.note.time))
            {
                return false;
            }
            UpdateAnchor(hand, segment);
        }
        else
        {
            UpdateAnchorBetween(hand, segment, next);
        }

        RenderHand(hand, frameIndex: 4, animatedAlpha: 1f);
        return true;
    }

    private bool RenderHoldApproach(HandVisual hand, NoteElements note)
    {
        float entryFrame = (timer.gameTime -
            (note.note.time - HoldEntryFrames / T2FramesPerSecond)) *
            T2FramesPerSecond;
        if (entryFrame < 0f)
        {
            Hide(hand, clearTracking: false);
            return true;
        }

        int frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(entryFrame * HoldTextureDelta), 0, 4);
        RenderHand(hand, frameIndex, animatedAlpha: 1f);
        return true;
    }

    private void RenderHand(HandVisual hand, int frameIndex,
        float animatedAlpha)
    {
        SetAllLayersHidden(hand);

        float offsetX = hand.pointsLeft
            ? LeftTouchOffsetX : RightTouchOffsetX;
        float offsetY = hand.pointsLeft
            ? LeftTouchOffsetY : RightTouchOffsetY;
        hand.root.style.left = hand.anchor.x + offsetX;
        hand.root.style.top = hand.anchor.y + offsetY;
        hand.root.style.display = DisplayStyle.Flex;

        if (frameIndex >= 0 && frameIndex < 5)
        {
            List<VisualElement> frames = hand.pointsLeft
                ? hand.leftFrames : hand.rightFrames;
            ShowLayer(frames[frameIndex], animatedAlpha);
        }
    }

    private static void ShowLayer(VisualElement layer, float alpha)
    {
        if (layer == null || alpha <= 0f) return;
        layer.style.left = -HandSize * 0.5f;
        layer.style.top = -HandSize * 0.5f;
        layer.style.opacity = Mathf.Clamp01(alpha);
        layer.style.display = DisplayStyle.Flex;
    }

    private static void SetAllLayersHidden(HandVisual hand)
    {
        foreach (VisualElement frame in hand.rightFrames)
            if (frame != null) frame.style.display = DisplayStyle.None;
        foreach (VisualElement frame in hand.leftFrames)
            if (frame != null) frame.style.display = DisplayStyle.None;
    }

    private void UpdateAnchor(HandVisual hand, NoteElements target)
    {
        if (target?.noteImage == null) return;
        hand.anchor = container.WorldToLocal(target.noteImage.worldBound.center);
    }

    private void UpdateAnchorBetween(HandVisual hand, NoteElements from,
        NoteElements to)
    {
        if (from?.noteImage == null || to?.noteImage == null) return;
        Vector2 fromCenter = container.WorldToLocal(
            from.noteImage.worldBound.center);
        Vector2 toCenter = container.WorldToLocal(
            to.noteImage.worldBound.center);
        float progress = Mathf.InverseLerp(
            from.note.time, to.note.time, timer.gameTime);
        hand.anchor = Vector2.Lerp(fromCenter, toCenter, progress);
    }

    private static NoteElements TargetNote(NoteElements note)
    {
        if (note is RepeatNoteElementsBase repeatNote &&
            repeatNote.head != null)
        {
            return repeatNote.head;
        }
        return note;
    }

    private static bool IsSustained(NoteElements note)
    {
        if (note == null) return false;
        return note.note.type == NoteType.Hold ||
            note.note.type == NoteType.Drag ||
            note.note.type == NoteType.RepeatHeadHold ||
            note.note.type == NoteType.RepeatHold;
    }

    private bool IsActiveSustain(NoteElements note)
    {
        return IsSustained(note) &&
            note.state == NoteElements.State.Ongoing &&
            !IsPastSustainEnd(note);
    }

    private bool IsPastSustainEnd(NoteElements note)
    {
        if (note?.note is HoldNote holdNote)
            return timer.gameTime >= holdNote.endTime;
        if (note?.note is DragNote dragNote)
            return timer.gameTime >= dragNote.endTime;
        return false;
    }

    private bool IsChainInProgress(NoteElements note)
    {
        if (!(note is ChainElementsBase chain)) return false;
        ChainElementsBase last = chain;
        while (last.NextChainNodeForGuide != null)
            last = last.NextChainNodeForGuide;
        return StarGuideTimingPolicy.IsChainVisible(timer.gameTime,
            last.note.time);
    }

    private static bool IsLeftFacing(NoteElements note)
    {
        if (note?.layout == null) return false;
        GameLayout.ScanDirection direction = note.intScan % 2 == 0
            ? note.layout.evenScanDirection
            : note.layout.oddScanDirection;
        return direction == GameLayout.ScanDirection.Left;
    }

    private static void Hide(HandVisual hand, bool clearTracking)
    {
        hand.root.style.display = DisplayStyle.None;
        SetAllLayersHidden(hand);
        if (!clearTracking) return;
        hand.trackedNote = null;
    }

    public void HideAll()
    {
        foreach (HandVisual hand in hands)
            Hide(hand, clearTracking: true);
    }
}
