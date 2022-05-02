using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Location : Deck<LocationsSO>, IPointerClickHandler
{
    [SerializeField] EnemyDeck enemyDeck;
    [SerializeField] TownDeck townDeck;
    public List<LocationsSO> locations;

    private void Awake()
    {
        for(int i = 0; i < locations[0].enemies.Count; i++)
        {
            enemyDeck.enemies.Add(locations[0].enemies[i]);
        }

        for(int i = 0; i < locations[0].towns.Count; i++)
        {
            townDeck.towns.Add(locations[0].towns[i]);
        }
    }
    private void Start()
    {

    }

    private void Update()
    {
        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GetExplore();
    }

    public void GetExplore()
    {
        if(enemyDeck is not null && townDeck is not null)
        {
            int rng = Random.Range(1,3);
            if (rng == 1)
                townDeck.CreateTown();
            else if (rng == 2 || rng == 3)
                enemyDeck.GetNewEnemyCard();
        }
    }
}

