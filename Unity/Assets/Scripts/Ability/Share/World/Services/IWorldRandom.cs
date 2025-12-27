namespace AbilityKit.Ability.World.Services
{
    public interface IWorldRandom
    {
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat01();
    }
}
