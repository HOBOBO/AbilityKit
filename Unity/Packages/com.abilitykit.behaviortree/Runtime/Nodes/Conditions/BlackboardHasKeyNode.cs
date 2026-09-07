using System;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>黑板 key 存在性条件（用于检测可key 是否已写入）</summary>
    public class BlackboardHasKeyNode : ConditionNodeBase
    {
        public const string KeyProperty = "key";

        private string _key = "";

        public override void OnInit(in NodeInitContext context)
        {
            _key = context.Properties.GetString(KeyProperty, "");
            if (string.IsNullOrEmpty(_key))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': hasKey requires key.");
        }

        protected override bool Validate(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
            => context.Blackboard.Schema.TryGetType(_key, out _);
    }
}
