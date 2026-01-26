using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IDispatcher
    {
        void Post(Action action);
    }
}
