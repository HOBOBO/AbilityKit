using System.Collections.Generic;
using System.Text;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>顺序节点：依次执行子节点，全部 Success 才 Success，任一 Failure 立即 Failure。</summary>
    public sealed class BtSequenceNode : BtCompositeNode
    {
        public override void OnStart(BtExecutionContext context)
        {
            RunningIndex = 0;
        }

        protected internal override bool CanExecute()
            => RunningIndex < ChildCount && State != BtNodeState.Failure;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            RunningIndex++;
            State = childState == BtNodeState.Success
                ? (RunningIndex >= ChildCount ? BtNodeState.Success : BtNodeState.Running)
                : childState;
        }

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = childIndex;
            State = BtNodeState.Running;
        }
    }

    /// <summary>选择节点：依次执行子节点，任一 Success 立即 Success，全部 Failure 才 Failure。</summary>
    public sealed class BtSelectorNode : BtCompositeNode
    {
        public override void OnStart(BtExecutionContext context)
        {
            RunningIndex = 0;
        }

        protected internal override bool CanExecute()
            => RunningIndex < ChildCount && State != BtNodeState.Success;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            State = childState == BtNodeState.Failure
                ? (++RunningIndex >= ChildCount ? BtNodeState.Failure : BtNodeState.Running)
                : childState;
        }

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = childIndex;
            State = BtNodeState.Running;
        }
    }

    /// <summary>
    /// 并行节点：所有子节点各占一个运行栈分支。
    /// RequireAll（默认）：任一 Failure 立即 Failure，全部完成才 Success；
    /// FirstSuccess：任一 Success 或 Failure 立即完成。
    /// </summary>
    public sealed class BtParallelNode : BtCompositeNode, IBtNodeStateful
    {
        public const string SuccessPolicyProperty = "successPolicy";

        private BtNodeState[] _childStates = System.Array.Empty<BtNodeState>();
        private bool _firstSuccess;

        protected override void OnCompositeInit(in BtNodeInitContext context)
        {
            _firstSuccess = context.Properties.GetInt64(SuccessPolicyProperty, 0) == 1;
            _childStates = new BtNodeState[context.ChildCount];
        }

        public override void OnStart(BtExecutionContext context)
        {
            RunningIndex = 0;
            for (var i = 0; i < _childStates.Length; i++) _childStates[i] = BtNodeState.Inactive;
        }

        protected internal override void OnChildStart()
        {
            _childStates[RunningIndex++] = BtNodeState.Running;
        }

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            _childStates[childIndex] = childState;
        }

        protected internal override bool CanExecute() => RunningIndex < ChildCount;

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = 0;
            for (var i = 0; i < _childStates.Length; i++) _childStates[i] = BtNodeState.Inactive;
        }

        protected internal override bool CanRunParallel() => true;

        protected internal override BtNodeState OverrideState(BtNodeState state)
        {
            var allComplete = true;
            for (var i = 0; i < _childStates.Length; i++)
            {
                if (_childStates[i] == BtNodeState.Running)
                {
                    allComplete = false;
                }
                else if (_childStates[i] == BtNodeState.Failure)
                {
                    return State = BtNodeState.Failure;
                }
                else if (_firstSuccess && _childStates[i] == BtNodeState.Success)
                {
                    return State = BtNodeState.Success;
                }
            }

            return State = allComplete ? BtNodeState.Success : BtNodeState.Running;
        }

        public string CaptureState()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < _childStates.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append((int)_childStates[i]);
            }
            return builder.ToString();
        }

        public void RestoreState(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                for (var i = 0; i < _childStates.Length; i++) _childStates[i] = BtNodeState.Inactive;
                return;
            }

            var parts = payload.Split(',');
            if (parts.Length != _childStates.Length)
                throw new System.InvalidOperationException("BT parallel snapshot child count mismatch.");
            for (var i = 0; i < parts.Length; i++) _childStates[i] = (BtNodeState)int.Parse(parts[i]);
        }
    }

    /// <summary>
    /// 随机选择节点：开始时对子节点洗牌（节点专属确定性随机流），按随机序执行选择语义。
    /// 修正了 BTCore RandomSelector 中途失败语义：失败后继续尝试下一个（选择语义），
    /// 全部失败才返回 Failure。
    /// </summary>
    public sealed class BtRandomSelectorNode : BtCompositeNode, IBtNodeStateful
    {
        private DeterministicRandom _random = null!;
        private readonly List<int> _pool = new();
        private readonly List<int> _order = new();   // 自底向顶：栈顶 = 末元素
        private int _childCount;

        protected override void OnCompositeInit(in BtNodeInitContext context)
        {
            _random = context.Random;
            _childCount = context.ChildCount;
        }

        public override void OnStart(BtExecutionContext context)
        {
            Shuffle();
        }

        protected internal override int CurrentChildIndex
            => _order.Count > 0 ? _order[_order.Count - 1] : 0;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            if (_order.Count > 0) _order.RemoveAt(_order.Count - 1);
            State = childState;
        }

        protected internal override bool CanExecute()
            => _order.Count > 0 && State != BtNodeState.Success;

        protected internal override void OnConditionalAbort(int childIndex)
        {
            Shuffle();
            State = BtNodeState.Running;
        }

        private void Shuffle()
        {
            _order.Clear();
            _pool.Clear();
            for (var i = 0; i < _childCount; i++) _pool.Add(i);
            for (var i = _childCount; i > 0; i--)
            {
                var j = _random.NextInt32(0, i);
                var index = _pool[j];
                _order.Add(index);
                _pool[j] = _pool[i - 1];
                _pool[i - 1] = index;
            }
        }

        public string CaptureState()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < _order.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(_order[i]);
            }
            return builder.ToString();
        }

        public void RestoreState(string payload)
        {
            _order.Clear();
            if (string.IsNullOrEmpty(payload)) return;
            foreach (var part in payload.Split(','))
            {
                _order.Add(int.Parse(part));
            }
        }
    }

    /// <summary>
    /// 随机顺序节点：开始时洗牌，按随机序执行顺序语义（任一子节点失败立即 Failure，
    /// 全部成功才 Success——修正 BTCore 实现中途中断语义缺失的问题）。
    /// </summary>
    public sealed class BtRandomSequenceNode : BtCompositeNode, IBtNodeStateful
    {
        private DeterministicRandom _random = null!;
        private readonly List<int> _pool = new();
        private readonly List<int> _order = new();
        private int _childCount;

        protected override void OnCompositeInit(in BtNodeInitContext context)
        {
            _random = context.Random;
            _childCount = context.ChildCount;
        }

        public override void OnStart(BtExecutionContext context)
        {
            Shuffle();
        }

        protected internal override int CurrentChildIndex
            => _order.Count > 0 ? _order[_order.Count - 1] : 0;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            if (_order.Count > 0) _order.RemoveAt(_order.Count - 1);
            State = childState == BtNodeState.Success
                ? (_order.Count == 0 ? BtNodeState.Success : BtNodeState.Running)
                : childState;
        }

        protected internal override bool CanExecute()
            => _order.Count > 0 && State != BtNodeState.Failure;

        protected internal override void OnConditionalAbort(int childIndex)
        {
            Shuffle();
            State = BtNodeState.Running;
        }

        private void Shuffle()
        {
            _order.Clear();
            _pool.Clear();
            for (var i = 0; i < _childCount; i++) _pool.Add(i);
            for (var i = _childCount; i > 0; i--)
            {
                var j = _random.NextInt32(0, i);
                var index = _pool[j];
                _order.Add(index);
                _pool[j] = _pool[i - 1];
                _pool[i - 1] = index;
            }
        }

        public string CaptureState()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < _order.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(_order[i]);
            }
            return builder.ToString();
        }

        public void RestoreState(string payload)
        {
            _order.Clear();
            if (string.IsNullOrEmpty(payload)) return;
            foreach (var part in payload.Split(','))
            {
                _order.Add(int.Parse(part));
            }
        }
    }
}
