using System;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>
    /// Optional connection capability for reconnect scheduling and manual recovery.
    /// </summary>
    public interface IReconnectableConnection
    {
        bool IsReconnectExhausted { get; }

        event Action<int, float> ReconnectScheduled;
        event Action<int> ReconnectAttemptStarted;
        event Action<int> ReconnectExhausted;

        void ResetReconnect();
    }
}
