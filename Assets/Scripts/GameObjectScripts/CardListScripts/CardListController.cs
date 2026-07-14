using System.Collections.Generic;
using UnityEngine;

// Read-only deck viewer. Open() reads card DATA (CardsSO) from the three zones,
// orders it via CardListPlan, and instantiates inert display clones under the
// scroll content; Close() destroys them. Live zone cards never move or change
// state — the old flow reparented the real Card objects into the list, which
// dragged fan tilt/scale/dim along and coupled the list to every zone's internals.
public class CardListController : MonoBehaviour
{
    [SerializeField] PlayerHand hand;
    [SerializeField] PlayerDeck deck;
    [SerializeField] DiscardPile discard;
    [SerializeField] GameObject cardPrefab;
    [SerializeField] Transform content; // ScrollRect content (GridLayoutGroup)
    [Header("Hover")]
    [SerializeField] float hoverScale = 1.35f;
    [SerializeField] float hoverPull = 0.15f;    // fraction of the distance to viewport centre
    [SerializeField] float hoverDuration = 0.15f;

    public void Open()
    {
        // RunEndController force-disables the canvas without Close(), so a
        // rebuild must clear leftovers first to stay idempotent.
        ClearClones();

        var owned = GatherOwnedCards();
        var types = new StatType[owned.Count];
        var names = new string[owned.Count];
        for (int i = 0; i < owned.Count; i++)
        {
            types[i] = owned[i].cardType;
            names[i] = owned[i].cardName;
        }

        foreach (var i in CardListPlan.Order(types, names))
        {
            var go = Instantiate(cardPrefab, content);
            go.GetComponent<Card>().cardSO = owned[i];
            go.name = owned[i].cardName;
            // Hover lives only on list clones, never on the shared Card prefab
            // (the hand fan must not pick it up), so it's added at runtime.
            go.AddComponent<CardListHover>().Init(hoverScale, hoverPull, hoverDuration);
        }
        GameManager.Instance.cardListCanvas.enabled = true;
    }

    public void Close()
    {
        GameManager.Instance.cardListCanvas.enabled = false;
        ClearClones();
    }

    // Hand + draw pile + discard = every card the run owns. Zone order is
    // irrelevant: CardListPlan re-sorts, deliberately hiding draw order.
    List<CardsSO> GatherOwnedCards()
    {
        var owned = new List<CardsSO>();
        if (hand != null) Collect(hand.cardsInPlay, owned);
        else Debug.LogWarning("CardListController: hand not assigned.");
        if (deck != null) Collect(deck.CardsInDeck, owned);
        else Debug.LogWarning("CardListController: deck not assigned.");
        if (discard != null) Collect(discard.Cards, owned);
        else Debug.LogWarning("CardListController: discard not assigned.");
        return owned;
    }

    static void Collect(List<Card> zone, List<CardsSO> owned)
    {
        foreach (var card in zone)
            if (card != null && card.cardSO != null)
                owned.Add(card.cardSO);
    }

    void ClearClones()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}
