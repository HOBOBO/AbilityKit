using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class InMemoryBattleLogicTransport : IBattleLogicTransport, IHostClient
    {
        private readonly HostRuntime _server;
        private readonly ServerClientId _clientId;
        private HostClientConnectionAdapter _connection;

        public InMemoryBattleLogicTransport(HostRuntime server, string clientId = "in_memory")
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _clientId = new ServerClientId(clientId);
        }

        public event Action<FramePacket> FramePushed;

        public ServerClientId ClientId => _clientId;

        public void Connect()
        {
            _connection ??= new HostClientConnectionAdapter(this);
            _server.Connect(_connection);
        }

        public void Disconnect()
        {
            _server.Disconnect(_clientId);
        }

        public void SendCreateWorld(CreateWorldRequest request)
        {
            var options = request.Options;
            options.ServiceBuilder ??= AbilityKit.Ability.World.Services.WorldServiceContainerFactory.CreateDefaultOnly();
            options.ServiceBuilder.RegisterInstance(new WorldInitData(request.OpCode, request.Payload));

            // Ensure SkillExecutor dependencies are resolvable in local/in-memory worlds.
            // Server worlds typically register IFrameTime via ServerFrameTimeModule.
            options.ServiceBuilder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            options.Modules.Add(new MobaWorldBootstrapModule());

            _server.CreateWorld(options);
        }

        public void SendJoin(JoinWorldRequest request)
        {
        }

        public void SendLeave(LeaveWorldRequest request)
        {
        }

        public void SendInput(SubmitInputRequest request)
        {
            if (_server.Features.TryGetFeature<IFrameSyncInputHub>(out var hub) && hub != null)
            {
                hub.SubmitInput(_clientId, request.WorldId, request.Input);
            }
        }

        public void OnMessage(ServerMessage message)
        {
            if (message is FrameMessage frame)
            {
                FramePushed?.Invoke(frame.Packet);
            }
        }
    }
}
