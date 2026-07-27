using System;

namespace ArchonsRise.SaveData
{
    [Serializable]
    public class SaveFile
    {
        // v7: adds RunState.characterId (which character this run is) and
        // PlayerState.toughness (renamed from hp — a divisor, not a pool).
        // v8: adds RunState.hotspots (crystal-hotspot charge state).
        // v9: adds MapState.aggroedEnemies (which enemy tokens are armed).
        // v10: adds RunState.shrines + the SpawnedEnemy shrine-reward tag.
        public int schemaVersion = 10;
        public RunState run = new RunState();
    }

    [Serializable]
    public class RunState
    {
        public PlayerState player = new PlayerState();
        // Which CharacterSO this run belongs to (v7). Empty = pre-v7 save;
        // DataManager resolves that to defaultCharacter.
        public string characterId = "";
        // Aligned to EmpowerType enum declaration order; one count per color.
        public int[] crystalCounts = Array.Empty<int>();
        public string[] deckCardIds = Array.Empty<string>();     // order = draw order
        public string[] handCardIds = Array.Empty<string>();
        public string[] discardCardIds = Array.Empty<string>();
        public string[] unitIds = Array.Empty<string>();
        // Parallel to unitIds: true = the unit was already used this round.
        public bool[] unitExhausted = Array.Empty<bool>();
        public MapState map = new MapState();
        // One entry per place with defeatedCount > 0; keyed by grid cell.
        // Guardians die in order and never respawn, so a single count fully
        // captures a place's conquest state.
        public PlaceConquest[] places = Array.Empty<PlaceConquest>();
        public int round;
        public int turn;
        public int doom;             // current doom-clock value
        public int roundsSinceSpawn; // spawn-cadence counter (EnemySpawner)
        // Alive mid-run spawns only; defeated ones simply drop out at save time.
        public SpawnedEnemy[] spawnedEnemies = Array.Empty<SpawnedEnemy>();
        // One entry per dungeon with progress or a flag; positions and SO
        // assignment re-derive from the map seed, dungeonId is a content
        // sanity check on restore (v6).
        public DungeonState[] dungeons = Array.Empty<DungeonState>();
        // Once-per-run doom-band flag firings (v6) — never re-fire even if
        // doom relief drops the clock back below a band edge.
        public bool dungeonMidFlagsFired;
        public bool dungeonHighFlagsFired;
        // One entry per hotspot with charges consumed (or a depleted finite
        // tile); positions and SO assignment re-derive from the map seed,
        // hotspotId is a content sanity check on restore (v8).
        public HotspotState[] hotspots = Array.Empty<HotspotState>();
        // One entry per shrine that is no longer Live (consumed or guarding);
        // positions/SO re-derive from the map seed, shrineId is a restore sanity
        // check (v10). state: 0=Live 1=ConsumedDormant 2=Guarding.
        public ShrineState[] shrines = Array.Empty<ShrineState>();
    }

    [Serializable]
    public class PlayerState
    {
        // Toughness: the Defend-shortfall divisor (never a pool). Renamed from
        // `hp` in v7.
        public int toughness;
        // VESTIGIAL — v6 files carry this JSON key and JsonUtility only parses
        // fields that exist on the model. SaveMigrator copies it into
        // `toughness`; nothing else reads or writes it. Do not delete.
        public int hp;
        public int level;
        public int exp;
        public int expToNextLevel;
        public int attack;
        public int defend;
        public int influence;
        public int explore;
        public string[] ownedSkillIds = Array.Empty<string>();
        public string[] exhaustedSkillIds = Array.Empty<string>();
        public float[] position = new float[3];
    }

    [Serializable]
    public class MapState
    {
        public int seed;
        public Cell[] defeatedEnemies = Array.Empty<Cell>();
        // Map cells the player has uncovered (fog cleared). Reveal is monotonic, so
        // re-clearing these on load reproduces the explored state over the seeded map.
        public Cell[] revealedCells = Array.Empty<Cell>();
        // Cells whose enemy token is armed (isAggro) — the halo pulses and the token
        // is clickable. NOT re-derivable from adjacency on load: fleeing a field fight
        // de-aggros a token the player is still standing next to, so it must be
        // persisted rather than recomputed (v9).
        public Cell[] aggroedEnemies = Array.Empty<Cell>();
    }

    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int x;
        public int y;

        public Cell(int x, int y) { this.x = x; this.y = y; }

        public bool Equals(Cell other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => unchecked((x * 397) ^ y);
    }

    [Serializable]
    public struct PlaceConquest
    {
        public int x;
        public int y;
        public int defeatedCount;
    }

    [Serializable]
    public struct SpawnedEnemy
    {
        public int x;
        public int y;
        public string enemyId;
        public int bonusHP;
        public int bonusAttack;
        // Shrine-guardian binding (v10): -1 = a normal spawn with no owed reward.
        // Otherwise the ShrineReward int + the originating shrine's cell, so a
        // bad-roll guardian pays 2x that reward on defeat across save/reload.
        public int shrineRewardType;
        public int shrineCellX;
        public int shrineCellY;
    }

    [Serializable]
    public struct DungeonState
    {
        public int x;
        public int y;
        public string dungeonId;
        public int defeatedCount;
        public bool flagged;
    }

    [Serializable]
    public struct HotspotState
    {
        public int x;
        public int y;
        public string hotspotId;
        public int remainingCharges; // -1 = unlimited (never persisted as depleted)
    }

    [Serializable]
    public struct ShrineState
    {
        public int x;
        public int y;
        public string shrineId;
        public int state; // 0=Live, 1=ConsumedDormant, 2=Guarding
    }
}
