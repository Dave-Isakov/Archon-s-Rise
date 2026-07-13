using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Your army is full" flow: pick an existing unit to disband, then the hire
// completes through the caller's continuation (atomic: no state where influence
// is spent without a unit). Cancel is free.
//

[RequireComponent(typeof(Canvas))]
public class DisbandPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;    // vertical layout for unit buttons
    [SerializeField] GameObject entryButtonPrefab; // Button + TMP label
    [SerializeField] Button cancelButton;

    System.Action _onDisbanded;
    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas ??= GetComponent<Canvas>();

    void Start()
    {
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(Close);
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    // Generic "make room, then continue": combat recruiting and the town panel
    // both pass their own continuation. Cancel never runs it.
    public void OpenForHire(System.Action onDisbanded)
    {
        _onDisbanded = onDisbanded;
        ClearEntries();
        Canvas.enabled = true;

        foreach (var unit in FindObjectsByType<Unit>())
        {
            var go = Instantiate(entryButtonPrefab, entryContainer);
            go.GetComponentInChildren<TextMeshProUGUI>().text = unit.unitSO.cardName;
            var captured = unit;
            go.GetComponent<Button>().onClick.AddListener(() => DisbandAndContinue(captured));
            spawned.Add(go);
        }
    }

    void DisbandAndContinue(Unit unit)
    {
        var player = FindAnyObjectByType<Player>();
        player.DisbandUnit(unit);
        _onDisbanded?.Invoke();
        Close();
    }

    void Close()
    {
        ClearEntries();
        _onDisbanded = null;
        Canvas.enabled = false;
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
