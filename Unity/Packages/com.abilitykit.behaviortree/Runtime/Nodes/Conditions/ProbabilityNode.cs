using System;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>使用确定性随机源按百分比判定条件是否成功</summary>
    public class ProbabilityNode : ConditionNodeBase
    {
        public const string PercentProperty = "percent";

        private DeterministicRandom _random = null!;
        private long _percent = 50;

        public override void OnInit(in NodeInitContext context)
        {
            _random = context.Random;
            _percent = context.Properties.GetInt64(PercentProperty, 50);
            if (_percent is < 0 or > 100)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': percent must be within [0,100].");
        }

        protected override bool Validate(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
            => _random.NextInt32(0, 100) < (int)_percent;
    }
}
