using UnityEngine;

// Subtle idle motion on an enemy card during combat (spec 2026-07-24). Drives a
// CHILD content pivot, never the card root — the CombatController layout applier
// owns the root's anchoredPosition, so the two never fight. Presentation only.
public class EnemyCardIdleSway : MonoBehaviour
{
    [SerializeField] RectTransform pivot;        // the content child, not the card root
    [SerializeField] float posAmplitude = 4f;    // px
    [SerializeField] float tiltAmplitude = 1.5f; // degrees
    [SerializeField] float period = 3.2f;        // seconds per cycle

    float phase;
    bool stopped;

    void OnEnable()
    {
        // Random phase so a row of cards never sways in lockstep.
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (stopped || pivot == null) return;
        float w = (Time.time / period) * Mathf.PI * 2f + phase;
        pivot.anchoredPosition = new Vector2(Mathf.Sin(w) * posAmplitude,
                                             Mathf.Cos(w * 0.5f) * posAmplitude * 0.5f);
        pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(w) * tiltAmplitude);
    }

    // Halt and re-centre so a defeat/return FX starts from a neutral pose.
    public void Stop()
    {
        stopped = true;
        if (pivot != null)
        {
            pivot.anchoredPosition = Vector2.zero;
            pivot.localRotation = Quaternion.identity;
        }
    }
}
