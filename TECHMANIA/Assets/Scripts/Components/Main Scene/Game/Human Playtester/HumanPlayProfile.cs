using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class HumanPlayProfile
{
    public static HumanPlayPlan BuildPlan(Pattern pattern,
        ChartDifficultyAnalysis analysis, int seed)
    {
        System.Random random = new System.Random(seed);
        int lanes = Math.Max(1, pattern.patternMetadata.playableLanes);
        List<Note> notes = pattern.notes
            .Where(n => n != null && n.lane >= 0 && n.lane < lanes)
            .OrderBy(n => n.time)
            .ThenBy(n => n.lane)
            .ToList();
        List<HumanPlayAction> actions = new List<HumanPlayAction>();

        float fatigue = 0f;
        float recentError = 0f;
        float timingDrift = NextRange(random, -0.0045f, 0.0045f);
        int severeStreak = 0;
        float previousTime = notes.Count > 0 ? notes[0].time : 0f;

        foreach (Note note in notes)
        {
            float gap = Mathf.Max(0f, note.time - previousTime);
            previousTime = note.time;
            float pressure = analysis == null ? 0.15f : analysis.PressureFor(note);
            float recovery = Mathf.Clamp01((gap - 0.35f) / 0.9f);
            fatigue = Mathf.Clamp01(fatigue + pressure * 0.012f - recovery * 0.20f);
            recentError = Mathf.Clamp01(recentError - gap * 0.55f);
            timingDrift = Mathf.Clamp(timingDrift +
                NextRange(random, -0.0012f, 0.0012f), -0.010f, 0.010f);

            float strain = Mathf.Clamp01(pressure * 0.78f +
                fatigue * 0.30f + recentError * 0.18f - recovery * 0.22f);
            Judgement judgement = ChooseJudgement(random, strain,
                severeStreak >= 2);
            HumanPlayActionKind kind = HumanPlayActionKind.Hit;

            bool longNote = note is HoldNote || note is DragNote;
            if (judgement == Judgement.Break)
            {
                kind = HumanPlayActionKind.Skip;
                severeStreak++;
                recentError = Mathf.Clamp01(recentError + 0.42f);
            }
            else
            {
                float dropChance = longNote ? 0.006f + strain * 0.035f : 0f;
                if (random.NextDouble() < dropChance && severeStreak < 2)
                {
                    kind = HumanPlayActionKind.DropLongNote;
                    severeStreak++;
                    recentError = Mathf.Clamp01(recentError + 0.30f);
                }
                else if (judgement == Judgement.Miss ||
                    judgement == Judgement.Good)
                {
                    severeStreak++;
                    recentError = Mathf.Clamp01(recentError + 0.22f);
                }
                else
                {
                    severeStreak = 0;
                    recentError = Mathf.Max(0f, recentError - 0.05f);
                }
            }

            float offset = judgement == Judgement.Break ? 0f :
                OffsetForJudgement(note, judgement, random, timingDrift);
            float releaseTime = LongEndTime(note);
            if (kind == HumanPlayActionKind.DropLongNote)
            {
                float duration = Mathf.Max(0.1f, releaseTime - note.time);
                releaseTime = note.time + duration *
                    NextRange(random, 0.35f, 0.78f);
            }
            actions.Add(new HumanPlayAction(note, kind, judgement,
                offset, releaseTime));
        }

        PreventFullyPerfectLongPlan(actions, random);
        return new HumanPlayPlan(seed, actions);
    }

    private static Judgement ChooseJudgement(System.Random random,
        float strain, bool recoveryRequired)
    {
        if (recoveryRequired)
        {
            return random.NextDouble() < 0.72
                ? Judgement.RainbowMax : Judgement.Max;
        }

        double roll = random.NextDouble();
        float breakChance = 0.001f + strain * 0.010f;
        float missChance = 0.003f + strain * 0.024f;
        float goodChance = 0.006f + strain * 0.055f;
        float coolChance = 0.018f + strain * 0.135f;
        float maxChance = 0.18f + strain * 0.16f;

        if (roll < breakChance) return Judgement.Break;
        roll -= breakChance;
        if (roll < missChance) return Judgement.Miss;
        roll -= missChance;
        if (roll < goodChance) return Judgement.Good;
        roll -= goodChance;
        if (roll < coolChance) return Judgement.Cool;
        roll -= coolChance;
        if (roll < maxChance) return Judgement.Max;
        return Judgement.RainbowMax;
    }

    private static float OffsetForJudgement(Note note, Judgement judgement,
        System.Random random, float timingDrift)
    {
        if (note.timeWindow == null || !note.timeWindow.ContainsKey(judgement))
            return timingDrift;

        float outer = note.timeWindow[judgement];
        float inner = 0f;
        switch (judgement)
        {
            case Judgement.Max:
                inner = note.timeWindow[Judgement.RainbowMax];
                break;
            case Judgement.Cool:
                inner = note.timeWindow[Judgement.Max];
                break;
            case Judgement.Good:
                inner = note.timeWindow[Judgement.Cool];
                break;
            case Judgement.Miss:
                inner = note.timeWindow[Judgement.Good];
                break;
        }

        const float edgePadding = 0.0005f;
        float min = Mathf.Min(outer - edgePadding,
            inner + edgePadding);
        float max = Mathf.Max(min, outer - edgePadding);
        float magnitude;
        if (judgement == Judgement.RainbowMax)
        {
            magnitude = Mathf.Clamp(Mathf.Abs(timingDrift) +
                NextRange(random, 0f, outer * 0.72f), 0f, max);
        }
        else
        {
            magnitude = NextRange(random, min, max);
        }
        float signBias = timingDrift >= 0f ? 0.62f : 0.38f;
        return random.NextDouble() < signBias ? magnitude : -magnitude;
    }

    private static void PreventFullyPerfectLongPlan(
        List<HumanPlayAction> actions, System.Random random)
    {
        if (actions.Count < 20 || actions.Any(a =>
            a.kind != HumanPlayActionKind.Hit ||
            a.intendedJudgement != Judgement.RainbowMax)) return;

        int index = Mathf.Clamp(Mathf.RoundToInt(actions.Count * 0.60f),
            0, actions.Count - 1);
        HumanPlayAction old = actions[index];
        actions[index] = new HumanPlayAction(old.note,
            HumanPlayActionKind.Hit, Judgement.Max,
            OffsetForJudgement(old.note, Judgement.Max, random, 0f),
            old.longNoteReleaseTime);
    }

    private static float LongEndTime(Note note)
    {
        HoldNote hold = note as HoldNote;
        if (hold != null) return hold.endTime;
        DragNote drag = note as DragNote;
        return drag != null ? drag.endTime : note.time;
    }

    private static float NextRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
