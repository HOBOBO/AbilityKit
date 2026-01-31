using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicTransportClient : IBattleLogicClient
    {
        private readonly IBattleLogicTransport _transport;
        private WorldId _worldId;

        public BattleLogicTransportClient(IBattleLogicTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.FramePushed += OnFramePushed;
        }

        public event Action<FramePacket> FrameReceived;

        public WorldId WorldId => _worldId;

        public void Connect()
        {
            _transport.Connect();
        }

        public void Disconnect()
        {
            _transport.Disconnect();
        }

        public void CreateWorld(CreateWorldRequest request)
        {
            _worldId = request.Options.Id;
            _transport.SendCreateWorld(request);
        }

        public void Join(JoinWorldRequest request)
        {
            _worldId = request.WorldId;
            _transport.SendJoin(request);
        }

        public void Leave(LeaveWorldRequest request)
        {
            _worldId = request.WorldId;
            _transport.SendLeave(request);
        }

        public void SubmitInput(SubmitInputRequest request)
        {
            _worldId = request.WorldId;
            _transport.SendInput(request);
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            _transport.FramePushed -= OnFramePushed;
        }

        private void OnFramePushed(FramePacket packet)
        {
            FrameReceived?.Invoke(packet);
        }
    }
}
