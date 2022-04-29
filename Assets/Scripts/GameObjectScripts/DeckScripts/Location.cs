using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Location : Deck<LocationsSO>
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
}

