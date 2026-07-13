using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Location : Deck<LocationsSO>
{
    [SerializeField] EnemyDeck enemyDeck;
    [SerializeField] TownDeck townDeck;
    [SerializeField] Rewards rewards;
    public List<LocationsSO> locations;

    private void Awake()
    {
        for(int i = 0; i < locations[0].enemies.Count; i++)
            enemyDeck.enemies.Add(locations[0].enemies[i]);

        for(int i = 0; i < locations[0].towns.Count; i++)
            townDeck.towns.Add(locations[0].towns[i]);
    }
    private void Start()
    {

    }

    private void Update()
    {
        
    }

    // public void OnPointerClick(PointerEventData eventData)
    // {
    //     GetExplore();
    // }

    // public void GetExplore()
    // {
    //     exploreCount++;
    //     if(enemyDeck is not null && townDeck is not null)
    //     {
    //         int rng = Random.Range(1,10);
    //         // if ((rng * exploreCount) % 4 == 0)
    //         //     townDeck.CreateTown();
    //         // else if (!((rng * exploreCount)%4==0) && ((rng * exploreCount) %2 == 0) )
    //              // enemyDeck.GetNewEnemyCard();
    //         else
    //             rewards.GetReward();
    //     }
    // }
}

