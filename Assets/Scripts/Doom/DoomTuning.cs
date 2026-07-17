// The doom/spawn balance knobs. A plain serializable class (not a ScriptableObject)
// so DoomRules/SpawnRules stay Unity-free and mcs-testable; DoomTuningSO wraps one
// instance for the inspector. All values are balance.md starting values.
[System.Serializable]
public class DoomTuning
{
    public int doomMax = 20;

    // Doom bands: 0..lowBandMax, lowBandMax+1..midBandMax, midBandMax+1..doomMax.
    public int lowBandMax = 6;
    public int midBandMax = 13;

    // Rounds between mid-run spawns, per band.
    public int lowSpawnInterval = 3;
    public int midSpawnInterval = 2;
    public int highSpawnInterval = 1;

    // Flat +HP/+Attack applied to enemies spawned in the mid/high band.
    public int midStatBonus = 1;
    public int highStatBonus = 2;

    // Spawn-zone seeding (GridGeneration).
    public int spawnZoneCount = 5;
    public int zoneMinSpacing = 4;
    public int startSafeRadius = 3;
    public int initialEnemiesPerZone = 2;

    // M2.12 starter-enemy guarantee: after map gen at least one enemy must sit
    // within this Chebyshev radius of the start (doom 0 → all tier 1). The
    // rail's fight step must always have a subject.
    public bool guaranteeStarterEnemy = true;
    public int starterEnemyRadius = 5;

    // Dungeon flagging + doom relief (M2.9, spec 2026-07-13). Flags fire once
    // per run when doom first enters the mid/high band; completion relief is
    // applied as a negative DoomClock.Add.
    public int flagsOnMidBand = 1;
    public int flagsOnHighBand = 1;
    public int dungeonCompleteRelief = 1;
    public int flaggedCompleteRelief = 3;
}
