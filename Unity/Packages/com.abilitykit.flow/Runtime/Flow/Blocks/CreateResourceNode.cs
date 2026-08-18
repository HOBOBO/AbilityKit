using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class CreateResourceNode<T> : IFlowNode
    {
        private readonly Func<FlowContext, T> _create;
        private bool _created;

        public CreateResourceNode(Func<FlowContext, T> create)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public void Enter(FlowContext ctx)
        {
            // 每次运行都重新创建并写入：_created 是防 Tick 内重复的守卫，
            // 不能跨运行存留（同节点实例第二次运行必须重新 Set）。
            var value = _create(ctx);
            ctx.Set(value);
            _created = true;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
        }

        public void Interrupt(FlowContext ctx)
        {
        }
    }
}
