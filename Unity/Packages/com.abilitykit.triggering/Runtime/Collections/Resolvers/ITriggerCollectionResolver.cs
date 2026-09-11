using System;

namespace AbilityKit.Triggering.Collections
{
    public interface ITriggerCollectionResolver
    {
        bool TryResolve(TriggerCollectionHandle handle, out IReadOnlyTriggerCollection collection);
    }

    public interface IMutableTriggerCollectionResolver : ITriggerCollectionResolver
    {
        TriggerCollectionHandle Register(IReadOnlyTriggerCollection collection);
        bool Release(TriggerCollectionHandle handle);
    }

    public interface ITriggerCollectionStore : IMutableTriggerCollectionResolver, IDisposable
    {
        int Count { get; }
        void Clear();
    }
}
