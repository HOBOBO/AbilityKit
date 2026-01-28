using System;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class CompiledTrigger<TArgs> : ITrigger<TArgs>
    {
        public readonly Func<TArgs, ExecCtx, bool> Predicate;
        public readonly Action<TArgs, ExecCtx> Actions;

        public CompiledTrigger(Func<TArgs, ExecCtx, bool> predicate, Action<TArgs, ExecCtx> actions)
        {
            Predicate = predicate;
            Actions = actions;
        }

        public bool Evaluate(in TArgs args, in ExecCtx ctx)
        {
            return Predicate == null || Predicate(args, ctx);
        }

        public void Execute(in TArgs args, in ExecCtx ctx)
        {
            Actions?.Invoke(args, ctx);
        }
    }
}
