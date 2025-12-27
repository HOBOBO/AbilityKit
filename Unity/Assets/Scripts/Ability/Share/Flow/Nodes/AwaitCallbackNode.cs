using System;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class AwaitCallbackNode : IFlowNode
    {
        public delegate IDisposable SubscribeDelegate(FlowContext ctx, Action<bool> complete);

        private readonly SubscribeDelegate _subscribe;
        private IDisposable _subscription;
        private FlowWakeUp _wakeUp;
        private bool _completed;
        private bool _succeeded;

        public AwaitCallbackNode(SubscribeDelegate subscribe)
        {
            _subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
        }

        public void Enter(FlowContext ctx)
        {
            _completed = false;
            _succeeded = false;
            _wakeUp = ctx.Get<FlowWakeUp>();
            _subscription = _subscribe(ctx, Complete);
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_completed) return FlowStatus.Running;
            return _succeeded ? FlowStatus.Succeeded : FlowStatus.Failed;
        }

        public void Exit(FlowContext ctx)
        {
            _subscription?.Dispose();
            _subscription = null;
            _wakeUp = null;
        }

        public void Interrupt(FlowContext ctx)
        {
            _subscription?.Dispose();
            _subscription = null;
            _wakeUp = null;
        }

        private void Complete(bool succeeded)
        {
            _completed = true;
            _succeeded = succeeded;

            // Trigger an immediate pump to advance parent nodes without needing continuous Step calls.
            // Caller must ensure Complete is invoked on the same thread as FlowRunner usage.
            _wakeUp?.Wake();
        }
    }
}
