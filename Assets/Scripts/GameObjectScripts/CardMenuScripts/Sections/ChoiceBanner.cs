using UnityEngine;

// Shows one segment per set action-flag on a choice card. Selecting a segment sets
// the choice stat. Hidden for non-choice cards. While Improvise is active the banner
// stays visible but locks (dim + reason) and destroys no stored choice.
public class ChoiceBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;          // the banner container to show/hide
    [SerializeField] StatSegment[] segments;   // Attack / Defend / Influence / Explore
    [SerializeField] GameObject lockedReason;  // "Locked while improvising"

    // Lifetime subscription (not per-enable): Render hides this banner by deactivating
    // its own GameObject (root == self) for non-choice cards. OnEnable/OnDisable would
    // let that SetActive(false) unsubscribe us with no way back. Awake/OnDestroy survive
    // self-deactivation.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        foreach (var seg in segments)
        {
            var captured = seg; // avoid closure-over-loop-var capturing the last element
            captured.Button.onClick.AddListener(() => inspector.ChooseStat(captured.Stat));
        }
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && card.cardSO.isChoice;
        root.SetActive(show);
        if (!show) return;

        bool locked = sel.Mode == PlayMode.Improvise;
        if (lockedReason != null) lockedReason.SetActive(locked);

        foreach (var seg in segments)
        {
            bool available = card.cardSO.cardType.HasFlag(seg.Stat);
            seg.gameObject.SetActive(available);
            if (!available) continue;

            if (locked)
                seg.SetState(StatSegment.State.Locked);
            else if (sel.Mode == PlayMode.Choice && sel.ChoiceStat == seg.Stat)
                seg.SetState(StatSegment.State.Selected);
            else
                seg.SetState(StatSegment.State.Available);
        }
    }
}
