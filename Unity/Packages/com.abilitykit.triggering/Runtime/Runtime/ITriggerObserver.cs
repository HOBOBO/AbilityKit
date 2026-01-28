using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    public interface ITriggerObserver<TCtx>
    {
        void OnEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx<TCtx> ctx);
        void OnExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx<TCtx> ctx);
        void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ETriggerShortCircuitReason reason, in ExecCtx<TCtx> ctx);
    }
}
