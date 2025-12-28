using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class InMemoryBattleLogicTransport : IBattleLogicTransport, ILogicServerClient
    {
        private readonly ILogicWorldServer _server;
        private readonly ServerClientId _clientId;

        public InMemoryBattleLogicTransport(ILogicWorldServer server, string clientId = "in_memory")
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _clientId = new ServerClientId(clientId);
        }

        public event Action<FramePacket> FramePushed;

        ServerClientId ILogicServerClient.ClientId => _clientId;

        public void Connect()
        {
            _server.Connect(this);
        }

        public void Disconnect()
        {
            _server.Disconnect(_clientId);
        }

        public void SendCreateWorld(CreateWorldRequest request)
        {
            _server.CreateWorld(request.Options);
        }

        public void SendJoin(JoinWorldRequest request)
        {
            _server.JoinWorld(_clientId, request.WorldId, request.PlayerId);
        }

        public void SendLeave(LeaveWorldRequest request)
        {
            _server.LeaveWorld(_clientId, request.WorldId, request.PlayerId);
        }

        public void SendInput(SubmitInputRequest request)
        {
            _server.SubmitInput(_clientId, request.WorldId, request.Input);
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
            FramePushed?.Invoke(packet);
        }
    }
}
