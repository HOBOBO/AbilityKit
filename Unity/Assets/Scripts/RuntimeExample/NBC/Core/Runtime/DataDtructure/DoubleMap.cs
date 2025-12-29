using System;
using System.Collections.Generic;

namespace NBC
{
    public class DoubleMap<K, V>
    {
        private readonly Dictionary<K, V> kv = new Dictionary<K, V>();
        private readonly Dictionary<V, K> vk = new Dictionary<V, K>();

        public DoubleMap()
        {
        }

        public DoubleMap(int capacity)
        {
            kv = new Dictionary<K, V>(capacity);
            vk = new Dictionary<V, K>(capacity);
        }

        public void ForEach(Action<K, V> action)
        {
            if (action == null)
            {
                return;
            }

            Dictionary<K, V>.KeyCollection keys = kv.Keys;
            foreach (K key in keys)
            {
                action(key, kv[key]);
            }
        }

        public List<K> Keys => new List<K>(kv.Keys);

        public List<V> Values => new List<V>(vk.Keys);

        public void Add(K key, V value)
        {
            if (key == null || value == null || kv.ContainsKey(key) || vk.ContainsKey(value))
            {
                return;
            }

            kv.Add(key, value);
            vk.Add(value, key);
        }

        public V GetValueByKey(K key)
        {
            if (key != null && kv.TryGetValue(key, out var byKey))
            {
                return byKey;
            }

            return default(V);
        }

        public K GetKeyByValue(V value)
        {
            if (value != null && vk.TryGetValue(value, out var byValue))
            {
                return byValue;
            }

            return default(K);
        }

        public void RemoveByKey(K key)
        {
            if (key == null)
            {
                return;
            }

            if (!kv.TryGetValue(key, out var value))
            {
                return;
            }

            kv.Remove(key);
            vk.Remove(value);
        }

        public void RemoveByValue(V value)
        {
            if (value == null)
            {
                return;
            }

            if (!vk.TryGetValue(value, out var key))
            {
                return;
            }

            kv.Remove(key);
            vk.Remove(value);
        }

        public void Clear()
        {
            kv.Clear();
            vk.Clear();
        }

        public bool ContainsKey(K key)
        {
            return key != null && kv.ContainsKey(key);
        }

        public bool ContainsValue(V value)
        {
            return value != null && vk.ContainsKey(value);
        }

        public bool Contains(K key, V value)
        {
            if (key == null || value == null)
            {
                return false;
            }

            return kv.ContainsKey(key) && vk.ContainsKey(value);
        }
    }
}