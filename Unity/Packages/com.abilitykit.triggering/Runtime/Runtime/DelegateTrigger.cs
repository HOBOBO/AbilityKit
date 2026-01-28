using System;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class DelegateTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>
    {
        private readonly Func<TArgs, ExecCtx<TCtx>, bool> _predicate;
        private readonly Action<TArgs, ExecCtx<TCtx>> _actions;

        public DelegateTrigger(Func<TArgs, ExecCtx<TCtx>, bool> predicate, Action<TArgs, ExecCtx<TCtx>> actions)
        {
            _predicate = predicate;
            _actions = actions;
        }

        public bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            return _predicate == null || _predicate(args, ctx);
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            _actions?.Invoke(args, ctx);
        }
    }
}
