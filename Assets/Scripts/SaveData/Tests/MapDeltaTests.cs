using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class MapDeltaTests
    {
        [Test]
        public void IsDefeated_TrueForRecordedCell()
        {
            var set = MapDelta.ToSet(new[] { new Cell(2, 3), new Cell(4, 5) });
            Assert.IsTrue(MapDelta.IsDefeated(set, new Cell(4, 5)));
            Assert.IsFalse(MapDelta.IsDefeated(set, new Cell(4, 6)));
        }

        [Test]
        public void ToArray_RoundTripsSet()
        {
            var set = MapDelta.ToSet(new[] { new Cell(1, 1), new Cell(1, 1), new Cell(2, 2) });
            var arr = MapDelta.ToArray(set);
            Assert.AreEqual(2, arr.Length); // de-duplicated
            CollectionAssert.Contains(arr, new Cell(1, 1));
            CollectionAssert.Contains(arr, new Cell(2, 2));
        }
    }
}
