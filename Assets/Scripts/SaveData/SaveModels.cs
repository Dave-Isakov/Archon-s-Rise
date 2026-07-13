using System;

namespace ArchonsRise.SaveData
{
    [Serializable]
    public class SaveFile
    {
        // v6: adds RunState.dungeons + the once-per-run doom-band flag bools
        // (M2.9 map dungeons).
        public int schemaVersion = 6;
        public RunState run = new RunState();
    }

    [Serializable]
    public class RunState
    {
        public PlayerState player = new PlayerState();
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
    }

    [Serializable]
    public class PlayerState
    {
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
}
