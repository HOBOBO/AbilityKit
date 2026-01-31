using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Drivers
{
    public sealed class DefaultWorldServerDriverSelector : IWorldServerDriverSelector
    {
        public bool UseFrameSyncDriver(IWorld world)
        {
            if (world == null) return false;
            if (world.Services == null) return false;

            return world.Services.TryResolve<IWorldInputSink>(out var sink) && sink != null;
        }
    }
}
