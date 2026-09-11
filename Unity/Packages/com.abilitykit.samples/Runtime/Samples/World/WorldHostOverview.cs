#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// WorldHostOverview - 演示 HostRuntime 和 World 管理
    /// 展示如何使用 HostRuntime 创建和管理多个 World
    /// </summary>
    [Sample]
    public sealed class WorldHostOverview : SampleBase
    {
        public override string Title => "WorldHost Overview";
        public override string Description => "HostRuntime、World 管理、消息广播、客户端连接";
        public override SampleCategory Category => SampleCategory.World;

        private WorldTypeRegistry _registry;

        protected override void OnRun()
        {
            Log("=== WorldHost 概述 ===");
            Output.Divider();

            // 1. HostRuntime 概念
            Log("【1】HostRuntime 概念");
            Output.Bullet("IWorldHost - World 生命周期管理");
            Output.Bullet("IServerConnectionHost - 客户端连接管理");
            Output.Bullet("支持 Hook 回调 - PreTick/PostTick/WorldCreated 等");
            Log("");

            // 2. 创建 WorldTypeRegistry
            Log("【2】创建 WorldTypeRegistry");
            _registry = new WorldTypeRegistry();
            _registry.Register("LobbyWorld", options => CreateWorldInstance(options));
            _registry.Register("BattleWorld", options => CreateWorldInstance(options));
            Log("WorldTypeRegistry 创建完成");
            Log("");

            // 3. 创建 WorldManager
            Log("【3】创建 WorldManager");
            var factory = new RegistryWorldFactory(_registry);
            var worldManager = new WorldManager(factory);
            Log("WorldManager 创建完成");
            Log("");

            // 4. 配置 Hooks
            Log("【4】配置生命周期钩子");
            var options = new HostRuntimeOptions
            {
                OnPreTick = delta => Log($"PreTick: delta={delta:F4}"),
                OnPostTick = _ => Log($"PostTick: 帧结束"),
                OnBeforeCreateWorld = opts => Log($"准备创建: {opts.Id.Value}"),
                OnWorldCreated = world => Log($"World 已创建: {world.Id.Value}"),
                OnWorldDestroyed = id => Log($"World 已销毁: {id.Value}")
            };
            Log("已配置 5 个 Hook 回调");
            Log("");

            // 5. 创建 HostRuntime
            Log("【5】创建 HostRuntime");
            var host = new HostRuntime(worldManager, options);
            Log("HostRuntime 创建完成");
            Log("");

            // 6. 创建 World
            Log("【6】创建 World");
            var world1 = host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("lobby"),
                WorldType = "LobbyWorld"
            });

            var world2 = host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("battle"),
                WorldType = "BattleWorld"
            });

            Log($"当前 World 数量: {worldManager.Worlds.Count}");
            Log("");

            // 7. 模拟客户端连接
            Log("【7】模拟客户端连接");
            var mockConnection = new MockServerConnection(new ServerClientId("client-1"));
            host.Connect(mockConnection);
            Log($"客户端连接: {mockConnection.ClientId}");

            mockConnection = new MockServerConnection(new ServerClientId("client-2"));
            host.Connect(mockConnection);
            Log($"客户端连接: {mockConnection.ClientId}");
            Log("");

            // 8. 消息广播
            Log("【8】消息广播");
            host.Broadcast(new WorldCreatedMessage(
                new WorldId("new-world"),
                "TestWorld"));
            Log("已广播 WorldCreatedMessage");
            Log("");

            // 9. Tick 驱动
            Log("【9】Tick 驱动");
            host.Tick(0.016f);
            host.Tick(0.016f);
            Log("已执行 2 帧");
            Log("");

            // 10. 销毁 World
            Log("【10】销毁 World");
            host.DestroyWorld(new WorldId("lobby"));
            Log($"当前 World 数量: {worldManager.Worlds.Count}");
            Log("");

            // 11. 清理
            Log("【11】清理资源");
            worldManager.DisposeAll();
            Log("所有资源已释放");

            Output.Divider();
        }

        private IWorld CreateWorldInstance(WorldCreateOptions options)
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);
            builder.RegisterServiceType<DefaultWorldRandom, DefaultWorldRandom>(WorldLifetime.Singleton);
            var container = builder.Build();
            return new MockWorldInstance(options.Id, options.WorldType, container);
        }
    }

    /// <summary>
    /// 模拟服务器连接
    /// </summary>
    public sealed class MockServerConnection : IServerConnection
    {
        private readonly List<ServerMessage> _receivedMessages = new List<ServerMessage>();

        public MockServerConnection(ServerClientId clientId)
        {
            ClientId = clientId;
        }

        public ServerClientId ClientId { get; }

        public IReadOnlyList<ServerMessage> ReceivedMessages => _receivedMessages;

        public void Send(ServerMessage message)
        {
            _receivedMessages.Add(message);
        }
    }

    /// <summary>
    /// 模拟 World 实例
    /// </summary>
    public sealed class MockWorldInstance : IWorld
    {
        private readonly WorldId _id;
        private readonly string _worldType;
        private readonly IWorldResolver _services;
        private bool _disposed;

        public MockWorldInstance(WorldId id, string worldType, IWorldResolver services)
        {
            _id = id;
            _worldType = worldType;
            _services = services;
        }

        public WorldId Id => _id;
        public string WorldType => _worldType;
        public IWorldResolver Services => _services;

        public void Initialize() { }

        public void Tick(float deltaTime)
        {
            if (_services.TryResolve<IWorldClock>(out var clock))
            {
                clock.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_services is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
