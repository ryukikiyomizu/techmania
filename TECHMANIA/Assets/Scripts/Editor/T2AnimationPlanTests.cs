using System;
using UnityEngine;

public static class T2AnimationPlanTests
{
    public static void Run()
    {
        StarGuideTiming();
        Debug.Log("[T2 Test] Selected animation-plan regressions passed.");
    }

    private static void StarGuideTiming()
    {
        const float noteTime = 12f;
        Require(StarGuideTimingPolicy.IsTapVisible(11.999f, noteTime,
                resolved: false),
            "Tap/repeat guide must remain visible immediately before judgement.");
        Require(!StarGuideTimingPolicy.IsTapVisible(noteTime, noteTime,
                resolved: false),
            "Tap/repeat guide must disappear exactly at judgement.");
        Require(!StarGuideTimingPolicy.IsTapVisible(12.001f, noteTime,
                resolved: false),
            "Tap/repeat guide must stay hidden after judgement.");
        Require(!StarGuideTimingPolicy.IsTapVisible(11.999f, noteTime,
                resolved: true),
            "Resolved tap/repeat guide must disappear immediately.");

        Require(StarGuideTimingPolicy.IsChainVisible(19.999f, 20f),
            "Chain guide must remain visible before its final node.");
        Require(!StarGuideTimingPolicy.IsChainVisible(20f, 20f),
            "Chain guide must disappear exactly at its final node.");
        Require(!StarGuideTimingPolicy.IsChainVisible(20.001f, 20f),
            "Chain guide must stay hidden after its final node.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
