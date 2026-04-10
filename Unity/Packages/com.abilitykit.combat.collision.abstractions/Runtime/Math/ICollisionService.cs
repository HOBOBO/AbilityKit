using AbilityKit.Ability.World.Services;

namespace AbilityKit.Core.Math
{
    public interface ICollisionService : IService
    {
        ICollisionWorld World { get; }
    }
}
