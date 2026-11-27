using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 为子节点在当前上下文层建立资源槽，收尾时先清理子节点，再尝试释放资源。
    /// </summary>
    public sealed class UsingResourceNode<T> : IFlowNode
    {
        private readonly Func<FlowContext, T> _create;
        private readonly Action<T> _dispose;
        private readonly IFlowNode _body;

        private T _value = default!;
        private bool _created;
        private bool _bodyEntered;

        /// <summary>
        /// 绑定资源创建、可选释放委托和需要使用资源的子节点。
        /// </summary>
        public UsingResourceNode(Func<FlowContext, T> create, Action<T> dispose, IFlowNode body)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _dispose = dispose;
        }

        /// <summary>
        /// 创建资源并写入当前上下文层；此时不进入子节点。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            _value = _create(ctx);
            _created = true;
            ctx.Set(_value);

            _bodyEntered = false;
        }

        /// <summary>
        /// 首次推进时进入子节点，其后返回子节点的推进结果。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_bodyEntered)
            {
                FlowDiagnostics.RecordEnter(ctx, _body);
                _bodyEntered = true;
                _body.Enter(ctx);
            }

            var result = FlowDiagnostics.Tick(ctx, _body, deltaTime);
            if (result == FlowStatus.Running) return FlowStatus.Running;

            _bodyEntered = false;
            FlowDiagnostics.Exit(ctx, _body, result);
            return result;
        }

        /// <summary>
        /// 退出仍在运行的子节点，并移除槽位和释放资源。
        /// </summary>
        public void Exit(FlowContext ctx) => Close(ctx, interrupt: false);

        /// <summary>
        /// 中断仍在运行的子节点，并移除槽位和释放资源。
        /// </summary>
        public void Interrupt(FlowContext ctx) => Close(ctx, interrupt: true);

        private void Close(FlowContext ctx, bool interrupt)
        {
            var cleanup = new FlowCleanup();
            if (_bodyEntered)
            {
                _bodyEntered = false;
                cleanup.CloseNode(ctx, _body, interrupt, interrupt ? FlowStatus.Canceled : FlowStatus.Succeeded);
            }

            try
            {
                ReleaseResource(ctx);
            }
            catch (Exception exception)
            {
                cleanup.AddError(exception);
            }
            cleanup.ThrowIfFailed();
        }

        private void ReleaseResource(FlowContext ctx)
        {
            if (!_created) return;
            var value = _value;
            _value = default!;
            _created = false;
            ctx.Remove<T>();
            _dispose?.Invoke(value);
        }
    }
}
