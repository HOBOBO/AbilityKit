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
        private readonly PresentationInstanceRegistry<TKey, TPayload, THandle> _instances;

        public PresentationCueViewRegistry(
            IPresentationCueViewPort<TKey, TPayload, THandle> port,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _instances = new PresentationInstanceRegistry<TKey, TPayload, THandle>(port, keyComparer);
        }

        public int ActiveCount => _instances.ActiveCount;

        public long CreatedCount => _instances.CreatedCount;

        public long UpdatedCount => _instances.UpdatedCount;

        public long DestroyedCount => _instances.DestroyedCount;

        public long FailedCreateCount => _instances.FailedCreateCount;

        public long StaleHandleCount => _instances.StaleHandleCount;

        public bool Apply(in PresentationCueReconciliationDecision<TKey, TPayload> decision)
        {
            if (!decision.Accepted)
            {
                return false;
            }

            switch (decision.Command)
            {
                case PresentationCueCommandKind.EnsureActive:
                    var payload = decision.Payload;
                    return _instances.EnsureActive(decision.Key, in payload);
                case PresentationCueCommandKind.Stop:
                    return _instances.Stop(decision.Key);
                case PresentationCueCommandKind.EmitTransient:
                    var transientPayload = decision.Payload;
                    return _instances.EmitTransient(decision.Key, in transientPayload);
                default:
                    return false;
            }
        }

        public bool TryGetHandle(TKey key, out THandle handle)
        {
            return _instances.TryGetHandle(key, out handle);
        }

        public bool EnsureActive(TKey key, in TPayload payload)
        {
            return _instances.EnsureActive(key, in payload);
        }

        public bool Stop(TKey key)
        {
            return _instances.Stop(key);
        }

        public bool EmitTransient(TKey key, in TPayload payload)
        {
            return _instances.EmitTransient(key, in payload);
        }

        public void Clear()
        {
            _instances.Clear();
        }

        public void ReconcileActiveSet(IReadOnlyCollection<TKey> desiredKeys)
        {
            _instances.ReconcileActiveSet(desiredKeys);
        }
    }
}
