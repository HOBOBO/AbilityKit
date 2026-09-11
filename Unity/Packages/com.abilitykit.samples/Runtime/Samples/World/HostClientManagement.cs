#nullable enable

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
    /// 演示 HostRuntime 如何管理客户端连接、消息发送和 World 生命周期广播。
    /// </summary>
    [Sample(603, "world", "host", "client", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class HostClientManagement : SampleBase
    {
        public override string Title => "World Host Client";
        public override string Description => "使用 HostRuntime 管理客户端连接、消息广播和 World 生命周期消息";
        public override SampleCategory Category => SampleCategory.World;

        private int _beforeSendCount;
        private int _afterSendCount;

        protected override void OnRun()
        {
            var registry = new WorldTypeRegistry();
            registry.Register("LobbyWorld", CreateWorld);
            var worldManager = new WorldManager(new RegistryWorldFactory(registry));
            var host = new HostRuntime(worldManager, CreateOptions());

            var player1 = new RecordingConnection(new ServerClientId("player-1"));
            var player2 = new RecordingConnection(new ServerClientId("player-2"));

            Section("连接客户端并创建 World");
            host.Connect(player1);
            host.Connect(player2);
            KeyValue("HostClient.ConnectedClients", "2");
            var lobby = host.CreateWorld(new WorldCreateOptions
            {
                Id = new WorldId("lobby"),
                WorldType = "LobbyWorld"
            });

            KeyValue("WorldId", lobby.Id.Value);
            KeyValue("HostClient.WorldCreated", lobby.Id.Value);
            KeyValue("Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("FirstMessage", player1.LastMessageName);

            Divider();
            Section("广播与点对点消息");
            host.Broadcast(new TextServerMessage("match-ready"));
            host.SendTo(player2, new TextServerMessage("private-loadout"));

            KeyValue("Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("BeforeSend", _beforeSendCount.ToString());
            KeyValue("HostClient.BeforeSend", _beforeSendCount.ToString());
            KeyValue("AfterSend", _afterSendCount.ToString());

            Divider();
            Section("断开客户端与固定帧 Tick");
            host.Disconnect(player1.ClientId);
            host.Broadcast(new TextServerMessage("player-1-left"));
            host.Tick(0.05f);

            var clock = lobby.Services.Resolve<IWorldClock>();
            KeyValue("Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player1Messages", player1.ReceivedMessages.Count.ToString());
            KeyValue("Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("HostClient.Player2Messages", player2.ReceivedMessages.Count.ToString());
            KeyValue("LobbyTime", clock.Time.ToString("F2"));
            KeyValue("HostClient.LobbyTime", clock.Time.ToString("F2"));

            Divider();
            Section("销毁 World 自动广播");
            var destroyed = host.DestroyWorld(lobby.Id);
            KeyValue("Destroyed", destroyed.ToString());
            KeyValue("HostClient.Destroyed", destroyed.ToString());
            KeyValue("Player2LastMessage", player2.LastMessageName);
            KeyValue("HostClient.Player2LastMessage", player2.LastMessageName);

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("HostRuntime.Connect / Disconnect：维护服务端客户端连接表。 ");
            Bullet("HostRuntime.Broadcast / SendTo：统一触发发送前后钩子。 ");
            Bullet("HostRuntime.CreateWorld / DestroyWorld：封装 WorldManager 并广播生命周期消息。 ");
            Bullet("HostRuntime.Tick：由宿主统一驱动 WorldManager.Tick。 ");
        }

        private HostRuntimeOptions CreateOptions()
        {
            return new HostRuntimeOptions
            {
                OnBeforeCreateWorld = options => KeyValue("BeforeCreateWorld", options.Id.Value),
                OnWorldCreated = world => KeyValue("WorldCreated", world.Id.Value),
                OnWorldDestroyed = id => KeyValue("WorldDestroyed", id.Value),
                OnPreTick = deltaTime => KeyValue("PreTick", deltaTime.ToString("F2")),
                OnPostTick = deltaTime => KeyValue("PostTick", deltaTime.ToString("F2")),
                OnBeforeSendMessage = (_, _) => _beforeSendCount++,
                OnAfterSendMessage = (_, _) => _afterSendCount++
            };
        }

        private static IWorld CreateWorld(WorldCreateOptions options)
        {
            var builder = new WorldContainerBuilder();
            builder.RegisterServiceType<IWorldClock, WorldClock>(WorldLifetime.Singleton);
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);

            return new HostWorld(options.Id, options.WorldType, builder.Build());
        }

        private sealed class TextServerMessage : ServerMessage
        {
            public TextServerMessage(string text)
            {
                Text = text;
            }

            public string Text { get; }
        }

        private sealed class RecordingConnection : IServerConnection
        {
            private readonly List<ServerMessage> _receivedMessages = new List<ServerMessage>();

            public RecordingConnection(ServerClientId clientId)
            {
                ClientId = clientId;
            }

            public ServerClientId ClientId { get; }
            public IReadOnlyList<ServerMessage> ReceivedMessages => _receivedMessages;
            public string LastMessageName => _receivedMessages.Count == 0 ? "none" : _receivedMessages[_receivedMessages.Count - 1].GetType().Name;

            public void Send(ServerMessage message)
            {
                _receivedMessages.Add(message);
            }
        }

        private sealed class HostWorld : IWorld
        {
            private readonly IWorldResolver _services;
            private bool _disposed;

            public HostWorld(WorldId id, string worldType, IWorldResolver services)
            {
                Id = id;
                WorldType = worldType;
                _services = services;
            }

            public WorldId Id { get; }
            public string WorldType { get; }
            public IWorldResolver Services => _services;

            public void Initialize()
            {
            }

            public void Tick(float deltaTime)
            {
                _services.Resolve<IWorldClock>().Tick(deltaTime);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_services is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
