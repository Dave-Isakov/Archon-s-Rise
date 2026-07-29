using System.Collections.Generic;
using UnityEngine;

// The corner stack of transient messages (spec 2026-07-28). Replaces the
// click-to-dismiss message canvas: a toast fades on its own, so nothing stands
// between the player and the next decision.
//
// The rail's own CanvasGroup must have blocksRaycasts = false and its canvas
// must sort above everything, so a toast can float over a card-pick modal
// without ever eating a click. The canvas is authored enabled and nothing may
// disable it — it has no closed state.
public class ToastRail : MonoBehaviour
{
    [SerializeField] Transform container;   // vertical layout, newest at one end
    [SerializeField] Toast toastPrefab;
    [SerializeField] float dwellSeconds = 3.5f;
    [SerializeField] int maxVisible = 4;

    readonly List<Toast> live = new();

    void OnEnable()
    {
        GameLog.Instance.Posted += OnPosted;
    }

    void OnDisable()
    {
        // Existing, not Instance: at scene teardown Instance would create a new
        // GameObject mid-destroy, which Unity rejects with an error.
        var log = GameLog.Existing;
        if (log != null) log.Posted -= OnPosted;
    }

    void OnPosted(string text)
    {
        if (toastPrefab == null || container == null) return;

        live.RemoveAll(t => t == null);

        // A fifth toast pushes the oldest into an early fade rather than
        // letting the stack grow off-screen.
        while (live.Count >= maxVisible)
        {
            var oldest = live[0];
            live.RemoveAt(0);
            if (oldest != null) oldest.BeginFadeNow();
        }

        var toast = Instantiate(toastPrefab, container);
        live.Add(toast);
        toast.Play(text, dwellSeconds);
    }
}
