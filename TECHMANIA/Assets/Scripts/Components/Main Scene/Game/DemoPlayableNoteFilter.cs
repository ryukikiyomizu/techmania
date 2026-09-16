using System.Collections.Generic;

public static class DemoPlayableNoteFilter
{
    public static void Apply(Pattern pattern, int firstVisibleScan,
        int lastVisibleScanExclusive)
    {
        if (pattern == null || firstVisibleScan >= lastVisibleScanExclusive)
        {
            return;
        }

        int beatsPerScan = pattern.patternMetadata.bps;
        int pulsesPerScan = Pattern.pulsesPerBeat * beatsPerScan;
        int firstVisiblePulse = firstVisibleScan * pulsesPerScan;
        int lastVisiblePulseExclusive =
            lastVisibleScanExclusive * pulsesPerScan;
        pattern.notes.RemoveWhere(note =>
        {
            if (pattern.IsHidden(note.lane)) return false;

            int endPulseExclusive = note.pulse;
            if (note is HoldNote holdNote)
            {
                endPulseExclusive += holdNote.duration;
            }
            else if (note is DragNote dragNote)
            {
                endPulseExclusive += dragNote.Duration();
            }

            return note.pulse < firstVisiblePulse ||
                note.pulse >= lastVisiblePulseExclusive ||
                endPulseExclusive > lastVisiblePulseExclusive;
        });

        // NoteManager links every ChainNode to the most recent ChainHead.
        // If trimming removed that head, remove its remaining nodes rather than
        // spawning an invalid partial chain at the start of the demo window.
        bool hasVisibleChainHead = false;
        List<Note> orphanChainNodes = new List<Note>();
        foreach (Note note in pattern.notes)
        {
            if (pattern.IsHidden(note.lane)) continue;

            if (note.type == NoteType.ChainHead)
            {
                hasVisibleChainHead = true;
            }
            else if (note.type == NoteType.ChainNode &&
                !hasVisibleChainHead)
            {
                orphanChainNodes.Add(note);
            }
        }
        foreach (Note note in orphanChainNodes)
        {
            pattern.notes.Remove(note);
        }
    }
}
