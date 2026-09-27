using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public interface IPresentationCueViewPort<TKey, TPayload, THandle>
    {
        bool IsAlive(THandle handle);

        bool TryCreate(TKey key, in TPayload payload, out THandle handle);

        void Update(THandle handle, in TPayload payload);

        void Destroy(THandle handle);
    }

    /// <summary>
    /// Owns presentation handles independently from any rendering platform.
    /// Failed destroys remain registered so teardown can retry them.
    /// </summary>
    public sealed class PresentationCueViewRegistry<TKey, TPayload, THandle>
    {
        private readonly IPresentationCueViewPort<TKey, TPayload, THandle> _port;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private readonly Dictionary<TKey, THandle> _active;
        private readonly List<TKey> _clearBuffer = new List<TKey>();

        public PresentationCueViewRegistry(
            IPresentationCueViewPort<TKey, TPayload, THandle> port,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _active = new Dictionary<TKey, THandle>(_keyComparer);
        }

        public int ActiveCount => _active.Count;

        public long CreatedCount { get; private set; }

        public long UpdatedCount { get; private set; }

        public long DestroyedCount { get; private set; }

        public long FailedCreateCount { get; private set; }

        public long StaleHandleCount { get; private set; }

        public bool Apply(in PresentationCueReconciliationDecision<TKey, TPayload> decision)
        {
            if (!decision.Accepted ||
                _keyComparer.Equals(decision.Key, default(TKey)))
            {
                return false;
            }

            switch (decision.Command)
            {
                case PresentationCueCommandKind.EnsureActive:
                    var payload = decision.Payload;
                    return EnsureActive(decision.Key, in payload);
                case PresentationCueCommandKind.Stop:
                    return Stop(decision.Key);
                default:
                    return false;
            }
        }

        public bool TryGetHandle(TKey key, out THandle handle)
        {
            if (_keyComparer.Equals(key, default(TKey)))
            {
                handle = default(THandle);
                return false;
            }

            return _active.TryGetValue(key, out handle);
        }

        public bool EnsureActive(TKey key, in TPayload payload)
        {
            if (_keyComparer.Equals(key, default(TKey))) return false;

            if (_active.TryGetValue(key, out var existing))
            {
                if (_port.IsAlive(existing))
                {
                    _port.Update(existing, in payload);
                    UpdatedCount++;
                    return true;
                }

                _active.Remove(key);
                StaleHandleCount++;
            }

            if (!_port.TryCreate(key, in payload, out var created))
            {
                FailedCreateCount++;
                return false;
            }

            _active.Add(key, created);
            CreatedCount++;
            return true;
        }

        public bool Stop(TKey key)
        {
            if (_keyComparer.Equals(key, default(TKey)) ||
                !_active.TryGetValue(key, out var handle))
            {
                return false;
            }

            if (_port.IsAlive(handle))
            {
                _port.Destroy(handle);
                DestroyedCount++;
            }
            else
            {
                StaleHandleCount++;
            }

            _active.Remove(key);
            return true;
        }

        public void Clear()
        {
            if (_active.Count == 0) return;

            _clearBuffer.Clear();
            foreach (var pair in _active)
            {
                _clearBuffer.Add(pair.Key);
            }

            List<Exception> failures = null;
            for (var i = 0; i < _clearBuffer.Count; i++)
            {
                try
                {
                    Stop(_clearBuffer[i]);
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }

            _clearBuffer.Clear();
            if (failures != null)
            {
                throw new AggregateException(
                    "One or more presentation cue views failed to stop.",
                    failures);
            }
        }
    }
}
