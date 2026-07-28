using TMPro;
using UnityEngine;
using DG.Tweening;

// One toast's lifecycle: fade in, dwell, fade out, destroy. The rail owns
// stacking and count; this owns only its own timing, so an early eviction is
// just BeginFadeNow.
[RequireComponent(typeof(CanvasGroup))]
public class Toast : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] float fadeTime = 0.2f;

    CanvasGroup group;
    Tween active;

    void Awake() => group = GetComponent<CanvasGroup>();

    public void Play(string text, float dwell)
    {
        if (label != null) label.text = text;
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        // Toasts never take clicks — the rail floats over modals and must not
        // eat them (spec 2026-07-28).
        group.blocksRaycasts = false;
        group.interactable = false;

        active = DOTween.Sequence()
            .Append(group.DOFade(1f, fadeTime))
            .AppendInterval(dwell)
            .Append(group.DOFade(0f, fadeTime))
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    // Called by the rail when a newer toast pushes this one past the visible cap.
    public void BeginFadeNow()
    {
        if (active != null) active.Kill();
        if (group == null) group = GetComponent<CanvasGroup>();
        active = group.DOFade(0f, fadeTime)
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    void OnDestroy()
    {
        if (active != null) active.Kill();
    }
}
