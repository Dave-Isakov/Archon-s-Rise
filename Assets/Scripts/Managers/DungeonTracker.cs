using UnityEngine;
using ArchonsRise.SaveData;

// Runtime dungeon registry for the current run: wraps the pure DungeonLedger
// (ConquestTracker pattern). Owns doom-band flag firing and save export /
// restore. Lazily creates its scene object; scene-scoped, so a new run
// starts blank.
public class DungeonTracker : MonoBehaviour
{
    private readonly DungeonLedger ledger = new DungeonLedger();
    // Flag targets need no determinism: the resulting flags are saved explicitly.
    private readonly System.Random rng = new System.Random();

    private static DungeonTracker instance;
    public static DungeonTracker Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("DungeonTracker").AddComponent<DungeonTracker>();
            return instance;
        }
    }

    public void Register(Vector3Int cell, string dungeonId) => ledger.Register(ToCell(cell), dungeonId);
    public int DefeatedCount(Vector3Int cell) => ledger.DefeatedCount(ToCell(cell));
    public void RecordDefeat(Vector3Int cell) => ledger.RecordDefeat(ToCell(cell));
    public bool IsComplete(Vector3Int cell) => ledger.IsComplete(ToCell(cell));
    public bool IsFlagged(Vector3Int cell) => ledger.IsFlagged(ToCell(cell));
    public int FlaggedCount => ledger.FlaggedCount();
    public bool MidFired => ledger.MidFlagsFired;
    public bool HighFired => ledger.HighFlagsFired;

    // Called by DoomClock when the doom value first crosses into a band.
    public void OnMidBandEntered()
    {
        if (ledger.MidFlagsFired) return;
        ledger.MidFlagsFired = true;
        FireFlags(DoomClock.Instance.Tuning.flagsOnMidBand);
    }

    public void OnHighBandEntered()
    {
        if (ledger.HighFlagsFired) return;
        ledger.HighFlagsFired = true;
        FireFlags(DoomClock.Instance.Tuning.flagsOnHighBand);
    }

    private void FireFlags(int count)
    {
        var targets = DungeonRules.PickFlagTargets(ledger.FlagCandidates(), count, max => rng.Next(max));
        if (targets.Count > 0)
            GameManager.Instance.ValidationMessage(
                "Corruption seeps into a dungeon! It pushes the Doom Clock higher every round until cleared.");
        foreach (var cell in targets)
            ledger.SetFlagged(cell);
        RefreshTokenVisuals();
    }

    private void RefreshTokenVisuals()
    {
        foreach (var t in FindObjectsByType<DungeonToken>(FindObjectsSortMode.None))
            t.RefreshVisual();
    }

    public DungeonState[] Export() => ledger.Export();

    public void ApplySave(DungeonState[] dungeons, bool midFired, bool highFired)
    {
        ledger.MidFlagsFired = midFired;
        ledger.HighFlagsFired = highFired;
        if (dungeons == null) return;
        foreach (var d in dungeons)
            if (!ledger.ApplySavedState(d))
                Debug.LogWarning($"Dungeon restore: cell ({d.x},{d.y}) id '{d.dungeonId}' doesn't match the regenerated map — skipped.");
        RefreshTokenVisuals();
    }

    private static Cell ToCell(Vector3Int v) => new Cell(v.x, v.y);
}
