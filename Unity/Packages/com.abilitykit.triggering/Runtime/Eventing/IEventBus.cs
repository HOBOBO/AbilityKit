using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Eventing
{
    public interface IEventBus
    {
        void Publish<TArgs>(EventKey<TArgs> key, in TArgs args);
        void Publish<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control);

        IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs> handler);
        IDisposable Subscribe<TArgs>(EventKey<TArgs> key, Action<TArgs, ExecutionControl> handler);
        void Flush();
    }
}
