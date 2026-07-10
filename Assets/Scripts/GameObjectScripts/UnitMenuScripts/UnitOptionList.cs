using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Builds one UnitOptionRow per authored option when the pop-out opens and
// re-binds them on every inspector change. Rows are rebuilt per Open because
// option counts differ per unit.
public class UnitOptionList : MonoBehaviour
{
    [SerializeField] UnitInspector inspector;
    [SerializeField] Transform rowContainer;
    [SerializeField] GameObject rowPrefab;
    [SerializeField] TextMeshProUGUI unitName;

    readonly List<UnitOptionRow> rows = new();
    public IReadOnlyList<UnitOptionRow> Rows => rows;

    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;

        if (unitName != null && inspector.Unit != null)
            unitName.text = inspector.Unit.unitSO.cardName;

        while (rows.Count < sel.Count)
        {
            var row = Instantiate(rowPrefab, rowContainer).GetComponent<UnitOptionRow>();
            int captured = rows.Count;
            row.button.onClick.AddListener(() => inspector.SelectOption(captured));
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < sel.Count;
            rows[i].gameObject.SetActive(active);
            if (active) rows[i].Bind(sel.Describe(i), sel.SelectedIndex == i, sel.IsAffordable(i));
        }
    }
}
