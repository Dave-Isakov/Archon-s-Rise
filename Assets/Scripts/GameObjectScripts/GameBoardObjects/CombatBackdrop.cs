using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fades the combat canvas background so the board (and the world-space player
// avatar) show through once a fight is underway (spec 2026-07-24). Background
// IMAGE only — HUD, hand, buttons, and enemy cards stay fully opaque.
// Presentation only; never gates combat.
public class CombatBackdrop : MonoBehaviour
{
    [SerializeField] Image background;                       // full-screen combat backdrop
    [SerializeField, Range(0f, 1f)] float combatAlpha = 0.35f;
    [SerializeField] float fadeDuration = 0.35f;

    Coroutine fade;

    // Drop the backdrop to the battle tint (called after the intro beat).
    public void FadeToBattle() => StartFade(combatAlpha);

    // Return to fully opaque (called at combat close, so the next fight fades in).
    public void Restore() => StartFade(1f);

    void StartFade(float target)
    {
        if (background == null) return;
        if (fade != null) { StopCoroutine(fade); fade = null; }
        // If the canvas is already disabled we can't run a coroutine — snap so the
        // alpha is still correct for the next fight.
        if (!gameObject.activeInHierarchy) { SetAlpha(target); return; }
        fade = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = background.color.a;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            SetAlpha(Mathf.Lerp(start, target, t / fadeDuration));
            yield return null;
        }
        SetAlpha(target);
        fade = null;
    }

    void SetAlpha(float a) { var c = background.color; c.a = a; background.color = c; }
}
