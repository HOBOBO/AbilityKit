using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.WorldStart
{
    public interface IWorldAutoStartHandler : IService
    {
        bool TryAutoStart(IWorld world, float deltaTime);
    }
}
