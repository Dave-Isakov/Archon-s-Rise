using TMPro;
using UnityEngine;
using DG.Tweening;

// Spawns a colour-coded floating "+N" that flies from a played card to its stat
// number and fades. Play-only feedback; undo relies on StatsDisplay's count-down.
public class StatEchoes : MonoBehaviour
{
    [SerializeField] StatsDisplay stats;      // for AnchorFor(stat)
    [SerializeField] GameObject labelPrefab;  // StatEcho.prefab: TMP + CanvasGroup
    [SerializeField] Transform container;     // parent for spawned labels (top overlay canvas)
    [SerializeField] float flightTime = 0.45f;

    public void Emit(Vector3 originWorld, StatType stat, int amount)
    {
        if (amount == 0 || labelPrefab == null || stats == null) return;

        var go = Instantiate(labelPrefab, container != null ? container : transform);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        var cg = go.GetComponent<CanvasGroup>();

        if (tmp != null)
        {
            tmp.text = (amount > 0 ? "+" : "") + amount;
            tmp.color = StatPalette.For(stat);
        }

        var t = go.transform;
        t.position = originWorld;

        Transform anchor = stats.AnchorFor(stat);
        Vector3 dest = anchor != null ? anchor.position : originWorld;

        t.DOMove(dest, flightTime).SetEase(Ease.InOutQuad);
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.DOFade(0f, flightTime).SetEase(Ease.InQuad);
        }
        Destroy(go, flightTime + 0.05f);
    }
}
