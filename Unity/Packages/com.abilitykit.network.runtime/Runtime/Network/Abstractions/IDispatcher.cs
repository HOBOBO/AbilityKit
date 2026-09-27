using System;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>
    /// Schedules work on the consumer thread. Inline dispatch runs on the caller's thread;
    /// Unity consumers must supply a dispatcher captured on the Unity main thread.
    /// Callers own any mutable payload captured by a deferred action.
    /// </summary>
    public interface IDispatcher
    {
        void Post(Action action);
    }
}
