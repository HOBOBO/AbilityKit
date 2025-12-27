using System;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class WaitSecondsNode : IFlowNode
    {
        private readonly float _seconds;
        private float _elapsed;

        public WaitSecondsNode(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
        }

        public void Enter(FlowContext ctx)
        {
            _elapsed = 0f;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            _elapsed += deltaTime;
            return _elapsed >= _seconds ? FlowStatus.Succeeded : FlowStatus.Running;
        }

        public void Exit(FlowContext ctx)
        {
        }

        public void Interrupt(FlowContext ctx)
        {
        }
    }
}
