using System;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle.Transport
{
    public sealed class NullBattleLogicTransport : IBattleLogicTransport
    {
        public event Action<FramePacket> FramePushed;

        public void Connect()
        {
        }

        public void Disconnect()
        {
        }

        public void SendCreateWorld(CreateWorldRequest request)
        {
        }

        public void SendJoin(JoinWorldRequest request)
        {
        }

        public void SendLeave(LeaveWorldRequest request)
        {
        }

        public void SendInput(SubmitInputRequest request)
        {
        }

        public void PushFrame(FramePacket packet)
        {
            FramePushed?.Invoke(packet);
        }
    }
}
