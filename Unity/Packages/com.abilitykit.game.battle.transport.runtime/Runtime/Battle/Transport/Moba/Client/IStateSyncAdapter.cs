using System;
using System.Collections.Generic;

using AbilityKit.Network.Battle;
namespace AbilityKit.Game.Battle.Transport.Moba.Client
{
    /// <summary>
    /// 状态同步模式
    /// </summary>
    public enum SyncMode
    {
        Lockstep = 0,
        StateSync = 1,
        Hybrid = 2
    }

    /// <summary>
    /// 实体快照接口
    /// </summary>
    public interface IActorSnapshot
    {
        int ActorId { get; }
        float PositionX { get; }
        float PositionY { get; }
        float PositionZ { get; }
        float Rotation { get; }
        float VelocityX { get; }
        float VelocityZ { get; }
        float Hp { get; }
        float HpMax { get; }
        int TeamId { get; }
    }

    /// <summary>
    /// 世界快照接口
    /// </summary>
    public interface IWorldSnapshot
    {
        ulong WorldId { get; }
        int Frame { get; }
        long Timestamp { get; }
        bool IsFullSnapshot { get; }
        IReadOnlyList<IActorSnapshot> Actors { get; }
    }

    /// <summary>
    /// 帧输入项接口
    /// </summary>
    public interface IFrameInput
    {
        uint PlayerId { get; }
        uint OpCode { get; }
        byte[] Payload { get; }
    }

    /// <summary>
    /// 帧数据接口
    /// </summary>
    public interface IFrameData
    {
        int Frame { get; }
        IReadOnlyList<IFrameInput> Inputs { get; }
    }

    /// <summary>
    /// 状态同步适配器接口
    /// 统一帧同步和状态同步两种模式
    /// </summary>
    public interface IStateSyncAdapter : IDisposable
    {
        SyncMode Mode { get; }
        bool IsConnected { get; }
        int CurrentFrame { get; }
        int LocalActorId { get; }

        /// <summary>
        /// 网络客户端实现，可替换
        /// </summary>
        INetworkClient GatewayClient { get; set; }

        event Action<bool> OnConnectionChanged;
        event Action<int> OnFrameAdvanced;
        event Action<IWorldSnapshot> OnSnapshotReceived;

        void Initialize(object context, object config);
        void Connect(string host, int port, string roomId, string playerId);
        void Disconnect();
        void SubmitInput(uint playerId, uint opCode, byte[] payload = null);
        void Tick(float deltaTime);
        IWorldSnapshot GetLatestSnapshot();
    }

    /// <summary>
    /// 玩家输入
    /// </summary>
    public readonly struct PlayerInput
    {
        public uint PlayerId { get; init; }
        public uint OpCode { get; init; }
        public byte[] Payload { get; init; }

        public static PlayerInput Create(uint playerId, uint opCode, byte[] payload = null) => new PlayerInput
        {
            PlayerId = playerId,
            OpCode = opCode,
            Payload = payload ?? new byte[0]
        };
    }
}
