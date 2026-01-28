using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    public interface ITriggerObserver
    {
        void OnEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx ctx);
        void OnExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx ctx);
        void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ETriggerShortCircuitReason reason, in ExecCtx ctx);
    }
}
