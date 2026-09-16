using System.Collections.Generic;

public enum HumanPlayActionKind
{
    Hit,
    Skip,
    DropLongNote
}

public sealed class HumanPlayAction
{
    public Note note { get; private set; }
    public HumanPlayActionKind kind { get; private set; }
    public Judgement intendedJudgement { get; private set; }
    public float timingOffset { get; private set; }
    public float longNoteReleaseTime { get; private set; }

    public HumanPlayAction(Note note, HumanPlayActionKind kind,
        Judgement intendedJudgement, float timingOffset,
        float longNoteReleaseTime)
    {
        this.note = note;
        this.kind = kind;
        this.intendedJudgement = intendedJudgement;
        this.timingOffset = timingOffset;
        this.longNoteReleaseTime = longNoteReleaseTime;
    }
}

public sealed class HumanPlayPlan
{
    private readonly Dictionary<Note, HumanPlayAction> actionByNote;

    public int seed { get; private set; }
    public IReadOnlyList<HumanPlayAction> actions { get; private set; }

    public HumanPlayPlan(int seed, List<HumanPlayAction> actions)
    {
        this.seed = seed;
        this.actions = actions.AsReadOnly();
        actionByNote = new Dictionary<Note, HumanPlayAction>();
        foreach (HumanPlayAction action in actions)
            actionByNote[action.note] = action;
    }

    public HumanPlayAction ActionFor(Note note)
    {
        HumanPlayAction action;
        return note != null && actionByNote.TryGetValue(note, out action)
            ? action : null;
    }
}
