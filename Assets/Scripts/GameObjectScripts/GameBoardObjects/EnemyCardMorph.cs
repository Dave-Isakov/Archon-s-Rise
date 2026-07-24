using System.Collections;
using UnityEngine;

// Morphs an enemy card between the board (its source token) and its arc slot
// (spec 2026-07-24). MorphIn: emerge from the token and fan to the slot.
// MorphBack: return to the token on a flee. Presentation only — the fight is
// already resolved when these run. Shares the CanvasGroup EnemyCardDefeatFx
// requires.
[RequireComponent(typeof(CanvasGroup))]
public class EnemyCardMorph : MonoBehaviour
{
    [SerializeField] float morphDuration = 0.3f;
    [SerializeField] float startScaleMul = 0.3f; // fraction of slot scale at spawn

    CanvasGroup group;
    RectTransform rt;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rt = (RectTransform)transform;
    }

    // Fly in from `fromLocal` (the token projected into the arc-origin local
    // space) to the already-set slot. Call AFTER the applier has placed the card.
    public void MorphIn(Vector2 fromLocal)
        => StartCoroutine(MorphInRoutine(fromLocal, rt.anchoredPosition, rt.localScale, rt.localRotation));

    IEnumerator MorphInRoutine(Vector2 from, Vector2 to, Vector3 slotScale, Quaternion slotRot)
    {
        group.alpha = 0f;
        for (float t = 0f; t < morphDuration; t += Time.deltaTime)
        {
            float p = t / morphDuration;
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            rt.localScale = Vector3.Lerp(slotScale * startScaleMul, slotScale, p);
            rt.localRotation = Quaternion.Lerp(Quaternion.identity, slotRot, p);
            group.alpha = p;
            yield return null;
        }
        rt.anchoredPosition = to;
        rt.localScale = slotScale;
        rt.localRotation = slotRot;
        group.alpha = 1f;
    }

    // Return to `toLocal` (the token) and fade out, then invoke done. The caller
    // destroys the card.
    public void MorphBack(Vector2 toLocal, System.Action done)
        => StartCoroutine(MorphBackRoutine(toLocal, done));

    IEnumerator MorphBackRoutine(Vector2 toLocal, System.Action done)
    {
        Vector2 from = rt.anchoredPosition;
        Vector3 fromScale = rt.localScale;
        for (float t = 0f; t < morphDuration; t += Time.deltaTime)
        {
            float p = t / morphDuration;
            rt.anchoredPosition = Vector2.Lerp(from, toLocal, p);
            rt.localScale = Vector3.Lerp(fromScale, fromScale * startScaleMul, p);
            group.alpha = 1f - p;
            yield return null;
        }
        group.alpha = 0f;
        done?.Invoke();
    }
}
