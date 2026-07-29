using System.Collections.Generic;
using UnityEngine;

// The corner stack of transient messages (spec 2026-07-28). Replaces the
// click-to-dismiss message canvas: a toast fades on its own, so nothing stands
// between the player and the next decision.
//
// The rail's own CanvasGroup must have blocksRaycasts = false and its canvas
// must sort above everything, so a toast can float over a card-pick modal
// without ever eating a click.
public class ToastRail : MonoBehaviour
{
    [SerializeField] Transform container;   // vertical layout, newest at one end
    [SerializeField] Toast toastPrefab;
    [SerializeField] float dwellSeconds = 3.5f;
    [SerializeField] int maxVisible = 4;

    readonly List<Toast> live = new();

    void OnEnable()
    {
        // The rail has no closed state — it is an always-on overlay, unlike the
        // modal canvases GameManager.Awake force-disables. Authoring it disabled
        // (the convention everywhere else) silently swallows every toast, so the
        // invariant is asserted here rather than left to the inspector.
        var canvas = GetComponentInParent<Canvas>();
        bool wasEnabled = canvas != null && canvas.enabled;
        if (canvas != null) canvas.enabled = true;

        GameLog.Instance.Posted += OnPosted;
        if (canvas != null)
        {
            var names = new List<string>();
            foreach (var c in canvas.GetComponents<Component>())
                if (c != null) names.Add(c.GetType().Name);
            Debug.LogWarning($"[ToastDiag] Components on '{canvas.name}': {string.Join(", ", names)}");
        }
        Debug.LogWarning($"[ToastDiag] OnEnable on '{name}' (id={GetEntityId()}) " +
                  $"canvasFound={(canvas == null ? "NULL" : canvas.name + " id=" + canvas.GetEntityId())} " +
                  $"enabledBefore={wasEnabled} enabledAfter={(canvas != null && canvas.enabled)} " +
                  $"prefab={(toastPrefab == null ? "NULL" : toastPrefab.name)} " +
                  $"container={(container == null ? "NULL" : container.name)}");
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
        Debug.Log($"[ToastDiag] OnPosted fired: \"{text}\"");
        if (toastPrefab == null || container == null)
        {
            Debug.LogError($"[ToastDiag] BAILED — toastPrefab={(toastPrefab == null ? "NULL" : "ok")} " +
                           $"container={(container == null ? "NULL" : "ok")}");
            return;
        }

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

        var rt = toast.transform as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        Debug.Log($"[ToastDiag] spawned '{toast.name}' " +
                  $"activeInHierarchy={toast.gameObject.activeInHierarchy} " +
                  $"prefabRootActive={toastPrefab.gameObject.activeSelf} " +
                  $"alpha={toast.GetComponent<CanvasGroup>().alpha} " +
                  $"scale={(rt != null ? rt.lossyScale.ToString() : "n/a")} " +
                  $"size={(rt != null ? rt.rect.size.ToString() : "n/a")} " +
                  $"pos={(rt != null ? rt.position.ToString() : "n/a")} " +
                  $"containerChildren={container.childCount} " +
                  $"canvas={(canvas == null ? "NONE" : canvas.name + " id=" + canvas.GetEntityId())} " +
                  $"canvasEnabled={(canvas != null && canvas.enabled)} " +
                  $"sortOrder={(canvas != null ? canvas.sortingOrder : -1)}");
    }
}
