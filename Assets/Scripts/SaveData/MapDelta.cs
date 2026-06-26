using System.Collections.Generic;

namespace ArchonsRise.SaveData
{
    public static class MapDelta
    {
        public static HashSet<Cell> ToSet(IEnumerable<Cell> cells) => new HashSet<Cell>(cells);

        public static Cell[] ToArray(HashSet<Cell> cells)
        {
            var arr = new Cell[cells.Count];
            cells.CopyTo(arr);
            return arr;
        }

        public static bool IsDefeated(HashSet<Cell> defeated, Cell cell) => defeated.Contains(cell);
    }
}
