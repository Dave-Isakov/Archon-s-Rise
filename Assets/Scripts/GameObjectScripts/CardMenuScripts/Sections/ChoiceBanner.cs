using UnityEngine;
using UnityEngine.UI;

// Shows one button per set action-flag on a choice card. Selecting a segment sets
// the choice stat. Hidden for non-choice cards and when Improvise is active.
public class ChoiceBanner : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] GameObject root;          // the banner container to show/hide
    [SerializeField] Button attackButton;
    [SerializeField] Button defendButton;
    [SerializeField] Button influenceButton;
    [SerializeField] Button exploreButton;

    // Subscribe for the component's whole lifetime, not per-enable: Render hides this
    // banner by deactivating its own GameObject (root == self) for non-choice cards.
    // With OnEnable/OnDisable that SetActive(false) would unsubscribe us, and nothing
    // could ever re-show the banner (the only caller of Render is the event we'd have
    // dropped). Awake/OnDestroy keep the subscription alive while self-deactivated, so
    // a later choice card still drives Render and reactivates us.
    void Awake()     { inspector.Changed += Render; }
    void OnDestroy() { inspector.Changed -= Render; }

    void Start()
    {
        attackButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Attack));
        defendButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Defend));
        influenceButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Influence));
        exploreButton.onClick.AddListener(() => inspector.ChooseStat(StatType.Explore));
    }

    void Render()
    {
        var sel = inspector.Selection;
        var card = inspector.Card;
        bool show = sel != null && card != null && card.cardSO.isChoice
                    && sel.Mode != PlayMode.Improvise;
        root.SetActive(show);
        if (!show) return;

        Bind(attackButton, StatType.Attack, card.cardSO.cardType, sel);
        Bind(defendButton, StatType.Defend, card.cardSO.cardType, sel);
        Bind(influenceButton, StatType.Influence, card.cardSO.cardType, sel);
        Bind(exploreButton, StatType.Explore, card.cardSO.cardType, sel);
    }

    static void Bind(Button b, StatType stat, StatType cardType, CardPlaySelection sel)
    {
        bool available = cardType.HasFlag(stat);
        b.gameObject.SetActive(available);
        if (!available) return;
        // selected highlight: interactable=false marks the chosen one
        b.interactable = !(sel.Mode == PlayMode.Choice && sel.ChoiceStat == stat);
    }
}
