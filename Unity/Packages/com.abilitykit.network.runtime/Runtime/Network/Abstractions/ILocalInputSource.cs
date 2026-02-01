using System;

namespace AbilityKit.Network.Abstractions
{
    public interface ILocalInputSource<TInput> : IDisposable
    {
        int LocalFrame { get; }

        bool TryDequeue(out TInput input);
    }
}
