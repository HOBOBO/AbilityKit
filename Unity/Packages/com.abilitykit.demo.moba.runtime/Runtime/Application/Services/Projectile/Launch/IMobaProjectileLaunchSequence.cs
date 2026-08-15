using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public interface IMobaProjectileLaunchSequence
    {
        bool TryStart(in MobaProjectileLaunchContext context, out MobaProjectileLaunchResult result);
        bool IsComplete(in MobaProjectileLaunchResult result);
        void Stop(in MobaProjectileLaunchResult result, ContinuousEndReason reason);
    }
}
