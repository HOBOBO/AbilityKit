using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Drivers
{
    public interface IWorldServerDriverSelector
    {
        bool UseFrameSyncDriver(IWorld world);
    }
}
