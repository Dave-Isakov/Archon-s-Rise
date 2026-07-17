using System.Collections.Generic;
using UnityEngine;

// Scene manager for the M2.12 tutorial, on the TutorialCanvas root (always
// active). It hosts ALL event listeners — wired listener components on this
// object, never on the banner/popup children, avoiding the
// self-disabling-listener trap — owns the pure TutorialRules state, persists
// via TutorialPrefs, and drives the banner + highlight frame. Tips disabled
// hides banner/frame and mutes one-shots/pulses; the canvas GameObject stays
// active so the always-available ? popup keeps working.
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [SerializeField] List<TutorialStepSO> railSteps = new();
    [SerializeField] List<TutorialOneShotSO> oneShots = new();
    [SerializeField] List<HelpEntrySO> helpEntries = new(); // reset needs the full key list
    [SerializeField] TutorialBanner banner;
    [SerializeField] HighlightFrame highlight;
    [SerializeField] DoomTuningSO doomTuning;
    [SerializeField] CanvasGroup canvasGroup; // whole TutorialCanvas content

    TutorialRules rules;
    bool tipShowing; // a one-shot currently occupies the banner

    void Awake()
    {
        Instance = this;
        BuildRules();
    }

    void BuildRules()
    {
        var seenOneShots = new List<string>();
        foreach (var o in oneShots)
            if (TutorialPrefs.OneShotSeen(o.id)) seenOneShots.Add(o.id);
        var seenHelp = new List<string>();
        foreach (var h in helpEntries)
            if (TutorialPrefs.HelpSeen(h.panelId)) seenHelp.Add(h.panelId);
        var stepEvents = new List<string>();
        foreach (var s in railSteps) stepEvents.Add(s.completionEventId ?? "");
        rules = new TutorialRules(stepEvents, TutorialPrefs.Enabled, TutorialPrefs.RailStep,
            seenOneShots, seenHelp);
        tipShowing = false;
    }

    void Start() => RefreshRailUi();

    // Modal interplay (spec): everything tutorial hides while a queued modal
    // or a picker canvas is open, and permanently on the terminal run-end
    // screen — and reappears after.
    static bool Suppressed =>
        RunEndController.HasEnded
        || RewardQueue.Instance.Busy
        || UnitPickerPanel.AnyOpen
        || DisbandPanel.AnyOpen;

    void Update()
    {
        bool visible = !Suppressed;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        if (!visible) return;

        // Drain deferred one-shots once the rail is done and the banner idle.
        if (!tipShowing && !rules.RailActive && rules.TryDequeuePendingOneShot(out var id))
        {
            TutorialPrefs.MarkOneShot(id);
            ShowTip(FindOneShot(id));
        }
    }

    // ---- event intake ----

    // Target of every wired listener's UnityEvent response, in STATIC mode
    // with the stable event id typed as the string argument.
    public void NotifyEvent(string eventId)
    {
        Apply(rules.NotifyEvent(eventId));

        foreach (var tip in oneShots)
            if (tip.triggerEventId == eventId && rules.NotifyOneShot(tip.id))
            {
                TutorialPrefs.MarkOneShot(tip.id);
                ShowTip(tip);
                break; // one banner at a time
            }
    }

    // IntListener (DYNAMIC int) on OnDoomChanged_UpdateMeter: entering the
    // flagged (mid) band is derived from the doom value, not raised anywhere.
    public void NotifyDoom(int doom)
    {
        if (doomTuning != null && doom > doomTuning.tuning.lowBandMax)
            NotifyEvent("doom-band");
    }

    // ---- banner buttons (wired in the editor) ----

    public void NextPressed()
    {
        if (tipShowing) { DismissTip(); return; }
        Apply(rules.NextPressed());
    }

    public void SkipPressed()
    {
        var all = new List<string>();
        foreach (var o in oneShots) all.Add(o.id);
        rules.Skip(all);
        foreach (var id in all) TutorialPrefs.MarkOneShot(id);
        TutorialPrefs.RailStep = TutorialRules.Done;
        tipShowing = false;
        RefreshRailUi();
    }

    // ---- settings surface (MainMenu) ----

    public bool TipsEnabled => rules != null && rules.Enabled;

    public void SetTipsEnabled(bool on)
    {
        rules.SetEnabled(on);
        TutorialPrefs.Enabled = on;
        tipShowing = false;
        RefreshRailUi();
    }

    public void ResetTutorial()
    {
        var oneShotIds = new List<string>();
        foreach (var o in oneShots) oneShotIds.Add(o.id);
        var helpIds = new List<string>();
        foreach (var h in helpEntries) helpIds.Add(h.panelId);
        TutorialPrefs.ResetAll(oneShotIds, helpIds);
        BuildRules(); // fresh defaults → the rail restarts at the welcome step
        RefreshRailUi();
    }

    // ---- help (? icons) ----

    public bool ShouldPulseHelp(string panelId) => rules.ShouldPulseHelp(panelId);

    public void MarkHelpSeen(string panelId)
    {
        rules.MarkHelpSeen(panelId);
        TutorialPrefs.MarkHelp(panelId);
    }

    // ---- internals ----

    void Apply(RailChange change)
    {
        if (change == RailChange.None) return;
        TutorialPrefs.RailStep = rules.RailStep;
        RefreshRailUi();
    }

    void RefreshRailUi()
    {
        if (rules.RailActive)
        {
            var step = railSteps[rules.RailStep];
            banner.ShowStep(step.bannerText, rules.CurrentStepIsInformational);
            highlight.Show(step.highlightTargetId);
        }
        else if (!tipShowing)
        {
            banner.HideAll();
            highlight.Hide();
        }
    }

    TutorialOneShotSO FindOneShot(string id)
    {
        foreach (var o in oneShots) if (o.id == id) return o;
        return null;
    }

    void ShowTip(TutorialOneShotSO tip)
    {
        if (tip == null) return;
        tipShowing = true;
        banner.ShowTip(tip.bannerText);
        highlight.Show(tip.highlightTargetId);
    }

    void DismissTip()
    {
        tipShowing = false;
        banner.HideAll();
        highlight.Hide();
    }
}
