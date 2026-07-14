using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ArchonsRise.SaveData;

// Mid-run enemy spawning from the seeded zones. Cadence and toughness come
// from the doom clock (DoomRules); alive spawns are saved explicitly in
// schema v4, never derived from the seed (decision 2026-07-07).
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [SerializeField] EnemyDeck deck;
    [SerializeField] Tilemap ground;
    [SerializeField] TileBase townTile;
    [SerializeField] TileBase dungeonTile;
    [SerializeField] DoomTuningSO tuning;

    private readonly List<Cell> zones = new();
    // Fresh rng per session: mid-run spawns need no determinism because the
    // alive set is what gets saved.
    private readonly System.Random rng = new System.Random();

    public int RoundsSinceSpawn { get; set; }

    private static readonly Cell[] offsets =
    {
        new Cell(-1, 1), new Cell(0, 1), new Cell(1, 0),
        new Cell(0, -1), new Cell(-1, -1), new Cell(-1, 0)
    };

    void Awake() => Instance = this;

    public void SetZones(IReadOnlyList<Cell> cells)
    {
        zones.Clear();
        for (int i = 0; i < cells.Count; i++) zones.Add(cells[i]);
    }

    // Called by GameManager.RoundPlus after the doom tick.
    public void OnRoundEnd()
    {
        if (RunEndController.HasEnded) return;
        var t = tuning.tuning;
        int doom = DoomClock.Instance != null ? DoomClock.Instance.Doom : 0;
        RoundsSinceSpawn++;
        if (!DoomRules.ShouldSpawn(doom, RoundsSinceSpawn, t)) return;
        RoundsSinceSpawn = 0;
        SpawnOne(doom, t);
    }

    private void SpawnOne(int doom, DoomTuning t)
    {
        if (zones.Count == 0 || deck.enemies.Count == 0) return;

        var zone = zones[rng.Next(zones.Count)];
        var blocked = BuildBlockedSet(zone);
        if (!SpawnRules.TryPickSpawnCell(zone, offsets, blocked, max => rng.Next(max), out var cell))
            return; // zone saturated — skip this spawn (spec: never force-place)

        var tiers = new List<int>();
        foreach (var e in deck.enemies) tiers.Add(e.tier);
        int idx = SpawnRules.PickEnemyIndex(tiers, DoomRules.MaxTier(doom, t), max => rng.Next(max));
        if (idx < 0) return;

        int bonus = DoomRules.StatBonus(doom, t);
        deck.GetNewEnemyToken(new Vector3Int(cell.x, cell.y, 0), ground, idx, bonus, bonus, true);
    }

    // Everything a spawn must avoid inside this zone's footprint: off-map,
    // non-land, town cells, existing enemies, and the player's cell.
    private HashSet<Cell> BuildBlockedSet(Cell zone)
    {
        var blocked = new HashSet<Cell>();
        var area = new List<Cell> { zone };
        foreach (var o in offsets) area.Add(new Cell(zone.x + o.x, zone.y + o.y));
        foreach (var c in area)
        {
            var pos = new Vector3Int(c.x, c.y, 0);
            if (c.x < 0 || c.x > 19 || c.y < 0 || c.y > 19
                || !ground.HasTile(pos) || ground.GetTile(pos) == townTile
                || ground.GetTile(pos) == dungeonTile)
                blocked.Add(c);
        }
        foreach (var token in FindObjectsByType<EnemyToken>(FindObjectsSortMode.None))
            blocked.Add(new Cell(token.gridPos.x, token.gridPos.y));
        foreach (var town in FindObjectsByType<TownToken>(FindObjectsSortMode.None))
            blocked.Add(new Cell(town.gridPos.x, town.gridPos.y));
        var player = FindAnyObjectByType<PlayerPosition>();
        if (player != null)
        {
            var pc = ground.WorldToCell(player.transform.position);
            blocked.Add(new Cell(pc.x, pc.y));
        }
        return blocked;
    }

    // Save path: only alive mid-run spawns; defeated ones drop out naturally.
    public SpawnedEnemy[] ExportAlive()
    {
        var list = new List<SpawnedEnemy>();
        foreach (var token in FindObjectsByType<EnemyToken>(FindObjectsSortMode.None))
            if (token.isMidRunSpawn && token.enemy != null)
                list.Add(new SpawnedEnemy
                {
                    x = token.gridPos.x,
                    y = token.gridPos.y,
                    enemyId = token.enemy.id,
                    bonusHP = token.bonusHP,
                    bonusAttack = token.bonusAttack
                });
        return list.ToArray();
    }

    // Load path: re-instantiate each saved spawn through the content registry.
    public void RestoreSpawned(SpawnedEnemy[] saved, ContentRegistry<EnemiesSO> registry)
    {
        if (saved == null) return;
        foreach (var sp in saved)
        {
            if (!registry.TryGet(sp.enemyId, out var so))
            {
                Debug.LogWarning($"RestoreSpawned: unknown enemy id '{sp.enemyId}' — skipped.");
                continue;
            }
            int idx = deck.enemies.IndexOf(so);
            if (idx < 0)
            {
                Debug.LogWarning($"RestoreSpawned: enemy '{sp.enemyId}' not in the EnemyDeck pool — skipped.");
                continue;
            }
            deck.GetNewEnemyToken(new Vector3Int(sp.x, sp.y, 0), ground, idx, sp.bonusHP, sp.bonusAttack, true);
        }
    }
}
