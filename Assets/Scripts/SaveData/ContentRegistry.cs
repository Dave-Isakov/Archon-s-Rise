using System;
using System.Collections.Generic;

namespace ArchonsRise.SaveData
{
    public class ContentRegistry<T>
    {
        private readonly Dictionary<string, T> _byId = new Dictionary<string, T>();
        private readonly List<T> _items = new List<T>();

        public ContentRegistry(IEnumerable<T> items, Func<T, string> idSelector)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (idSelector == null) throw new ArgumentNullException(nameof(idSelector));

            foreach (var item in items)
            {
                var id = idSelector(item);
                if (string.IsNullOrEmpty(id))
                    throw new ArgumentException($"Content item has a null or empty id: {item}");
                if (_byId.ContainsKey(id))
                    throw new ArgumentException($"Duplicate content id: '{id}'");
                _byId.Add(id, item);
                _items.Add(item);
            }
        }

        public IReadOnlyList<T> Items => _items;

        public bool TryGet(string id, out T item) => _byId.TryGetValue(id, out item);

        public T Get(string id)
        {
            if (_byId.TryGetValue(id, out var item)) return item;
            throw new KeyNotFoundException($"No content registered for id '{id}'.");
        }

        public List<T> Resolve(IEnumerable<string> ids)
        {
            var result = new List<T>();
            foreach (var id in ids) result.Add(Get(id));
            return result;
        }
    }
}
