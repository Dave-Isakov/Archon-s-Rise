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
    [SerializeField] Tilemap fog;
    [SerializeField] List<TileBase> tiles; //[0] = Plains, [1] = Forest, [2] = Desert, [3] = Water, [4] = Mountain
    [SerializeField] TileBase townTile;
    [SerializeField] GameObject enemyToken;
    [SerializeField] Transform enemyParentObject;
    [SerializeField] Transform townParentObject;
    [SerializeField] EnemyDeck deck;
    [SerializeField] TownDeck towns;
    // Castles carry the heaviest guardian rosters, so they must never be the first
    // place a fresh player meets. Towns closer to the start than this (Chebyshev
    // distance from (0,0)) only roll Town/Keep; Castles seed from here outward.
    [SerializeField] int castleMinDistanceFromStart = 8;
    [SerializeField] TileBase dungeonTile;
    [SerializeField] GameObject dungeonTokenPrefab;
    [SerializeField] int dungeonCount = 6;
    [SerializeField] int dungeonMinSpacing = 4;
    [SerializeField] List<DungeonsSO> dungeonPool = new();
    [SerializeField] DoomTuningSO doomTuning;
    [SerializeField] EnemySpawner spawner;
    // Spawn zones seeded this generation — deterministic over the map seed,
    // so they are never saved. EnemySpawner reads them after Start.
    public List<ArchonsRise.SaveData.Cell> ZoneCells { get; private set; } = new();
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
                        // Only border water (the map's outer ring) is always visible; interior
                        // water stays fogged until explored.
                        bool onMapEdge = x == 0 || x == 19 || y == 0 || y == 19;
                        if (fog != null && onMapEdge) fog.SetTile(tilePos, null);
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

        // Towns near the start roll only from the non-Castle pool so a fresh player
        // never opens onto a Castle's guardian wall; distant towns roll the full pool.
        var nonCastleTowns = towns.towns.FindAll(t => t.placeType != PlaceType.Castle);
        var placedTowns = new List<TownToken>();
        for(int x = 3; x < 18; x+=(Rng(5,7)))
        {
            for(int y = 3; y < 18; y+=(Rng(5,7)))
            {
                var tilePos = new Vector3Int(x,y);
                ground.SetTile(tilePos, townTile);
                var tile = ground.GetTile<TownRuleTile>(tilePos);
                var townToken = Instantiate(tile.m_DefaultGameObject, ground.CellToWorld(tilePos)+ new Vector3(0,-1), Quaternion.identity, townParentObject);
                var placed = townToken.GetComponent<TownToken>();
                // Chebyshev distance from the (0,0) start; x,y are always positive here.
                var pool = (System.Math.Max(x, y) < castleMinDistanceFromStart && nonCastleTowns.Count > 0)
                    ? nonCastleTowns
                    : towns.towns;
                placed.townSO = pool[Rng(0, pool.Count)];
                placed.gridPos = tilePos;
                placedTowns.Add(placed);
            }
        }

        // M2.5's victory is "conquer 2 Castles", so the seeded map must always
        // contain at least 2. Upgrade the last-placed non-Castle tokens if the
        // random picks came up short. Deterministic over the seed (consumes no
        // extra RNG draws) and runs before the tokens' Start, so conquest
        // registration sees the final types.
        var castleSO = towns.towns.Find(t => t.placeType == PlaceType.Castle);
        if (castleSO != null)
        {
            int castles = 0;
            foreach (var t in placedTowns)
                if (t.townSO.placeType == PlaceType.Castle) castles++;
            // Prefer distant towns (iterating from the last-placed, farthest corner)
            // so the guaranteed Castles stay out of the early game.
            for (int i = placedTowns.Count - 1; i >= 0 && castles < 2; i--)
                if (placedTowns[i].townSO.placeType != PlaceType.Castle
                    && System.Math.Max(placedTowns[i].gridPos.x, placedTowns[i].gridPos.y) >= castleMinDistanceFromStart)
                {
                    placedTowns[i].townSO = castleSO;
                    castles++;
                }
            // Fallback: an unwinnable map (< 2 Castles) is worse than a slightly
            // close one — if the distance gate left too few, upgrade the farthest
            // remaining non-Castles regardless of distance.
            for (int i = placedTowns.Count - 1; i >= 0 && castles < 2; i--)
                if (placedTowns[i].townSO.placeType != PlaceType.Castle)
                {
                    placedTowns[i].townSO = castleSO;
                    castles++;
                }
        }

        // Dungeon placement (M2.9): configurable count, spaced like spawn
        // zones, deterministic over the map seed — positions and SO assignment
        // are never saved, only progress is (PlaceConquest pattern).
        if (dungeonTile != null && dungeonTokenPrefab != null && dungeonPool.Count > 0)
        {
            var dCandidates = new List<ArchonsRise.SaveData.Cell>();
            for (int x = 0; x < 20; x++)
                for (int y = 0; y < 20; y++)
                {
                    var pos = new Vector3Int(x, y);
                    if (!ground.HasTile(pos) || ground.GetTile(pos) == townTile) continue;
                    var cell = new ArchonsRise.SaveData.Cell(x, y);
                    if (SpawnRules.Spacing(cell, new ArchonsRise.SaveData.Cell(0, 0)) < doomTuning.tuning.startSafeRadius) continue;
                    dCandidates.Add(cell);
                }
            var dungeonCells = SpawnRules.SeedZones(dCandidates, dungeonCount, dungeonMinSpacing, max => Rng(0, max));

            // Assign SOs seed-randomly: without replacement until the pool
            // exhausts, then refill (repeats only when count > pool size).
            var bag = new List<DungeonsSO>(dungeonPool);
            foreach (var cell in dungeonCells)
            {
                if (bag.Count == 0) bag.AddRange(dungeonPool);
                var so = bag[Rng(0, bag.Count)];
                bag.Remove(so);

                var tilePos = new Vector3Int(cell.x, cell.y);
                ground.SetTile(tilePos, dungeonTile);
                var token = Instantiate(dungeonTokenPrefab,
                    ground.CellToWorld(tilePos) + new Vector3(0, -1), Quaternion.identity, townParentObject);
                var placed = token.GetComponent<DungeonToken>();
                placed.dungeonSO = so;
                placed.gridPos = tilePos;
            }
        }

        // Seed spawn zones across the WHOLE map (replaces the accidental
        // lower-left-only enemy region). Candidates: land cells that aren't
        // towns and sit outside the start's safe radius. Deterministic over
        // the map seed.
        var tuning = doomTuning.tuning;
        var candidates = new List<ArchonsRise.SaveData.Cell>();
        for (int x = 0; x < 20; x++)
            for (int y = 0; y < 20; y++)
            {
                var pos = new Vector3Int(x, y);
                if (!ground.HasTile(pos) || ground.GetTile(pos) == townTile || ground.GetTile(pos) == dungeonTile) continue;
                var cell = new ArchonsRise.SaveData.Cell(x, y);
                if (SpawnRules.Spacing(cell, new ArchonsRise.SaveData.Cell(0, 0)) < tuning.startSafeRadius) continue;
                candidates.Add(cell);
            }
        ZoneCells = SpawnRules.SeedZones(candidates, tuning.spawnZoneCount, tuning.zoneMinSpacing,
            max => Rng(0, max));

        // Initial enemies: each zone contributes its starting pack. Doom is 0
        // at map gen → tier 1 only, no stat bonus.
        var offsets = new List<ArchonsRise.SaveData.Cell>
        {
            new ArchonsRise.SaveData.Cell(-1, 1), new ArchonsRise.SaveData.Cell(0, 1),
            new ArchonsRise.SaveData.Cell(1, 0),  new ArchonsRise.SaveData.Cell(0, -1),
            new ArchonsRise.SaveData.Cell(-1, -1), new ArchonsRise.SaveData.Cell(-1, 0)
        };

        // Guaranteed near-start zone: the safe-radius spread keeps zones far from
        // (0,0), which left the opening with no reachable threat. Force one zone at
        // the closest valid land cell just outside the start (never adjacent, so the
        // first pack isn't in the player's face), prepended so it always gets a pack.
        var start = new ArchonsRise.SaveData.Cell(0, 0);
        for (int d = 2; d <= 6; d++)
        {
            ArchonsRise.SaveData.Cell? pick = null;
            for (int x = 0; x <= d && pick == null; x++)
                for (int y = 0; y <= d; y++)
                {
                    if (System.Math.Max(x, y) != d) continue; // ring at Chebyshev distance d
                    if (x > 19 || y > 19) continue;
                    var pos = new Vector3Int(x, y);
                    if (!ground.HasTile(pos) || ground.GetTile(pos) == townTile || ground.GetTile(pos) == dungeonTile) continue;
                    pick = new ArchonsRise.SaveData.Cell(x, y);
                    break;
                }
            if (pick.HasValue)
            {
                if (!ZoneCells.Contains(pick.Value)) ZoneCells.Insert(0, pick.Value);
                break;
            }
        }

        var blocked = new HashSet<ArchonsRise.SaveData.Cell>
        {
            start // player start
        };
        // Never spawn on a cell adjacent to the start, so the near-start zone can't
        // drop an enemy right next to the player on turn one.
        foreach (var o in offsets)
            blocked.Add(new ArchonsRise.SaveData.Cell(start.x + o.x, start.y + o.y));
        var tiers = new List<int>();
        foreach (var e in deck.enemies) tiers.Add(e.tier);

        foreach (var zone in ZoneCells)
        {
            // Pre-block off-map / non-land / town cells in this zone's footprint
            // so TryPickSpawnCell only ever returns real, walkable cells.
            var area = new List<ArchonsRise.SaveData.Cell> { zone };
            foreach (var o in offsets)
                area.Add(new ArchonsRise.SaveData.Cell(zone.x + o.x, zone.y + o.y));
            foreach (var c in area)
            {
                var pos = new Vector3Int(c.x, c.y);
                if (c.x < 0 || c.x > 19 || c.y < 0 || c.y > 19
                    || !ground.HasTile(pos) || ground.GetTile(pos) == townTile
                    || ground.GetTile(pos) == dungeonTile)
                    blocked.Add(c);
            }

            for (int i = 0; i < tuning.initialEnemiesPerZone; i++)
            {
                if (!SpawnRules.TryPickSpawnCell(zone, offsets, blocked, max => Rng(0, max), out var cell)) break;
                int idx = SpawnRules.PickEnemyIndex(tiers, DoomRules.MaxTier(0, tuning), max => Rng(0, max));
                if (idx < 0) break;
                deck.GetNewEnemyToken(new Vector3Int(cell.x, cell.y), ground, idx);
                blocked.Add(cell);
            }
        }

        if (spawner != null) spawner.SetZones(ZoneCells);
    }
}
