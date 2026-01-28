namespace AbilityKit.Triggering.Runtime
{
    public interface ITrigger<TArgs>
    {
        bool Evaluate(in TArgs args, in ExecCtx ctx);
        void Execute(in TArgs args, in ExecCtx ctx);
    }
}
