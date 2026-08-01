#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchonsRise.SaveData;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Dev tool: while in Play Mode on a fresh New Game (GameBoard), builds the exact
// board state that exercises forced aggro + the swarm pull-in (spec 2026-08-01)
// and writes Save.json, so the feature is one click away instead of a hunt for
// the right pair of enemies. Editor-only; never shipped.
//
// The geometry: two enemies adjacent to each other share exactly two common
// neighbours. Park the player on one of them with both enemies armed, and the
// single step to the other is a move from one adjacent hex to another adjacent
// hex — the forced trigger — while standing next to both, which is the pull-in.
public static class SwarmSaveTool
{
    const int RevealRadius   = 3;   // fog cleared around the player; must cover the whole quad
    const int SearchRadius   = 12;  // how far out from the player to hunt for a quad
    const int ExploreCards   = 3;   // enough Explore in hand to afford the trigger step
    const int CombatCards    = 6;   // Attack/Defend/Siege, so the forced fight is playable

    [MenuItem("Tools/Archon's Rise/Create Swarm Test Save")]
    public static void Create()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Swarm Test Save",
                "Enter Play Mode on a fresh New Game (GameBoard scene) first, then run this again.", "OK");
            return;
        }

        var dm     = DataManager.Instance;
        var player = UnityEngine.Object.FindAnyObjectByType<Player>();
        var pos  = UnityEngine.Object.FindAnyObjectByType<PlayerPosition>();
        var deck = UnityEngine.Object.FindAnyObjectByType<PlayerDeck>();
        var hand = UnityEngine.Object.FindAnyObjectByType<PlayerHand>();
        var grid = UnityEngine.Object.FindAnyObjectByType<Grid>();
        var dir  = UnityEngine.Object.FindAnyObjectByType<ExplorationController>();
        var enemyDeck = UnityEngine.Object.FindAnyObjectByType<EnemyDeck>();

        if (dm == null || player == null || pos == null || deck == null || hand == null || grid == null
            || dir == null || enemyDeck == null)
        {
            Debug.LogError("Swarm Test Save: missing core objects — run this in the GameBoard play session.");
            return;
        }
        int tier1 = LowestTierIndex(enemyDeck.enemies);
        if (tier1 < 0)
        {
            Debug.LogError("Swarm Test Save: the EnemyDeck pool is empty — nothing to spawn.");
            return;
        }

        var ground = dir.Map;
        var start = ground.WorldToCell(pos.transform.position);
        if (!TryFindQuad(pos, ground, start, out var e1, out var e2, out var standOn, out var stepTo))
        {
            Debug.LogError($"Swarm Test Save: no clear two-enemy quad within {SearchRadius} cells of {start}. "
                         + "Try a fresh New Game (different seed).");
            return;
        }

        // --- Player onto the standing hex, fog cleared over the whole quad ---
        pos.transform.position = grid.CellToWorld(standOn);
        if (dir.Fog != null)
            for (int dx = -RevealRadius; dx <= RevealRadius; dx++)
                for (int dy = -RevealRadius; dy <= RevealRadius; dy++)
                    dir.Fog.SetTile(new Vector3Int(standOn.x + dx, standOn.y + dy, 0), null);

        // --- The pair, as mid-run spawns so they save and restore explicitly ---
        var a = SpawnArmed(enemyDeck, ground, e1, tier1);
        var b = SpawnArmed(enemyDeck, ground, e2, tier1);

        // --- A hand that can actually take the step and then fight ---
        var pool = (dm.allCards ?? new CardsSO[0]).Where(c => c != null && c.cardType != StatType.Wound).ToList();
        var cards = Pick(pool, StatType.Explore, ExploreCards);
        cards.AddRange(Pick(pool, StatType.Attack | StatType.Defend | StatType.Siege, CombatCards));
        int handSize = Mathf.Max(1, player.PlayerHandSize);
        hand.RebuildHand(cards.Take(handSize).ToList());
        deck.RebuildDeck(cards.Skip(handSize).ToList());

        // --- Capture and write (bypasses the settled-state gate, as the late-game tool does) ---
        var file = dm.CaptureRunState();
        if (file == null) { Debug.LogError("Swarm Test Save: CaptureRunState returned null."); return; }
        dm.current = file;
        string path = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        File.WriteAllText(path, SaveSerializer.ToJson(file));

        Debug.Log($"Swarm Test Save written to {path}\n" +
                  $"enemies at {e1} ({a.enemy.cardName}) and {e2} ({b.enemy.cardName}), both armed. " +
                  $"Player on {standOn}.\n" +
                  $"Load Game, then step {standOn} -> {stepTo}: both hexes touch both enemies, so that " +
                  $"one move forces the fight AND pulls both in. The canvas should refuse to close on a click-off.");
    }

    // Spawns an enemy already armed. gridPos is assigned here rather than left to
    // the token's Start(), because the aggro capture reads it during this same
    // frame and would otherwise record a default cell.
    static EnemyToken SpawnArmed(EnemyDeck deck, Tilemap ground, Vector3Int cell, int enemyIndex)
    {
        var token = deck.GetNewEnemyToken(cell, ground, enemyIndex, 0, 0, isMidRunSpawn: true);
        token.gridPos = cell;
        token.isAggro = true;
        return token;
    }

    // The weakest enemy in the roaming pool, so the forced fight is winnable with
    // the starter hand this tool deals. -1 when the pool holds nothing usable.
    static int LowestTierIndex(List<EnemiesSO> enemies)
    {
        if (enemies == null) return -1;
        int best = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            if (best < 0 || enemies[i].tier < enemies[best].tier) best = i;
        }
        return best;
    }

    // `count` cards matching any of the wanted stat flags (StatType is a flags
    // enum, so a Siege card also carries Attack), padded by repeats if the pool
    // is thin. Never returns null entries.
    static List<CardsSO> Pick(List<CardsSO> pool, StatType wanted, int count)
    {
        var matching = pool.Where(c => (c.cardType & wanted) != 0).ToList();
        var picked = new List<CardsSO>();
        if (matching.Count == 0) return picked;
        for (int i = 0; i < count; i++) picked.Add(matching[i % matching.Count]);
        return picked;
    }

    // Hunts outward from `start` for two adjacent enemy cells whose two shared
    // neighbours are both clear standing ground, with no third enemy in reach of
    // either — so the fight the player triggers is exactly two enemies, not three.
    static bool TryFindQuad(PlayerPosition pos, Tilemap ground, Vector3Int start,
        out Vector3Int e1, out Vector3Int e2, out Vector3Int standOn, out Vector3Int stepTo)
    {
        e1 = e2 = standOn = stepTo = default;
        var blocked = OccupiedCells();

        for (int r = 1; r <= SearchRadius; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var first = new Vector3Int(start.x + dx, start.y + dy, 0);
                    if (!IsClear(ground, blocked, first)) continue;

                    foreach (var second in Neighbours(pos, first))
                    {
                        if (!IsClear(ground, blocked, second)) continue;

                        // The two cells adjacent to BOTH enemies.
                        var shared = Neighbours(pos, first).Intersect(Neighbours(pos, second)).ToList();
                        if (shared.Count != 2) continue;
                        if (!IsClear(ground, blocked, shared[0]) || !IsClear(ground, blocked, shared[1])) continue;
                        if (HasEnemyNear(pos, shared[0]) || HasEnemyNear(pos, shared[1])) continue;

                        e1 = first; e2 = second;
                        standOn = shared[0]; stepTo = shared[1];
                        return true;
                    }
                }
        return false;
    }

    // Walkable land, and nothing already standing on it.
    static bool IsClear(Tilemap ground, HashSet<Vector3Int> blocked, Vector3Int cell)
        => ground.HasTile(cell) && !blocked.Contains(cell);

    // Every cell a token of any kind occupies — enemies, towns, dungeons, shrines.
    // Standing an enemy (or the player) on a place cell would confuse the test.
    static HashSet<Vector3Int> OccupiedCells()
    {
        var set = new HashSet<Vector3Int>();
        foreach (var t in UnityEngine.Object.FindObjectsByType<EnemyToken>()) set.Add(t.gridPos);
        foreach (var t in UnityEngine.Object.FindObjectsByType<TownToken>()) set.Add(t.gridPos);
        foreach (var t in UnityEngine.Object.FindObjectsByType<DungeonToken>()) set.Add(t.gridPos);
        foreach (var t in UnityEngine.Object.FindObjectsByType<ShrineToken>()) set.Add(t.gridPos);
        return set;
    }

    // A pre-existing map enemy within one hex of this cell would join the fight
    // too, making the test's expected roster unpredictable.
    static bool HasEnemyNear(PlayerPosition pos, Vector3Int cell)
    {
        var ring = Neighbours(pos, cell);
        foreach (var t in UnityEngine.Object.FindObjectsByType<EnemyToken>())
            if (Array.IndexOf(ring, t.gridPos) >= 0) return true;
        return false;
    }

    // Parity-correct hex neighbours, via the same UpdateCompass every board rule
    // uses — offsets differ by row parity, so this can't be a fixed table.
    static Vector3Int[] Neighbours(PlayerPosition pos, Vector3Int cell)
    {
        var compass = new Dictionary<Directions, Vector3Int>
        {
            {Directions.Northwest, new Vector3Int(-1,1)},
            {Directions.Northeast, new Vector3Int(0,1)},
            {Directions.East, new Vector3Int(1,0)},
            {Directions.Southeast, new Vector3Int(0,-1)},
            {Directions.Southwest, new Vector3Int(-1,-1)},
            {Directions.West, new Vector3Int(-1,0)}
        };
        pos.UpdateCompass(cell, compass);
        var result = new Vector3Int[6];
        int i = 0;
        foreach (Directions d in Enum.GetValues(typeof(Directions))) result[i++] = cell + compass[d];
        return result;
    }
}
#endif
