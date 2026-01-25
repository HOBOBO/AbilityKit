using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public interface IBattleLogicClient : IDisposable
    {
        event Action<FramePacket> FrameReceived;

        WorldId WorldId { get; }

        void Connect();
        void Disconnect();

        void CreateWorld(CreateWorldRequest request);
        void Join(JoinWorldRequest request);
        void Leave(LeaveWorldRequest request);
        void SubmitInput(SubmitInputRequest request);

        void Tick(float deltaTime);
    }
}
