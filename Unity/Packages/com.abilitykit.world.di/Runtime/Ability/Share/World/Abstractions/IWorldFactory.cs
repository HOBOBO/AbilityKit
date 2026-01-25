namespace AbilityKit.Ability.World.Abstractions
{
    public interface IWorldFactory
    {
        IWorld Create(WorldCreateOptions options);
    }
}
