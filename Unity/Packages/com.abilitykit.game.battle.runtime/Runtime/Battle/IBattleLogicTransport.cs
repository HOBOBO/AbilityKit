using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public interface IBattleLogicTransport
    {
        event Action<FramePacket> FramePushed;

        void Connect();
        void Disconnect();

        void SendCreateWorld(CreateWorldRequest request);
        void SendJoin(JoinWorldRequest request);
        void SendLeave(LeaveWorldRequest request);
        void SendInput(SubmitInputRequest request);
    }
}
