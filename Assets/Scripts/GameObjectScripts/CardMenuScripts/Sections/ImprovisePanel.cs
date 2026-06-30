using UnityEngine;

// Four +1 stat options. Selecting one puts the selection into Improvise mode. Hidden
// for non-empowerable cards (Wounds / EmpowerType.None). While Empower is active the
// panel stays visible but locks (dim + reason) and destroys no stored improvise stat.
public class ImprovisePanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] StatSegment[] segments;   // Attack / Defend / Influence / Explore
    [SerializeField] GameObject lockedReason;  // "Locked while empowered"

    // Lifetime subscription, not per-enable: Render hides this panel by deactivating
    // its own GameObject (root == self) for non-empowerable cards. See ChoiceBanner for detail.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        foreach (var seg in segments)
        {
            var captured = seg;
            captured.Button.onClick.AddListener(() => inspector.ImproviseStat(captured.Stat));
        }
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool canImprovise = sel != null && card != null
                            && card.cardSO.empowerType != EmpowerType.None;
        root.SetActive(canImprovise);
        if (!canImprovise) return;

        bool locked = sel.EffectiveEmpowered();
        if (lockedReason != null) lockedReason.SetActive(locked);

        foreach (var seg in segments)
        {
            // Improvise offers a flat +1 to any stat, so every segment is always
            // selectable; make sure it's shown (parallels ChoiceBanner's activation).
            seg.gameObject.SetActive(true);

            if (locked)
                seg.SetState(StatSegment.State.Locked);
            else if (sel.Mode == PlayMode.Improvise && sel.ImproviseStat == seg.Stat)
                seg.SetState(StatSegment.State.Selected);
            else
                seg.SetState(StatSegment.State.Available);
        }
    }
}
