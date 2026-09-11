using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Collections
{
    /// <summary>
    /// Non-thread-safe store intended to be owned by one trigger execution session.
    /// </summary>
    public sealed class TriggerCollectionStore : ITriggerCollectionStore
    {
        private readonly Dictionary<int, IReadOnlyTriggerCollection> _collections =
            new Dictionary<int, IReadOnlyTriggerCollection>();
        private int _nextHandle = 1;
        private bool _disposed;

        public int Count => _collections.Count;

        public TriggerCollectionHandle Register(IReadOnlyTriggerCollection collection)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TriggerCollectionStore));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (_nextHandle <= 0)
                throw new InvalidOperationException("Trigger collection handle space is exhausted.");

            var handle = new TriggerCollectionHandle(_nextHandle++);
            _collections.Add(handle.Value, collection);
            return handle;
        }

        public bool TryResolve(TriggerCollectionHandle handle, out IReadOnlyTriggerCollection collection)
        {
            if (_disposed || !handle.IsValid)
            {
                collection = null;
                return false;
            }
            return _collections.TryGetValue(handle.Value, out collection);
        }

        public bool Release(TriggerCollectionHandle handle)
        {
            return !_disposed && handle.IsValid && _collections.Remove(handle.Value);
        }

        public void Clear()
        {
            if (_disposed) return;
            _collections.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _collections.Clear();
        }
    }
}
