using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Abstractions
{
    public interface IWorldContext
    {
        WorldId Id { get; }
        string WorldType { get; }
        IWorldServices Services { get; }
    }
}
