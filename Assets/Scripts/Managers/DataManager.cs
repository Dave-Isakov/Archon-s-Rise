using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    private static DataManager instance;
    public static DataManager Instance { get { return instance; } }

    List<GameObject> playerCardObjects = new();

    public int playerAttack;
    public int playerDefend;
    public int playerInfluence;
    public int playerExplore;
    public PlayerSO player;
    public int playerHandSize;
    public int improvAttackValue;
    public int improvDefendValue;
    public int improvInfluenceValue;
    public int improvExploreValue;
    public int playerHP;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
        playerHandSize = player.PlayerHandSize;
    }

    // public void CardsOnGameBoardList(GameObject playerCard)
    // {
    //     if(playerCard.activeSelf)
    //     {
    //         playerCardObjects.Add(playerCard);
    //     }
    //     else
    //     {
    //         playerCardObjects.Remove(playerCard);
    //     }
    //     foreach(var card in playerCardObjects)
    //         Debug.Log(card.GetComponent<Card>().cardSO.cardName);
    // }

    public void AssignPlayerStats(int[] stats)
    {
        playerAttack += stats[0];
        playerDefend += stats[1];
        playerInfluence += stats[2];
        playerExplore += stats[3];
    }

    public void UnAssignPlayerStats(int[] stats)
    {
        playerAttack -= stats[0];
        playerDefend -= stats[1];
        playerInfluence -= stats[2];
        playerExplore -= stats[3];
    }
}
