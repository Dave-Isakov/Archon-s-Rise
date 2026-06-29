using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayCommand : ICommands
{
    Card _card;
    CardEvent playCardEvent;

    public PlayCommand(CardEvent cardEvent, Card card)
    {
        playCardEvent = cardEvent;
        _card = card;
    }
    public void Execute()
    {
        playCardEvent.Raise(_card);
    }

    public void Undo()
    {
        playCardEvent.Raise(_card);
    }

    // Called when the command can no longer be undone (the stack is cleared at a commit
    // point). A play that can't be undone has no reason to stay in hand, so the card is
    // sent to the discard pile now instead of waiting for turn end. Its stat contribution
    // stays applied; only the card leaves the hand. PlayedCardDiscard is idempotent.
    public void Commit()
    {
        if (_card != null && _card.IsPlayed)
            _card.PlayedCardDiscard();
    }
}
