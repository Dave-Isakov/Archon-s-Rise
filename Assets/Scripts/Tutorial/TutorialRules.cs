using System.Collections.Generic;

// Pure tutorial state machine (M2.12 spec): rail advancement with
// out-of-order tolerance, one-shot dedupe + defer-past-rail, help-pulse seen
// set. The MonoBehaviour side (TutorialManager) owns events, UI, and
// PlayerPrefs; this class only decides. Unity-free: mcs/EditMode testable.
public enum RailChange
{
    None,
    StepChanged,
    RailCompleted,
}

public class TutorialRules
{
    public const int Done = -1;

    readonly List<string> stepEvents = new List<string>(); // "" = Next-button step
    readonly HashSet<string> firedSinceStart = new HashSet<string>();
    readonly HashSet<string> seenOneShots = new HashSet<string>();
    readonly HashSet<string> seenHelp = new HashSet<string>();
    readonly List<string> pendingOneShots = new List<string>();

    public bool Enabled { get; private set; }
    public int RailStep { get; private set; }

    public TutorialRules(IEnumerable<string> stepCompletionEvents, bool enabled, int savedStep,
        IEnumerable<string> seenOneShotIds, IEnumerable<string> seenHelpIds)
    {
        foreach (var e in stepCompletionEvents) stepEvents.Add(e ?? "");
        Enabled = enabled;
        RailStep = savedStep >= stepEvents.Count ? Done : savedStep;
        if (seenOneShotIds != null) foreach (var id in seenOneShotIds) seenOneShots.Add(id);
        if (seenHelpIds != null) foreach (var id in seenHelpIds) seenHelp.Add(id);
    }

    public bool RailActive
    {
        get { return Enabled && RailStep != Done; }
    }

    public bool CurrentStepIsInformational
    {
        get { return RailActive && stepEvents[RailStep].Length == 0; }
    }

    // Every referenced completion event funnels here. Events are recorded even
    // when they are not the current step's (or while tutoring is off), so a
    // step whose event already fired auto-completes the moment it becomes
    // current — the rail can never stall because the player explored ahead.
    public RailChange NotifyEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return RailChange.None;
        firedSinceStart.Add(eventId);
        if (!RailActive) return RailChange.None;
        if (stepEvents[RailStep] != eventId) return RailChange.None;
        return Advance();
    }

    // The banner's Next button — only informational steps use it.
    public RailChange NextPressed()
    {
        if (!CurrentStepIsInformational) return RailChange.None;
        return Advance();
    }

    RailChange Advance()
    {
        RailStep++;
        // Chain past steps whose event already fired; informational steps
        // always stop the chain (they wait for Next).
        while (RailStep < stepEvents.Count
            && stepEvents[RailStep].Length != 0
            && firedSinceStart.Contains(stepEvents[RailStep]))
            RailStep++;
        if (RailStep >= stepEvents.Count) { RailStep = Done; return RailChange.RailCompleted; }
        return RailChange.StepChanged;
    }

    // Skip: rail done AND the launch one-shots suppressed (spec: "Skip = rail
    // and one-shots off"), via per-one-shot seen flags so persistence stays
    // inside the specced keys.
    public RailChange Skip(IEnumerable<string> allOneShotIds)
    {
        if (RailStep == Done) return RailChange.None;
        RailStep = Done;
        if (allOneShotIds != null) foreach (var id in allOneShotIds) seenOneShots.Add(id);
        pendingOneShots.Clear();
        return RailChange.RailCompleted;
    }

    public void SetEnabled(bool on) { Enabled = on; }

    // A one-shot's trigger fired. True = show it now (marked seen). During the
    // rail it defers instead (rail steps outrank one-shots). While tutoring is
    // off it is dropped unrecorded — a later natural trigger can still show it
    // after re-enabling.
    public bool NotifyOneShot(string id)
    {
        if (!Enabled || string.IsNullOrEmpty(id)) return false;
        if (seenOneShots.Contains(id)) return false;
        if (RailActive)
        {
            if (!pendingOneShots.Contains(id)) pendingOneShots.Add(id);
            return false;
        }
        seenOneShots.Add(id);
        return true;
    }

    // After the send-off, deferred one-shots drain one at a time, oldest first.
    public bool TryDequeuePendingOneShot(out string id)
    {
        id = null;
        if (!Enabled || RailStep != Done) return false;
        while (pendingOneShots.Count > 0)
        {
            var candidate = pendingOneShots[0];
            pendingOneShots.RemoveAt(0);
            if (seenOneShots.Contains(candidate)) continue;
            seenOneShots.Add(candidate);
            id = candidate;
            return true;
        }
        return false;
    }

    // ? pulse: until the entry's first read, and only while tips are enabled.
    public bool ShouldPulseHelp(string panelId)
    {
        return Enabled && !string.IsNullOrEmpty(panelId) && !seenHelp.Contains(panelId);
    }

    public void MarkHelpSeen(string panelId)
    {
        if (!string.IsNullOrEmpty(panelId)) seenHelp.Add(panelId);
    }
}
