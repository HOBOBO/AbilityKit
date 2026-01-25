using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Math
{
    public interface ICollisionService : IService
    {
        ICollisionWorld World { get; }
    }
}
