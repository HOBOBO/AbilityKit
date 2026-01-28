using System;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class DelegateTrigger<TArgs> : ITrigger<TArgs>
    {
        private readonly Func<TArgs, ExecCtx, bool> _predicate;
        private readonly Action<TArgs, ExecCtx> _actions;

        public DelegateTrigger(Func<TArgs, ExecCtx, bool> predicate, Action<TArgs, ExecCtx> actions)
        {
            _predicate = predicate;
            _actions = actions;
        }

        public bool Evaluate(in TArgs args, in ExecCtx ctx)
        {
            return _predicate == null || _predicate(args, ctx);
        }

        public void Execute(in TArgs args, in ExecCtx ctx)
        {
            _actions?.Invoke(args, ctx);
        }
    }
}
