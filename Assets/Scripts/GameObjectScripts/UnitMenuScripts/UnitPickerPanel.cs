using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal "ready a spent unit" picker (spec 2026-07-14). DisbandPanel's shape:
// own Canvas toggled on/off, one button per unit, continuation callback. Opens
// with a refresh budget; only exhausted units list, entries over the remaining
// budget show disabled, each pick deducts the unit's influenceCost (min 1) and
// readies it via the callback. Clicking off — or nothing left affordable — closes.
// Not a reward modal: opens directly, never through RewardQueue.
[RequireComponent(typeof(Canvas))]
public class UnitPickerPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;     // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] TextMeshProUGUI titleLabel;   // "Refresh — 3 left"

    // M2.12: the tutorial banner hides while any picker is open.
    public static bool AnyOpen { get; private set; }

    System.Action<Unit> _onPick;
    int _remaining;
    readonly List<GameObject> spawned = new();

    enum PickerMode { Refresh, Wound, Heal }
    PickerMode _mode = PickerMode.Refresh;

    // Wound mode only. Selection is non-destructive — nothing commits until the
    // take-hit row is clicked — so a picked unit can be un-picked.
    readonly List<Unit> _committed = new();
    System.Action<IReadOnlyList<Unit>> _onConfirm;
    CounterattackPreview _preview;
    int _defendLeft, _toughness;
    EnemyTraitTuning _tuning;

    Canvas _canvas;
    Canvas Canvas => _canvas ??= GetComponent<Canvas>();

    void Start()
    {
        AnyOpen = false;
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    public void OpenForRefresh(int budget, System.Action<Unit> onPick)
    {
        AnyOpen = true;
        _mode = PickerMode.Refresh;
        _onPick = onPick;
        _remaining = budget;
        Canvas.enabled = true;
        Rebuild();
    }

    void Rebuild()
    {
        ClearEntries();
        if (titleLabel != null)
            titleLabel.text = $"{IconMarkup.Tag(IconConcept.Refresh)} Refresh — {_remaining} left";

        bool any = false;
        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (!unit.IsPlayed || unit.IsWounded) continue; // only spent, unwounded units list
            var go = Instantiate(entryButtonPrefab, entryContainer);
            int cost = RefreshRules.PickCost(unit.unitSO.influenceCost);
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Influence, cost)}";
            bool pickable = RefreshRules.CanPick(unit.IsPlayed, unit.IsWounded, unit.unitSO.influenceCost, _remaining);
            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), !pickable);
            if (pickable)
            {
                any = true;
                var captured = unit;
                button.onClick.AddListener(() => Pick(captured));
            }
            spawned.Add(go);
        }
        if (!any) Close(); // unspent budget is lost (spec) — nothing left to buy
    }

    void Pick(Unit unit)
    {
        _remaining -= RefreshRules.PickCost(unit.unitSO.influenceCost);
        _onPick?.Invoke(unit);
        Rebuild(); // unit stood up, so it drops off the list; budget re-renders
    }

    // Public so the ClickOffCatcher can bind to it. Closing early forfeits any
    // unspent refresh budget, which is the shipped rule (spec 2026-07-14) — the
    // old Done button did exactly the same thing. Wound mode refuses: the player
    // must not be able to click off the counterattack, so it exits through the
    // take-hit row, which calls CloseInternal directly.
    public void Close()
    {
        if (_mode == PickerMode.Wound)
        {
            GameLog.Instance.Post("Choose who takes the hit, then confirm.");
            return;
        }
        CloseInternal();
    }

    void CloseInternal()
    {
        AnyOpen = false;
        ClearEntries();
        _onPick = null;
        _onConfirm = null;
        _committed.Clear();
        _mode = PickerMode.Refresh;
        Canvas.enabled = false;
    }

    // Opens the "who takes this hit?" picker. Every row is a body that can
    // absorb the counterattack, and the player is the last one.
    public void OpenForWounds(CounterattackPreview preview, int defendLeft, int toughness,
        EnemyTraitTuning tuning, System.Action<IReadOnlyList<Unit>> onConfirm)
    {
        AnyOpen = true;
        _mode = PickerMode.Wound;
        _preview = preview;
        _defendLeft = defendLeft;
        _toughness = toughness;
        _tuning = tuning;
        _onConfirm = onConfirm;
        _committed.Clear();
        Canvas.enabled = true;
        RebuildWounds();
    }

    int CommittedSoak()
    {
        int soak = 0;
        foreach (var u in _committed) if (u != null) soak += u.ArmorClass;
        return soak;
    }

    CounterattackOutcome CurrentOutcome()
        => EnemyTraitRules.Resolve(_preview, _defendLeft, CommittedSoak(),
                                   _toughness, _tuning, _committed.Count);

    void RebuildWounds()
    {
        ClearEntries();

        var outcome = CurrentOutcome();
        int reduced = _preview.UnblockedThreat - CommittedSoak();
        if (reduced < 0) reduced = 0;

        if (titleLabel != null)
            titleLabel.text = CommittedSoak() > 0
                ? $"{IconMarkup.Tag(IconConcept.Attack)}{_preview.UnblockedThreat} -> {IconMarkup.Tag(IconConcept.Attack)}{reduced}"
                : $"{IconMarkup.Tag(IconConcept.Attack)}{_preview.UnblockedThreat}";

        // Exhausted units qualify — taking a hit is not USING the unit.
        foreach (var unit in FindObjectsByType<Unit>())
        {
            if (unit.IsWounded) continue;

            var go = Instantiate(entryButtonPrefab, entryContainer);
            bool picked = _committed.Contains(unit);
            bool pickable = picked || (unit.ArmorClass > 0 && outcome.HandWounds > 0);

            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{(picked ? "> " : "")}{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Defend, unit.ArmorClass)}";

            var button = go.GetComponent<Button>();
            button.interactable = pickable;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), !pickable);
            if (pickable)
            {
                var captured = unit;
                button.onClick.AddListener(() => TogglePick(captured));
            }
            spawned.Add(go);
        }

        // The take-hit row: always last, always live, and the only exit.
        var takeGo = Instantiate(entryButtonPrefab, entryContainer);
        var takeLabel = takeGo.GetComponentInChildren<TextMeshProUGUI>();
        takeLabel.text = $"Take {outcome.HandWounds} {IconMarkup.Tag(IconConcept.Wound)}";
        takeLabel.color = new Color(0.90f, 0.20f, 0.20f);
        var takeButton = takeGo.GetComponent<Button>();
        takeButton.interactable = true;
        UiLock.Apply(takeGo.GetComponent<CanvasGroup>(), false);
        takeButton.onClick.AddListener(ConfirmWounds);
        spawned.Add(takeGo);
    }

    void TogglePick(Unit unit)
    {
        if (!_committed.Remove(unit)) _committed.Add(unit);
        RebuildWounds();
    }

    void ConfirmWounds()
    {
        var result = new List<Unit>(_committed);
        var callback = _onConfirm;
        CloseInternal();
        callback?.Invoke(result);
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
