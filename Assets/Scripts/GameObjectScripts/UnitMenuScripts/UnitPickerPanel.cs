using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Modal "ready a spent unit" picker (spec 2026-07-14). DisbandPanel's shape:
// own Canvas toggled on/off, one button per unit, continuation callback. Opens
// with a refresh budget; only exhausted units list, entries over the remaining
// budget show disabled, each pick deducts the unit's influenceCost (min 1) and
// readies it via the callback. Done — or nothing left affordable — closes.
// Not a reward modal: opens directly, never through RewardQueue.
[RequireComponent(typeof(Canvas))]
public class UnitPickerPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;     // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button doneButton;
    [SerializeField] TextMeshProUGUI titleLabel;   // "Refresh — 3 left"

    // M2.12: the tutorial banner hides while any picker is open.
    public static bool AnyOpen { get; private set; }

    System.Action<Unit> _onPick;
    int _remaining;
    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas ??= GetComponent<Canvas>();

    void Start()
    {
        doneButton.onClick.RemoveAllListeners();
        doneButton.onClick.AddListener(Close);
        AnyOpen = false;
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    public void OpenForRefresh(int budget, System.Action<Unit> onPick)
    {
        AnyOpen = true;
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
            if (!unit.IsPlayed) continue; // only spent units list
            var go = Instantiate(entryButtonPrefab, entryContainer);
            int cost = RefreshRules.PickCost(unit.unitSO.influenceCost);
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.unitSO.cardName} — {IconMarkup.Cost(IconConcept.Influence, cost)}";
            bool pickable = RefreshRules.CanPick(unit.IsPlayed, unit.unitSO.influenceCost, _remaining);
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

    void Close()
    {
        AnyOpen = false;
        ClearEntries();
        _onPick = null;
        Canvas.enabled = false;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
