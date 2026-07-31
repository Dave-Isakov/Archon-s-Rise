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
    public bool IsGuarding(Vector3Int cell) => State(cell) == ShrineVisualState.Guarding;

    public void SetState(Vector3Int cell, ShrineVisualState s)
    {
        ledger.SetState(ToCell(cell), s);
        RefreshTokenVisuals();
    }

    // Bad-roll bookkeeping (2026-07-31): the guardian is shrine STATE, not a map
    // token — the shrine starts guarding and remembers the reward its guardian
    // owes at 2x. The fight itself is opened by ShrineToken, which owns the SO.
    public void SetGuarding(Vector3Int cell, ShrineReward owedType)
    {
        ledger.SetGuarding(ToCell(cell), (int)owedType);
        RefreshTokenVisuals();
    }

    // The (int)ShrineReward owed at this cell, or ShrineLedger.NoReward.
    public int OwedReward(Vector3Int cell) => ledger.OwedReward(ToCell(cell));

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

    // NOTE (2026-07-31): SpawnGuardian / FindFreeCell / ShrineSoAt were deleted
    // here. The guardian used to be dropped on the map as an EnemyToken, which
    // was wrong twice over: the fight didn't open when the bargain soured, and
    // the placement used a hard-coded even-row neighbour set with no parity
    // correction (PlayerPosition.UpdateCompass), so on an odd-row shrine the
    // "neighbour" was genuinely two hexes away. Nothing is placed any more —
    // the guardian is state on the shrine, fought from the shrine.

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
