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
}
