using System;
using System.Collections.Generic;
using NUnit.Framework;
using ArchonsRise.SaveData;

namespace ArchonsRise.SaveData.Tests
{
    public class ContentRegistryTests
    {
        private class Item
        {
            public string Id;
            public Item(string id) { Id = id; }
        }

        private static ContentRegistry<Item> Build(params string[] ids)
        {
            var items = new List<Item>();
            foreach (var id in ids) items.Add(new Item(id));
            return new ContentRegistry<Item>(items, i => i.Id);
        }

        [Test]
        public void Get_ReturnsItemById()
        {
            var reg = Build("a", "b");
            Assert.AreEqual("b", reg.Get("b").Id);
        }

        [Test]
        public void Resolve_PreservesOrder()
        {
            var reg = Build("a", "b", "c");
            var resolved = reg.Resolve(new[] { "c", "a", "c" });
            CollectionAssert.AreEqual(new[] { "c", "a", "c" }, resolved.ConvertAll(i => i.Id));
        }

        [Test]
        public void DuplicateId_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build("a", "a"));
        }

        [Test]
        public void EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build("a", ""));
        }

        [Test]
        public void MissingId_Throws()
        {
            var reg = Build("a");
            Assert.Throws<KeyNotFoundException>(() => reg.Get("zzz"));
        }
    }
}
