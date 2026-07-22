using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Town hiring: pick WHICH unit at ITS OWN influence price (spec 2026-07-09;
// replaces the silent recruitableUnits[0] + flat recruitLevel flow). Mirrors
// DisbandPanel's build/clear pattern; unaffordable entries stay visible but
// disabled. Standard Buttons with default navigation, so the later towns
// controller pass needs no rework.
//
// Visibility convention (shared with DisbandPanel and the other modals): the
// GameObject stays active and we toggle the Canvas component; Start() force-
// closes it so the authored checkbox can't leave it stuck, and always runs to
// wire the cancel button. See DisbandPanel for the full rationale.
[RequireComponent(typeof(Canvas))]
public class RecruitPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;
    [SerializeField] DisbandPanel disbandPanel;

    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponent<Canvas>());

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    public void Open(TownToken town)
    {
        ClearEntries();
        Canvas.enabled = true;
        var player = FindAnyObjectByType<Player>();

        foreach (var unit in town.townSO.recruitableUnits)
        {
            if (unit == null) continue;
            var go = Instantiate(entryButtonPrefab, entryContainer);
            string summary = string.Join(" / ", unit.options.Select(UnitOptionText.Describe));
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.cardName} — {IconMarkup.Cost(IconConcept.Influence, unit.influenceCost)}\n<size=70%>{summary}</size>";
            go.GetComponent<Button>().interactable = player.playerInfluence >= unit.influenceCost;
            UiLock.Apply(go.GetComponent<CanvasGroup>(), player.playerInfluence < unit.influenceCost);
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => Pick(captured));
            spawned.Add(go);
        }
    }

    void Pick(UnitsSO unit)
    {
        var player = FindAnyObjectByType<Player>();
        if (ArmyRules.NeedsDisband(player.Units.Count, player.ArmyCap))
        {
            disbandPanel.OpenForHire(() => Hire(unit));
            Close();
            return;
        }
        Hire(unit);
        Close();
    }

    void Hire(UnitsSO unit)
    {
        var player = FindAnyObjectByType<Player>();
        player.AddUnit(unit);
        // Spend the unit's Influence directly. (Was raised through a serialized
        // IntEvent that had been mis-wired to GetCurrentInfluence — which only
        // rebroadcasts the number — so hiring never actually deducted.)
        player.Influence(unit.influenceCost);
        // Recruiting is the visit's committed action (spec 2026-07-22).
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
    }

    void Close()
    {
        ClearEntries();
        Canvas.enabled = false;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
