using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Your army is full" flow: pick an existing unit to disband, then the hire
// completes through the exact same events RecruitButton fires (atomic: no
// state where influence is spent without a unit). Cancel is free.
public class DisbandPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;            // root, inactive by default
    [SerializeField] Transform entryContainer;    // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;
    [SerializeField] TownEvent townEvent;          // same asset RecruitButton raises
    [SerializeField] IntEvent influenceCostEvent;  // same asset RecruitButton raises

    TownToken _town;
    readonly List<GameObject> spawned = new();

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public void Open(TownToken town)
    {
        _town = town;
        ClearEntries();
        panel.SetActive(true);

        foreach (var unit in FindObjectsByType<Unit>())
        {
            var go = Instantiate(entryButtonPrefab, entryContainer);
            go.GetComponentInChildren<TextMeshProUGUI>().text = unit.unitSO.cardName;
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => DisbandAndHire(captured));
            spawned.Add(go);
        }
    }

    void DisbandAndHire(Unit unit)
    {
        var player = FindAnyObjectByType<Player>();
        player.DisbandUnit(unit);
        // Same two events the normal Recruit click raises: hire + spend.
        townEvent.Raise(_town);
        influenceCostEvent.Raise(_town.townSO.recruitLevel);
        Close();
    }

    void Close()
    {
        ClearEntries();
        panel.SetActive(false);
        _town = null;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
