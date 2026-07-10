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
public class RecruitPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;             // root, inactive by default
    [SerializeField] Transform entryContainer;
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;
    [SerializeField] DisbandPanel disbandPanel;

    readonly List<GameObject> spawned = new();

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        // NOTE: do NOT SetActive(false) here. This component lives on the panel
        // it toggles, which starts inactive — so Start() first runs on the frame
        // Open() activates the panel, and hiding it here would swallow that first
        // open. The panel is kept inactive in the editor and re-hidden by Close().
    }

    public void Open(TownToken town)
    {
        ClearEntries();
        panel.SetActive(true);
        var player = FindAnyObjectByType<Player>();

        foreach (var unit in town.townSO.recruitableUnits)
        {
            if (unit == null) continue;
            var go = Instantiate(entryButtonPrefab, entryContainer);
            string summary = string.Join(" / ", unit.options.Select(UnitOptionText.Describe));
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{unit.cardName} — <sprite=\"gem\" index=0>{unit.influenceCost}\n<size=70%>{summary}</size>";
            go.GetComponent<Button>().interactable = player.playerInfluence >= unit.influenceCost;
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
    }

    void Close()
    {
        ClearEntries();
        panel.SetActive(false);
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
