using System;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Continuous;
using AbilityKit.Core.Logging;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousContextLifecycleBinder : IContinuousLifecycleBinder
    {
        private readonly MobaTraceRegistry _trace;
        private readonly ITriggerActionRunner _actionRunner;

        public MobaContinuousContextLifecycleBinder(MobaTraceRegistry trace, ITriggerActionRunner actionRunner)
        {
            _trace = trace;
            _actionRunner = actionRunner;
        }

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
            EndExecutionContext(continuous, reason);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
        }

        private void EndExecutionContext(IContinuous continuous, ContinuousEndReason reason)
        {
            if (continuous is not IMobaContinuousExecutionContextProvider provider) return;
            if (!provider.TryGetCombatExecutionContext(out var context) || !context.IsValid) return;

            // ParentContextId is a borrowed attachment point used to create downstream
            // execution nodes. The Continuous runtime does not own that node and must not end it.
            var ownerKey = context.OwnerContextId;
            if (ownerKey == 0) return;

            try
            {
                _actionRunner?.CancelByOwnerKey(ownerKey);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaContinuousContextLifecycle] CancelByOwnerKey exception (ownerKey={ownerKey})");
            }
        }

    }
}
