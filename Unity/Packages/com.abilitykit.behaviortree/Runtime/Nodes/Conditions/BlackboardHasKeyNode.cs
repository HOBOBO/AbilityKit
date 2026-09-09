using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>黑板 key 存在性条件（用于检测可key 是否已写入）</summary>
    public class BlackboardHasKeyNode : ConditionNodeBase, ConditionDependencyProvider
    {
        public const string KeyProperty = "key";

        private string _key = "";

        // 当前黑板 Schema 在树实例生命周期内不可变，因此该条件无需重复评估。
        public IReadOnlyList<string> BlackboardDependencies => Array.Empty<string>();

        public override void OnInit(in NodeInitContext context)
        {
            _key = context.Properties.GetString(KeyProperty, "");
            if (string.IsNullOrEmpty(_key))
                throw new InvalidOperationException($"行为树节点 '{context.Definition.Id}'：检查黑板键时必须指定键名。");
        }

        protected override bool Validate(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
            => context.Blackboard.Schema.TryGetType(_key, out _);
    }
}
