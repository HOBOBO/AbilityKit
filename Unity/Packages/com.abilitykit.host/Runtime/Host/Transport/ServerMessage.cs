using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Transport
{
    public abstract class ServerMessage
    {
    }

    public sealed class WorldCreatedMessage : ServerMessage
    {
        public readonly WorldId WorldId;
        public readonly string WorldType;

        public WorldCreatedMessage(WorldId worldId, string worldType)
        {
            WorldId = worldId;
            WorldType = worldType;
        }
    }

    public sealed class WorldDestroyedMessage : ServerMessage
    {
        public readonly WorldId WorldId;

        public WorldDestroyedMessage(WorldId worldId)
        {
            WorldId = worldId;
        }
    }

    public sealed class PlayerJoinedMessage : ServerMessage
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;

        public PlayerJoinedMessage(WorldId worldId, PlayerId playerId)
        {
            WorldId = worldId;
            PlayerId = playerId;
        }
    }

    public sealed class PlayerLeftMessage : ServerMessage
    {
        public readonly WorldId WorldId;
        public readonly PlayerId PlayerId;

        public PlayerLeftMessage(WorldId worldId, PlayerId playerId)
        {
            WorldId = worldId;
            PlayerId = playerId;
        }
    }

    public sealed class FrameMessage : ServerMessage
    {
        public readonly FramePacket Packet;

        public FrameMessage(FramePacket packet)
        {
            Packet = packet;
        }
    }
}
