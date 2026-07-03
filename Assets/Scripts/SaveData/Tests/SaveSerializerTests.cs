using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class SaveSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new SaveFile
            {
                schemaVersion = 2,
                run = new RunState
                {
                    player = new PlayerState
                    {
                        hp = 2, handSize = 5, level = 3, exp = 7, expToNextLevel = 20,
                        attack = 1, defend = 2, influence = 3, explore = 4,
                        position = new float[] { 1.5f, -2.5f, 0f }
                    },
                    crystalCounts = new[] { 1, 0, 2, 0, 1 },
                    deckCardIds = new[] { "card_attack", "card_defend" },
                    handCardIds = new[] { "card_explore" },
                    discardCardIds = new[] { "card_wound" },
                    unitIds = new[] { "unit_knight" },
                    places = new[]
                    {
                        new PlaceConquest { x = 5, y = 9, defeatedCount = 1 },
                        new PlaceConquest { x = 8, y = 3, defeatedCount = 2 }
                    },
                    map = new MapState
                    {
                        seed = 123456,
                        defeatedEnemies = new[] { new Cell(3, 4), new Cell(5, 6) }
                    },
                    round = 2,
                    turn = 1
                }
            };

            string json = SaveSerializer.ToJson(original);
            SaveFile restored = SaveSerializer.FromJson(json);

            // Re-serialize and compare JSON strings: any lost/changed field shows as a diff.
            Assert.AreEqual(json, SaveSerializer.ToJson(restored));
        }

        [Test]
        public void Cell_ValueEquality_WorksInHashSet()
        {
            var set = new System.Collections.Generic.HashSet<Cell> { new Cell(1, 2) };
            Assert.IsTrue(set.Contains(new Cell(1, 2)));
            Assert.IsFalse(set.Contains(new Cell(2, 1)));
        }
    }
}
