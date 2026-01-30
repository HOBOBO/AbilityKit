using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.ECS
{
    public interface IEcsWorld
    {
        IWorldResolver Services { get; }

        bool Exists(EcsEntityId id);

        IUnitResolver Units { get; }
    }
}
