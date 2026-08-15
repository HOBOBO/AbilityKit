using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public interface IMobaProjectileLaunchExecutor
    {
        bool TryStartLaunch(in MobaProjectileLaunchRequest request, out MobaProjectileLaunchResult result);
        bool IsLaunchComplete(in MobaProjectileLaunchResult result);
        void StopLaunch(in MobaProjectileLaunchResult result, ContinuousEndReason reason);
    }
}
