using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Legacy.Transport
{
    public sealed class LegacyPlayerJoinedMessage : ServerMessage
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;

        public LegacyPlayerJoinedMessage(WorldId worldId, PlayerId playerId)
        {
            WorldId = worldId;
            PlayerId = playerId;
        }
    }

    public sealed class LegacyPlayerLeftMessage : ServerMessage
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;

        public LegacyPlayerLeftMessage(WorldId worldId, PlayerId playerId)
        {
            WorldId = worldId;
            PlayerId = playerId;
        }
    }

    public sealed class LegacyFrameMessage : ServerMessage
    {
        public readonly FramePacket Packet;

        public LegacyFrameMessage(FramePacket packet)
        {
            Packet = packet;
        }
    }
}
