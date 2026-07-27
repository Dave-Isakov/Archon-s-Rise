using UnityEngine;
using ArchonsRise.Shrines;
using ArchonsRise.SaveData;

// Runtime shrine registry for the current run: wraps the pure ShrineLedger
// (DungeonTracker/HotspotTracker pattern). Scene-scoped; a new run starts blank.
public class ShrineTracker : MonoBehaviour
{
    private readonly ShrineLedger ledger = new ShrineLedger();

    private static ShrineTracker instance;
    public static ShrineTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ShrineTracker").AddComponent<ShrineTracker>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Register(Vector3Int cell, string id) => ledger.Register(ToCell(cell), id);
    public ShrineVisualState State(Vector3Int cell) => ledger.State(ToCell(cell));

    public void SetState(Vector3Int cell, ShrineVisualState s)
    {
        ledger.SetState(ToCell(cell), s);
        RefreshTokenVisuals();
    }

    public ShrineState[] Export() => ledger.Export();

    public void ApplySave(ShrineState[] shrines)
    {
        if (shrines == null) return;
        foreach (var s in shrines)
            if (!ledger.ApplySavedState(s))
                Debug.LogWarning($"Shrine restore: cell ({s.x},{s.y}) id '{s.shrineId}' doesn't match the regenerated map — skipped.");
        RefreshTokenVisuals();
    }

    public void RefreshTokenVisuals()
    {
        foreach (var t in FindObjectsByType<ShrineToken>(FindObjectsSortMode.None))
            t.RefreshVisual();
    }

    // The ShrineSO authored at a cell. Used on save restore, where a guardian's
    // owed reward names its shrine's cell but not the asset (SO assignment
    // re-derives from the map seed, so the token is the source of truth).
    public ShrineSO ShrineSoAt(Vector3Int cell)
    {
        foreach (var t in FindObjectsByType<ShrineToken>(FindObjectsSortMode.None))
            if (t.gridPos == cell) return t.shrineSO;
        return null;
    }

    // Bad-roll spawn (spec 2026-07-24, §3): drop the shrine's guardian on the
    // shrine cell (or the nearest free neighbour — the player is standing on the
    // shrine when they engage), tag it with the owed reward, and mark the shrine
    // Guarding. Reuses the mid-run EnemyToken path so save/restore is free.
    public void SpawnGuardian(Vector3Int shrineCell, ShrineSO shrine, ShrineReward owedType)
    {
        var deck = FindAnyObjectByType<EnemyDeck>();
        if (deck == null || shrine == null || shrine.summonedEnemy == null)
        {
            Debug.LogWarning("Shrine guardian: no EnemyDeck or no summonedEnemy authored — cannot spawn.");
            return;
        }

        int idx = deck.enemies.IndexOf(shrine.summonedEnemy);
        if (idx < 0)
        {
            Debug.LogWarning($"Shrine guardian '{shrine.summonedEnemy.name}' is not in the EnemyDeck pool — cannot spawn.");
            return;
        }

        var spawner = FindAnyObjectByType<EnemySpawner>();
        var ground = spawner != null ? spawner.Ground : null;
        if (ground == null)
        {
            Debug.LogWarning("Shrine guardian: no ground tilemap — cannot spawn.");
            return;
        }

        // Fixed tier-3 via the authored asset, so no doom stat bonus here (spec §3).
        var token = deck.GetNewEnemyToken(FindFreeCell(shrineCell, ground), ground, idx, 0, 0, true);
        if (token != null)
        {
            token.shrineRewardType = (int)owedType;
            token.shrineCell = shrineCell;
            token.shrineSO = shrine;
        }
        SetState(shrineCell, ShrineVisualState.Guarding);
    }

    // Matches EnemySpawner's neighbour set so a guardian never lands somewhere an
    // ordinary spawn couldn't.
    private static readonly Vector3Int[] neighbours =
    {
        new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(0, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, 0)
    };

    // The shrine cell itself if the player has stepped off it, else the first free
    // neighbour: on the map, unoccupied by a place or another enemy, and not the
    // player's cell.
    private Vector3Int FindFreeCell(Vector3Int shrineCell, UnityEngine.Tilemaps.Tilemap ground)
    {
        var player = FindAnyObjectByType<PlayerPosition>();
        var playerCell = player != null ? ground.WorldToCell(player.transform.position) : shrineCell;

        var taken = new System.Collections.Generic.HashSet<Vector3Int>();
        foreach (var e in FindObjectsByType<EnemyToken>(FindObjectsSortMode.None))
            taken.Add(e.gridPos);

        foreach (var o in neighbours)
        {
            var c = shrineCell + o;
            if (c == playerCell || taken.Contains(c)) continue;
            if (!ground.HasTile(c)) continue;
            if (HexOccupantRegistry.Existing != null && HexOccupantRegistry.Existing.Blocks(c)) continue;
            return c;
        }
        // Fully hemmed in: fall back to the shrine cell itself. The player is
        // standing there, so the guardian is immediately adjacent-and-armed —
        // acceptable for a bargain the player just chose to take.
        return shrineCell;
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
