using UnityEngine;
using UnityEngine.UI;

// Four +1 stat options. Selecting one puts the selection into Improvise mode.
// Disabled for non-empowerable cards (Wounds / EmpowerType.None).
public class ImprovisePanel : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;
    [SerializeField] Button attackButton;
    [SerializeField] Button defendButton;
    [SerializeField] Button influenceButton;
    [SerializeField] Button exploreButton;

    // Lifetime subscription, not per-enable: Render hides this panel by deactivating
    // its own GameObject (root == self) for non-empowerable cards. OnEnable/OnDisable
    // would let that SetActive(false) unsubscribe us, and nothing could re-show the
    // panel. Awake/OnDestroy survive self-deactivation. See ChoiceBanner for detail.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        attackButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Attack));
        defendButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Defend));
        influenceButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Influence));
        exploreButton.onClick.AddListener(() => inspector.ImproviseStat(StatType.Explore));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool canImprovise = sel != null && card != null
                            && card.cardSO.empowerType != EmpowerType.None;
        root.SetActive(canImprovise);
        if (!canImprovise) return;

        Mark(attackButton, StatType.Attack, sel);
        Mark(defendButton, StatType.Defend, sel);
        Mark(influenceButton, StatType.Influence, sel);
        Mark(exploreButton, StatType.Explore, sel);
    }

    static void Mark(Button b, StatType stat, CardPlaySelection sel)
    {
        b.interactable = !(sel.Mode == PlayMode.Improvise && sel.ImproviseStat == stat);
    }
}
