using System;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class CompiledTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>
    {
        public readonly Func<TArgs, ExecCtx<TCtx>, bool> Predicate;
        public readonly Action<TArgs, ExecCtx<TCtx>> Actions;

        public CompiledTrigger(Func<TArgs, ExecCtx<TCtx>, bool> predicate, Action<TArgs, ExecCtx<TCtx>> actions)
        {
            Predicate = predicate;
            Actions = actions;
        }

        public bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            return Predicate == null || Predicate(args, ctx);
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            Actions?.Invoke(args, ctx);
        }
    }
}
