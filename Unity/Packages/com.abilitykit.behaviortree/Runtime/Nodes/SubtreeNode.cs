using System;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 子树引用节点：通过 treeId 引用另一棵树，加载期<see cref="BtTreeCompiler"/> 内联展开
    /// （id 前缀 + 黑板合并），运行时不会直接执行本节点（展开后不再存在）    /// 若未提供定义解析器导致本节点残留，则按失败处理并报错    /// </summary>
    public class SubtreeNode : NodeBase
    {
        public const string TreeIdProperty = "treeId";

        private string _treeId = "";

        public override void OnInit(in NodeInitContext context)
        {
            _treeId = context.Properties.GetString(TreeIdProperty, "");
        }

        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            throw new InvalidOperationException(
                $"Subtree node '{NodeId}' (treeId='{_treeId}') reached runtime: " +
                "it must be expanded by BtTreeCompiler before execution.");
        }
    }
}
