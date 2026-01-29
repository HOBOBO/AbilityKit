using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Server
{
    public interface ILogicServerClient
    {
        ServerClientId ClientId { get; }

        void OnWorldCreated(WorldId worldId, string worldType);
        void OnWorldDestroyed(WorldId worldId);

        void OnPlayerJoined(WorldId worldId, PlayerId player);
        void OnPlayerLeft(WorldId worldId, PlayerId player);

        void OnFrame(FramePacket packet);
    }
}
