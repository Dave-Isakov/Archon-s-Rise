using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays an enemy card's defeat animation, then destroys the GameObject and
// invokes a completion callback. Presentation ONLY: the CombatController banks
// the kill (logical-set removal, undo commit, guardian record, reward tally)
// BEFORE calling these, so a cut-short animation can never desync combat.
[RequireComponent(typeof(CanvasGroup))]
public class EnemyCardDefeatFx : MonoBehaviour
{
    [Header("Shake + dissolve (Siege / Attack)")]
    [SerializeField] float shakeDuration = 0.15f;
    [SerializeField] float shakeAmplitude = 12f;   // px on the RectTransform
    [SerializeField] float shakeFrequency = 30f;   // Hz
    [SerializeField] float dissolveDuration = 0.4f;
    [SerializeField] Image dissolveImage;          // card art using the dissolve material

    [Header("Fade (Influence)")]
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] float fadeRise = 20f;         // px upward drift

    static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");
    CanvasGroup group;
    Material dissolveMat;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        // Instance the material so tweening _DissolveAmount never touches the shared asset.
        if (dissolveImage != null)
            dissolveMat = dissolveImage.material = new Material(dissolveImage.material);
    }

    public void PlayDestroy(System.Action onComplete) => StartCoroutine(DestroyRoutine(onComplete));
    public void PlayFade(System.Action onComplete)    => StartCoroutine(FadeRoutine(onComplete));

    IEnumerator DestroyRoutine(System.Action onComplete)
    {
        var rt = (RectTransform)transform;
        Vector2 origin = rt.anchoredPosition;
        for (float t = 0f; t < shakeDuration; t += Time.deltaTime)
        {
            float env = DefeatFxMath.ShakeEnvelope(t, shakeDuration, shakeAmplitude);
            rt.anchoredPosition = origin + Vector2.right * (env * Mathf.Sin(t * shakeFrequency * 2f * Mathf.PI));
            yield return null;
        }
        rt.anchoredPosition = origin;
        for (float t = 0f; t < dissolveDuration; t += Time.deltaTime)
        {
            if (dissolveMat != null) dissolveMat.SetFloat(DissolveId, DefeatFxMath.DissolveProgress(t, dissolveDuration));
            yield return null;
        }
        if (dissolveMat != null) dissolveMat.SetFloat(DissolveId, 1f);
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    IEnumerator FadeRoutine(System.Action onComplete)
    {
        var rt = (RectTransform)transform;
        Vector2 origin = rt.anchoredPosition;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float p = DefeatFxMath.DissolveProgress(t, fadeDuration);
            group.alpha = 1f - p;
            rt.anchoredPosition = origin + Vector2.up * (fadeRise * p);
            yield return null;
        }
        group.alpha = 0f;
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
