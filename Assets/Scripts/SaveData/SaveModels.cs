using System;

namespace ArchonsRise.SaveData
{
    [Serializable]
    public class SaveFile
    {
        // v5: adds RunState.unitExhausted (parallel to unitIds; mid-round saves
        // keep used units turned).
        public int schemaVersion = 5;
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
}
