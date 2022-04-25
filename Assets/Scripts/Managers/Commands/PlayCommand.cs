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
}
