using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// Reusable looping color-shift. Smoothly crossfades a UI Graphic's color through a
// set of colors, on an endless loop. Drop it on any object that has a Graphic
// (Image, Text, ...): configure the colors/speed in the inspector with Play On
// Enable, or drive it from code via Play(). Used by CardVisuals to give "All" cards
// (Crystallization) a cycling every-color look, but it isn't card-specific.
[DisallowMultipleComponent]
public class EmpowerColorShift : MonoBehaviour
{
    [Tooltip("Graphic to tint. Defaults to a Graphic on this GameObject if left empty.")]
    [SerializeField] Graphic target;
    [Tooltip("Colors to cycle through, in order. Loops back to the first.")]
    [SerializeField] List<Color> colors = new();
    [Tooltip("Seconds to crossfade from one color to the next.")]
    [SerializeField] float secondsPerColor = 1.5f;
    [Tooltip("Start cycling automatically on enable, using the inspector-set colors.")]
    [SerializeField] bool playOnEnable = true;

    Sequence _sequence;

    void Reset() => target = GetComponent<Graphic>();

    void OnEnable()
    {
        if (playOnEnable && colors.Count > 0)
            Play();
    }

    void OnDisable() => Stop();
    void OnDestroy() => Stop();

    // Cycle using the inspector-configured target, colors, and speed.
    public void Play() => Play(target, colors, secondsPerColor);

    // Cycle a specific graphic through the given colors. Restarts if already running.
    // The colors are copied, so later edits to the source list don't affect the loop.
    public void Play(Graphic graphic, IList<Color> cycle, float perColorSeconds)
    {
        Stop();
        target = graphic != null ? graphic : GetComponent<Graphic>();
        colors = cycle != null ? new List<Color>(cycle) : new List<Color>();
        secondsPerColor = perColorSeconds;
        if (target == null || colors.Count == 0) return;

        target.color = colors[0];
        // SetLink auto-kills the tween if this GameObject is destroyed mid-flight.
        _sequence = DOTween.Sequence().SetLink(gameObject).SetLoops(-1, LoopType.Restart);
        // Crossfade to each next color; the final step returns to colors[0] to close the loop.
        for (int i = 1; i <= colors.Count; i++)
        {
            Color next = colors[i % colors.Count];
            _sequence.Append(target.DOColor(next, secondsPerColor).SetEase(Ease.InOutSine));
        }
    }

    public void Stop()
    {
        _sequence?.Kill();
        _sequence = null;
    }
}
