using System.Collections.Generic;
using System.Text;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 随机选择节点：开始时对子节点洗牌（节点专属确定性随机流），按随机序执行选择语义
    /// 修正BTCore RandomSelector 中途失败语义：失败后继续尝试下一个（选择语义），
    /// 全部失败才返Failure
    /// </summary>
    public class RandomSelectorNode : CompositeNode, NodeStateful
    {
        private DeterministicRandom _random = null!;
        private readonly List<int> _pool = new();
        private readonly List<int> _order = new();
        private int _childCount;

        protected override void OnCompositeInit(in NodeInitContext context)
        {
            _random = context.Random;
            _childCount = context.ChildCount;
        }

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            Shuffle();
        }

        protected internal override int CurrentChildIndex
            => _order.Count > 0 ? _order[_order.Count - 1] : 0;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            if (_order.Count > 0) _order.RemoveAt(_order.Count - 1);
            State = childState;
        }

        protected internal override bool CanExecute()
            => _order.Count > 0 && State != NodeState.Success;

        protected internal override void OnConditionalAbort(int childIndex)
        {
            Shuffle();
            State = NodeState.Running;
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
