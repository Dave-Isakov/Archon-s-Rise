using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDrawCommand : ICommands
{
    PlayerDeck _deck;
    PlayerDeckEvent drawNewCardEvent;

    public CardDrawCommand(PlayerDeckEvent draw, PlayerDeck deck)
    {
        drawNewCardEvent = draw;
        _deck = deck;
    }
    public void Execute()
    {
        drawNewCardEvent.Raise(_deck);
    }

    public void Undo()
    {
        
    }
}
