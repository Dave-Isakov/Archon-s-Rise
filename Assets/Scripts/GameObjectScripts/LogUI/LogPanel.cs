using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The openable message history (spec 2026-07-28). Newest first, with a day
// header wherever the core says one belongs. Rebuilt on open rather than kept
// live: it is a review surface, not a HUD element.
//
// Closes by clicking off, per the 2026-07-28 sweep. No exit button.
[RequireComponent(typeof(Canvas))]
public class LogPanel : MonoBehaviour
{
    [SerializeField] Transform entryContainer;      // vertical layout inside a scroll view
    [SerializeField] GameObject entryPrefab;        // TMP text
    [SerializeField] GameObject dayHeaderPrefab;    // TMP text, styled as a divider
    [SerializeField] ClickOffCatcher catcher;

    readonly List<GameObject> spawned = new();

    Canvas _canvas;
    Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponent<Canvas>());

    void Start()
    {
        Canvas.enabled = false; // start closed regardless of the authored state
    }

    // Wired to the HUD's log button.
    public void Toggle()
    {
        if (Canvas.enabled) Close();
        else Open();
    }

    public void Open()
    {
        Rebuild();
        Canvas.enabled = true;
        if (catcher != null) catcher.SetArmed(true);
    }

    // Public so the ClickOffCatcher's UnityEvent can bind to it.
    public void Close()
    {
        ClearEntries();
        Canvas.enabled = false;
        if (catcher != null) catcher.SetArmed(false);
    }

    void Rebuild()
    {
        ClearEntries();
        var log = GameLog.Instance.Log;
        for (int i = 0; i < log.Entries.Count; i++)
        {
            if (log.NeedsDayDivider(i))
                Spawn(dayHeaderPrefab, $"Day {log.Entries[i].Day}");
            Spawn(entryPrefab, log.Entries[i].Text);
        }
    }

    void Spawn(GameObject prefab, string text)
    {
        if (prefab == null || entryContainer == null) return;
        var go = Instantiate(prefab, entryContainer);
        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
        spawned.Add(go);
    }

    void ClearEntries()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}
