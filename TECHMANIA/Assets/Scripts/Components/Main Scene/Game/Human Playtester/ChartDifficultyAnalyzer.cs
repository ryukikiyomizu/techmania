using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ChartChokeSegment
{
    public float startTime { get; private set; }
    public float endTime { get; private set; }
    public float peakPressure { get; private set; }
    public string dominantCause { get; private set; }

    public ChartChokeSegment(float startTime, float endTime,
        float peakPressure, string dominantCause)
    {
        this.startTime = startTime;
        this.endTime = endTime;
        this.peakPressure = peakPressure;
        this.dominantCause = dominantCause;
    }

    internal void Extend(float newEndTime, float pressure, string cause)
    {
        endTime = newEndTime;
        if (pressure <= peakPressure) return;
        peakPressure = pressure;
        dominantCause = cause;
    }
}

public sealed class ChartDifficultyAnalysis
{
    private readonly Dictionary<Note, float> pressureByNote;

    public float overallPressure { get; private set; }
    public IReadOnlyList<ChartChokeSegment> chokes { get; private set; }

    internal ChartDifficultyAnalysis(float overallPressure,
        Dictionary<Note, float> pressureByNote,
        List<ChartChokeSegment> chokes)
    {
        this.overallPressure = overallPressure;
        this.pressureByNote = pressureByNote;
        this.chokes = chokes.AsReadOnly();
    }

    public float PressureFor(Note note)
    {
        float pressure;
        return note != null && pressureByNote.TryGetValue(note, out pressure)
            ? pressure : 0f;
    }
}

public static class ChartDifficultyAnalyzer
{
    private const float kChokeThreshold = 0.58f;
    private const float kChokeMergeGap = 0.35f;

    public static ChartDifficultyAnalysis Analyze(Pattern pattern)
    {
        if (pattern == null || pattern.notes == null)
        {
            return new ChartDifficultyAnalysis(0f,
                new Dictionary<Note, float>(),
                new List<ChartChokeSegment>());
        }

        int lanes = Math.Max(1, pattern.patternMetadata.playableLanes);
        List<Note> notes = pattern.notes
            .Where(n => n != null && n.lane >= 0 && n.lane < lanes)
            .OrderBy(n => n.time)
            .ThenBy(n => n.lane)
            .ToList();
        Dictionary<Note, float> pressures = new Dictionary<Note, float>();
        Dictionary<Note, string> causes = new Dictionary<Note, string>();
        List<Note> activeLongNotes = new List<Note>();

        for (int i = 0; i < notes.Count; i++)
        {
            Note note = notes[i];
            activeLongNotes.RemoveAll(n => LongEndTime(n) <= note.time);

            int shortDensity = CountNeighbors(notes, i, 0.22f);
            int mediumDensity = CountNeighbors(notes, i, 0.75f);
            int chordSize = CountNeighbors(notes, i, 0.035f);
            float travel = 0f;
            float reversal = 0f;
            float repeat = 0f;
            float transition = 0f;
            float recovery = 0f;

            if (i > 0)
            {
                Note previous = notes[i - 1];
                float delta = note.time - previous.time;
                if (delta < 0.45f)
                {
                    travel = Mathf.Abs(note.lane - previous.lane) /
                        (float)Math.Max(1, lanes - 1);
                    repeat = note.lane == previous.lane && delta < 0.18f
                        ? 1f : 0f;
                    transition = note.type != previous.type ? 1f : 0f;
                }
                if (delta > 0.65f) recovery = 1f;
                if (i > 1 && delta < 0.35f)
                {
                    Note beforePrevious = notes[i - 2];
                    int firstDirection = previous.lane - beforePrevious.lane;
                    int secondDirection = note.lane - previous.lane;
                    if (firstDirection != 0 && secondDirection != 0 &&
                        Math.Sign(firstDirection) != Math.Sign(secondDirection))
                    {
                        reversal = 1f;
                    }
                }
            }

            float overlap = Mathf.Clamp01(activeLongNotes.Count / 2f);
            float dragComplexity = 0f;
            DragNote drag = note as DragNote;
            if (drag != null && drag.nodes != null)
            {
                dragComplexity = Mathf.Clamp01((drag.nodes.Count - 2) / 6f);
            }

            float raw =
                shortDensity * 0.105f +
                mediumDensity * 0.022f +
                chordSize * 0.12f +
                travel * 0.18f +
                reversal * 0.13f +
                repeat * 0.09f +
                overlap * 0.18f +
                dragComplexity * 0.16f +
                transition * 0.05f -
                recovery * 0.16f;
            float pressure = Mathf.Clamp01(raw);
            pressures[note] = pressure;
            causes[note] = DominantCause(shortDensity, chordSize, travel,
                reversal, repeat, overlap, dragComplexity);

            if (LongEndTime(note) > note.time)
            {
                activeLongNotes.Add(note);
            }
        }

        float overall = 0f;
        if (notes.Count > 0)
        {
            float average = pressures.Values.Average();
            List<float> sorted = pressures.Values.OrderBy(v => v).ToList();
            float upperQuartile = sorted[Mathf.Clamp(
                Mathf.FloorToInt((sorted.Count - 1) * 0.75f),
                0, sorted.Count - 1)];
            overall = Mathf.Clamp01(average * 0.65f + upperQuartile * 0.35f);
        }

        return new ChartDifficultyAnalysis(overall, pressures,
            BuildChokes(notes, pressures, causes));
    }

    private static int CountNeighbors(List<Note> notes, int index,
        float radius)
    {
        int count = 0;
        float time = notes[index].time;
        for (int i = index - 1; i >= 0 && time - notes[i].time <= radius; i--)
            count++;
        for (int i = index + 1; i < notes.Count && notes[i].time - time <= radius; i++)
            count++;
        return count;
    }

    private static float LongEndTime(Note note)
    {
        HoldNote hold = note as HoldNote;
        if (hold != null) return hold.endTime;
        DragNote drag = note as DragNote;
        return drag != null ? drag.endTime : note.time;
    }

    private static string DominantCause(int density, int chord, float travel,
        float reversal, float repeat, float overlap, float drag)
    {
        float best = density * 0.105f;
        string cause = "density";
        if (chord * 0.12f > best) { best = chord * 0.12f; cause = "chords"; }
        if (travel * 0.18f > best) { best = travel * 0.18f; cause = "travel"; }
        if (reversal * 0.13f > best) { best = reversal * 0.13f; cause = "reversals"; }
        if (repeat * 0.09f > best) { best = repeat * 0.09f; cause = "repeats"; }
        if (overlap * 0.18f > best) { best = overlap * 0.18f; cause = "overlap"; }
        if (drag * 0.16f > best) cause = "drag";
        return cause;
    }

    private static List<ChartChokeSegment> BuildChokes(List<Note> notes,
        Dictionary<Note, float> pressures,
        Dictionary<Note, string> causes)
    {
        List<ChartChokeSegment> result = new List<ChartChokeSegment>();
        ChartChokeSegment current = null;
        foreach (Note note in notes)
        {
            float pressure = pressures[note];
            if (pressure < kChokeThreshold) continue;
            if (current == null || note.time - current.endTime > kChokeMergeGap)
            {
                current = new ChartChokeSegment(note.time, note.time,
                    pressure, causes[note]);
                result.Add(current);
            }
            else
            {
                current.Extend(note.time, pressure, causes[note]);
            }
        }
        return result;
    }
}
