using AbilityKit.Deterministic;

namespace AbilityKit.Ability.World.Services
{
    public interface IWorldRandom : IDeterministicRandomStream
    {
        int NextInt(int minInclusive, int maxExclusive);
    }
}
