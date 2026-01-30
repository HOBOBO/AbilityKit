using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Transport;

namespace AbilityKit.Ability.Host.Legacy.Extensions.FrameSync
{
    public sealed class FrameMessage : ServerMessage
    {
        public readonly FramePacket Packet;

        public FrameMessage(FramePacket packet)
        {
            Packet = packet;
        }
    }
}
