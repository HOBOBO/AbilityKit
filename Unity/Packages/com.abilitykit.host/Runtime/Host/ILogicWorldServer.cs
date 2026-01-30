using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host
{
    public interface ILogicWorldServer
    {
        IWorldManager Worlds { get; }

        void Connect(ILogicServerClient client);
        void Disconnect(ServerClientId clientId);

        IWorld CreateWorld(WorldCreateOptions options);
        bool DestroyWorld(WorldId id);

        bool JoinWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId);
        bool LeaveWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId);

        bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input);

        void Tick(float deltaTime);
    }
}
