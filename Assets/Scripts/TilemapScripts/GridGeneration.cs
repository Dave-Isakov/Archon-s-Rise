using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridGeneration : MonoBehaviour
{
    [SerializeField] Tilemap ground;
    [SerializeField] Tilemap water;
    [SerializeField] Tilemap mountains;
    [SerializeField] List<TileBase> tiles; //[0] = Plains, [1] = Forest, [2] = Desert, [3] = Water, [4] = Mountain
    [SerializeField] TileBase townTile;
    [SerializeField] GameObject enemyToken;
    [SerializeField] Transform enemyParentObject;
    [SerializeField] Transform townParentObject;
    [SerializeField] EnemyDeck deck;
    [SerializeField] TownDeck towns;
    private Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };

    void Start()
    {
        var rngSource = new System.Random(DataManager.Instance.CurrentSeed);
        int Rng(int minInclusive, int maxExclusive) => rngSource.Next(minInclusive, maxExclusive);
        var player = FindAnyObjectByType<PlayerPosition>();
        for(int x = 0; x < 20; x++)
        {
            for(int y = 0; y < 20; y++)
            {
                var tilePos = new Vector3Int(x,y);
                if(x==0 && y== 0)
                {
                    ground.SetTile(tilePos, tiles[0]);
                }
                else
                {
                    var rng = Rng(0,100);
                    if(rng.IsBetween(0,45))
                    {
                        ground.SetTile(tilePos, tiles[0]);
                    }
                    else if(rng.IsBetween(46,75))
                    {
                        ground.SetTile(tilePos, tiles[1]);
                    }
                    else if(rng.IsBetween(76, 89))
                    {
                        ground.SetTile(tilePos, tiles[2]);
                    }
                    else if(rng.IsBetween(90, 94))
                    {
                        water.SetTile(tilePos, tiles[3]);
                    }
                    else if(rng.IsBetween(95,99))
                    {
                        mountains.SetTile(tilePos, tiles[4]);
                    }

                    if(ground.HasTile(tilePos))
                    {
                        var tile = ground.GetTile<HexRuleTile>(tilePos);
                        if(tile.terrain == TerrainType.Desert)
                        {
                            player.UpdateCompass(tilePos, compass);
                            foreach(Directions direction in Enum.GetValues(typeof(Directions)))
                            {
                                rng = Rng(0,10);
                                var adjTilePos = (tilePos + compass[direction]);
                                if(rng==5)
                                {
                                    ground.SetTile(tilePos, tiles[2]);
                                }
                            }
                        }
                    }


                    // if (tile.terrain == TerrainType.Water || tile.terrain == TerrainType.Mountain)

                    // foreach(Directions direction in Enum.GetValues(typeof(Directions)))
                    // {
                    //     var rng = UnityEngine.Random.Range(0,100);
                    //     var adjTilePos = (tilePos + compass[direction]);
                    //     if(rng > 91 && rng <= 95)
                    //     {
                    //
                    //     }
                    //     else if (rng > 95 && rng <= 99 )
                    //     {
                    //
                    //     }
                    // }
                }
            }
        }

        for(int x = 3; x < 18; x+=(Rng(5,7)))
        {
            for(int y = 3; y < 18; y+=(Rng(5,7)))
            {
                var tilePos = new Vector3Int(x,y);
                ground.SetTile(tilePos, townTile);
                var tile = ground.GetTile<TownRuleTile>(tilePos);
                var townToken = Instantiate(tile.m_DefaultGameObject, ground.CellToWorld(tilePos)+ new Vector3(0,-1), Quaternion.identity, townParentObject);
                townToken.GetComponent<TownToken>().townSO = towns.towns[Rng(0,3)];
            }
        }

        for(int x = 1; x < 10; x+=(Rng(2,5)))
        {
            for (int y = 2; y < 10; y+=(Rng(2,4)))
            {
                var tilePos = new Vector3Int(x,y);
                if(ground.GetTile(tilePos) != townTile)
                {
                    deck.GetNewEnemyToken(tilePos, ground, Rng(0, deck.enemies.Count));
                }
            }
        }
    }
}
