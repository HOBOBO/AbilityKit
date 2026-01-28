namespace AbilityKit.Triggering.Runtime
{
    public interface ITrigger<TArgs, TCtx>
    {
        bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx);
        void Execute(in TArgs args, in ExecCtx<TCtx> ctx);
    }
}
