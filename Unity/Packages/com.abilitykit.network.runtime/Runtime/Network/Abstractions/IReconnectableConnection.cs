using System;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>
    /// 支持重连调度及手动恢复的可选连接能力。
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
