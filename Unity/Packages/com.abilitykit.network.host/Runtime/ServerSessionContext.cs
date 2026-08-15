using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Network.Host
{
    /// <summary>
    /// Transport-neutral per-session state bag for authentication, room binding,
    /// correlation data, and application extensions.
    /// </summary>
    public sealed class ServerSessionContext
    {
        private readonly ConcurrentDictionary<string, object> _items =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private int _established;

        public IReadOnlyDictionary<string, object> Items => _items;
        public bool IsEstablished => Volatile.Read(ref _established) != 0;

        public void MarkEstablished()
        {
            Interlocked.Exchange(ref _established, 1);
        }

        public void Set<T>(string key, T value)
        {
            ValidateKey(key);
            if (value == null) throw new ArgumentNullException(nameof(value));
            _items[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);
            if (_items.TryGetValue(key, out var item) && item is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public bool Remove(string key)
        {
            ValidateKey(key);
            return _items.TryRemove(key, out _);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Context key is required.", nameof(key));
        }
    }
}
