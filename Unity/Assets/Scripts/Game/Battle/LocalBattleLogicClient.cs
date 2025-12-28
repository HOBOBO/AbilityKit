using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class LocalBattleLogicClient : IBattleLogicClient, ILogicServerClient
    {
        private readonly ILogicWorldServer _server;
        private readonly ServerClientId _clientId;
        private WorldId _worldId;

        public LocalBattleLogicClient(ILogicWorldServer server, string clientId = "local")
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _clientId = new ServerClientId(clientId);
        }

        public event Action<FramePacket> FrameReceived;

        public WorldId WorldId => _worldId;

        ServerClientId ILogicServerClient.ClientId => _clientId;

        public void Connect()
        {
            _server.Connect(this);
        }

        public void Disconnect()
        {
            _server.Disconnect(_clientId);
        }

        public void CreateWorld(CreateWorldRequest request)
        {
            _worldId = request.Options.Id;
            _server.CreateWorld(request.Options);
        }

        public void Join(JoinWorldRequest request)
        {
            _server.JoinWorld(_clientId, request.WorldId, request.PlayerId);
        }

        public void Leave(LeaveWorldRequest request)
        {
            _server.LeaveWorld(_clientId, request.WorldId, request.PlayerId);
        }

        public void SubmitInput(SubmitInputRequest request)
        {
            _server.SubmitInput(_clientId, request.WorldId, request.Input);
        }

        public void Tick(float deltaTime)
        {
            _server.Tick(deltaTime);
        }

        public void Dispose()
        {
            Disconnect();
        }

        void ILogicServerClient.OnWorldCreated(WorldId worldId, string worldType)
        {
        }

        void ILogicServerClient.OnWorldDestroyed(WorldId worldId)
        {
        }

        void ILogicServerClient.OnPlayerJoined(WorldId worldId, PlayerId player)
        {
        }

        void ILogicServerClient.OnPlayerLeft(WorldId worldId, PlayerId player)
        {
        }

        void ILogicServerClient.OnFrame(FramePacket packet)
        {
            FrameReceived?.Invoke(packet);
        }
    }
}
