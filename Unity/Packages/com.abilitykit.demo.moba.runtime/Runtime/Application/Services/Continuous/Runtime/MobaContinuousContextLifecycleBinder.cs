using AbilityKit.Continuous;
using AbilityKit.Core.Logging;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousContextLifecycleBinder : IContinuousLifecycleBinder
    {
        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            if (continuous is not IMobaContinuousExecutionContextProvider provider) return;
            if (provider.TryGetCombatExecutionContext(out var context) && context.HasExecutionSource) return;

            Log.Warning($"[MobaContinuousContextLifecycle] continuous activated without execution context. type={continuous.GetType().FullName}");
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
        }

    }
}
