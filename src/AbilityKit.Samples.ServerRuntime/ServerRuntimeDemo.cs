using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;

namespace AbilityKit.Samples.ServerRuntime;

/// <summary>输入 opCode：1=右移一帧，2=受击扣血（与 SyncRuntime Starter 同一套语义）。</summary>
public static class ServerInput
{
    public const int Move = 1;

    public const int Hit = 2;
}

/// <summary>
/// 权威战斗世界：一个英雄（位置 + 血量），按帧输入确定性推进。
/// 同时实现 <see cref="IWorldInputSink"/>（接收服务器投递的帧输入）
/// 与 <see cref="IWorldStateSnapshotProvider"/>（向服务器提供状态快照），
/// 这两个契约正是 FrameSyncDriverModule 在 PreTick / PostTick 钩子里寻找的扩展点。
/// </summary>
public sealed class ServerBattleWorld : IWorld, IWorldInputSink, IWorldStateSnapshotProvider
{
    public const int SnapshotOpCode = 1001;

    private const float MoveSpeed = 2f;
    private const float HitDamage = 10f;

    private readonly WorldContainer _container;
    private IReadOnlyList<PlayerInputCommand> _pending = Array.Empty<PlayerInputCommand>();

    public ServerBattleWorld(WorldId id, string worldType)
    {
        Id = id;
        WorldType = worldType;

        // 把自己注册进世界服务容器：FrameSyncDriverModule 通过 world.Services 解析这两个契约。
        var builder = new WorldContainerBuilder();
        builder.RegisterInstance<IWorldInputSink>(this);
        builder.RegisterInstance<IWorldStateSnapshotProvider>(this);
        _container = builder.Build();
    }

    public WorldId Id { get; }

    public string WorldType { get; }

    public IWorldResolver Services => _container;

    public float X { get; private set; }

    public float Health { get; private set; } = 100f;

    /// <summary>PreTick 钩子调用：接收服务器在下一帧要执行的客户端输入。</summary>
    public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        => _pending = inputs ?? Array.Empty<PlayerInputCommand>();

    /// <summary>服务器帧循环调用：确定性推进权威状态。</summary>
    public void Tick(float deltaTime)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            switch (_pending[i].OpCode)
            {
                case ServerInput.Move:
                    X += MoveSpeed * deltaTime;
                    break;
                case ServerInput.Hit:
                    Health -= HitDamage;
                    break;
            }
        }

        _pending = Array.Empty<PlayerInputCommand>();
    }

    /// <summary>PostTick 钩子调用：导出快照，服务器把它广播给所有客户端。</summary>
    public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
    {
        var payload = new byte[8];
        BitConverter.GetBytes(X).CopyTo(payload, 0);
        BitConverter.GetBytes(Health).CopyTo(payload, 4);
        snapshot = new WorldStateSnapshot(SnapshotOpCode, payload);
        return true;
    }

    public void Initialize()
    {
        Log.Info($"[World] 权威世界就绪 —— worldId={Id}, type={WorldType}");
    }

    private bool _disposed;

    public void Dispose()
    {
        // this 已注册为容器单例，容器 Dispose 会回调本方法——用标志防重入。
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _container.Dispose();
    }
}

/// <summary>本地回环客户端：只实现 IServerConnection，把服务器广播的消息打印出来。</summary>
public sealed class LoopbackClient : IServerConnection
{
    private int _frameCount;

    public LoopbackClient(string clientId)
    {
        ClientId = new ServerClientId(clientId);
    }

    public ServerClientId ClientId { get; }

    public void Send(ServerMessage message)
    {
        switch (message)
        {
            case WorldCreatedMessage created:
                Log.Info($"[Client:{ClientId}] 收到 WorldCreated —— worldId={created.WorldId}, type={created.WorldType}");
                break;
            case WorldDestroyedMessage destroyed:
                Log.Info($"[Client:{ClientId}] 收到 WorldDestroyed —— worldId={destroyed.WorldId}");
                break;
            case FrameMessage frame:
                _frameCount++;
                var packet = frame.Packet;
                if (packet.Snapshot is { Payload: { Length: 8 } payload })
                {
                    var x = BitConverter.ToSingle(payload, 0);
                    var hp = BitConverter.ToSingle(payload, 4);
                    Log.Info($"[Client:{ClientId}] 收到帧包 frame={packet.Frame.Value} —— 服务端权威状态 x={x:0.00} hp={hp:0}");
                }
                else
                {
                    Log.Info($"[Client:{ClientId}] 收到帧包 frame={packet.Frame.Value}（无快照载荷）");
                }

                break;
            default:
                Log.Info($"[Client:{ClientId}] 收到 {message.GetType().Name}");
                break;
        }
    }

    public int ReceivedFrames => _frameCount;
}
