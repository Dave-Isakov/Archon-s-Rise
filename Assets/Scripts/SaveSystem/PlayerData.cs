using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public int playerAttack;
    public int playerDefend;
    public int playerInfluence;
    public int playerExplore;
    public int playerExp;
    public int playerHandSize;
    public int playerLevel;
    public float[] position = new float[3];
    // public Card[] cardsInDeck = new Card[0];
    // public Card[] cardsInHand = new Card[0];
    // public Card[] cardsInDiscard = new Card[0];

    // public PlayerData()
    // {
    //     this.playerAttack = 0;
    //     this.playerDefend = 0;
    //     this.playerInfluence = 0;
    //     this.playerExplore = 0;
    //     this.playerExp = 0;
        // this.position = new float[0];
        // this.cardsInDeck = null;
        // this.cardsInHand = null;
        // this.cardsInDiscard = null;
    // }
    // public PlayerData(Player player, PlayerPosition playerPosition, PlayerDeck playerDeck, PlayerHand playerHand)
    // {
    //     this.playerAttack = player.PlayerAttack;
    //     this.playerDefend = player.PlayerDefend;
    //     this.playerInfluence = player.PlayerInfluence;
    //     this.playerExplore = player.PlayerExplore;
    //     this.playerExp = player.PlayerExp;
    //     this.position = new float[] {playerPosition.transform.position.x, playerPosition.transform.position.y, playerPosition.transform.position.z};
    //     this.cardsInDeck = playerDeck.CardsInDeck.ToArray();
    //     this.cardsInHand = playerHand.cardsInPlay.ToArray();
    // }

    public PlayerData(Player player, PlayerPosition playerPosition)
    {
        this.playerAttack = player.PlayerAttack;
        this.playerDefend = player.PlayerDefend;
        this.playerInfluence = player.PlayerInfluence;
        this.playerExplore = player.PlayerExplore;
        this.playerExp = player.PlayerExp;
        this.playerHandSize = player.PlayerHandSize;
        this.playerLevel = player.PlayerLevel;
        
        this.position[0] = playerPosition.position.x;
        this.position[1] = playerPosition.position.y;
        this.position[2] = playerPosition.position.z;
    }
}
