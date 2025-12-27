using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class FinallyNode : IFlowNode
    {
        private readonly IFlowNode _try;
        private readonly IFlowNode _finally;

        private bool _tryEntered;
        private bool _finallyEntered;
        private FlowStatus _tryStatus;

        public FinallyNode(IFlowNode tryNode, IFlowNode finallyNode)
        {
            _try = tryNode ?? throw new ArgumentNullException(nameof(tryNode));
            _finally = finallyNode ?? throw new ArgumentNullException(nameof(finallyNode));
        }

        public void Enter(FlowContext ctx)
        {
            _tryEntered = false;
            _finallyEntered = false;
            _tryStatus = FlowStatus.Running;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_finallyEntered)
            {
                if (!_tryEntered)
                {
                    _try.Enter(ctx);
                    _tryEntered = true;
                }

                var s = _try.Tick(ctx, deltaTime);
                if (s == FlowStatus.Running) return FlowStatus.Running;

                _tryStatus = s;
                _try.Exit(ctx);
                _tryEntered = false;

                _finally.Enter(ctx);
                _finallyEntered = true;
            }

            var fs = _finally.Tick(ctx, deltaTime);
            if (fs == FlowStatus.Running) return FlowStatus.Running;

            _finally.Exit(ctx);
            _finallyEntered = false;
            return _tryStatus;
        }

        public void Exit(FlowContext ctx)
        {
            if (_tryEntered)
            {
                _try.Exit(ctx);
                _tryEntered = false;
            }
            if (_finallyEntered)
            {
                _finally.Exit(ctx);
                _finallyEntered = false;
            }
        }

        public void Interrupt(FlowContext ctx)
        {
            if (_tryEntered)
            {
                _try.Interrupt(ctx);
                _tryEntered = false;
            }
            if (_finallyEntered)
            {
                _finally.Interrupt(ctx);
                _finallyEntered = false;
            }
        }
    }
}
