using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>
    /// 快速创建模板（纯 C# 构造器）。模板只使用 builtin.* 节点，保证对任意注册中心可校验；
    /// 与 golden 共用实现——模板即"永远新鲜的 golden"，任何一侧漂移都会在测试里先红。
    /// </summary>
    public static class BtAuthoringTemplates
    {
        public const string EmptyId = "空树（Succeed 根）";
        public const string ReactiveLoopId = "反应式决策骨架";
        public const string GoldenHeroCombatId = "Golden：英雄战斗示例";

        /// <summary>模板目录（向导下拉用），显示名 -> 构造器。</summary>
        public static List<(string DisplayName, Func<BtAuthoringSourceDocument> Build)> Catalog() => new()
        {
            (EmptyId, BuildEmpty),
            (ReactiveLoopId, BuildReactiveLoop),
            (GoldenHeroCombatId, BtAuthoringGoldenExamples.BuildHeroCombat),
        };

        /// <summary>空树：单个 Succeed 根（组合节点要求至少一个子节点，从叶子根起步替换更顺）。</summary>
        public static BtAuthoringSourceDocument BuildEmpty()
        {
            var document = new BtAuthoringSourceDocument
            {
                Metadata = { Description = "Empty tree: single succeed root." },
            };
            Node(document, "root", BtBuiltInNodeTypes.Succeed);
            document.Tree.RootNodeId = "root";
            document.Layout.Add(new BtNodeLayoutData { NodeId = "root", X = 400, Y = 60 });
            return document;
        }

        /// <summary>
        /// 反应式决策骨架（MOBA 响应式语义的通用版）：
        /// Sequence[ 感知条件（黑板比较）, Selector[ 行为分支(条件+动作), Hold(写黑板) ] ]。
        /// 帧事实在 self.* 命名空间刷新；动作每帧重发意图。
        /// </summary>
        public static BtAuthoringSourceDocument BuildReactiveLoop()
        {
            var document = new BtAuthoringSourceDocument
            {
                Metadata = { Description = "Reactive loop skeleton: perceive, arbitrate, act or hold." },
            };

            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "self.hasTarget", Type = BtValueType.Bool, Default = BtPropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "self.canAct", Type = BtValueType.Bool, Default = BtPropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "out.hold", Type = BtValueType.Bool, Default = BtPropertyValue.Of(true) });

            Node(document, "root", BtBuiltInNodeTypes.Sequence, "perceive", "arbitrate");
            Node(document, "perceive", BtBuiltInNodeTypes.Sequence, "hasTarget", "canAct");
            Node(document, "arbitrate", BtBuiltInNodeTypes.Selector, "actBranch", "hold");
            Node(document, "actBranch", BtBuiltInNodeTypes.Sequence, "actReady", "act");
            Node(document, "hold", BtBuiltInNodeTypes.SetBlackboard);
            Node(document, "act", BtBuiltInNodeTypes.Wait);
            Node(document, "hasTarget", BtBuiltInNodeTypes.BlackboardCompare);
            Node(document, "canAct", BtBuiltInNodeTypes.BlackboardCompare);
            Node(document, "actReady", BtBuiltInNodeTypes.BlackboardCompare);
            document.Tree.RootNodeId = "root";

            Prop(document, "hasTarget", BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.hasTarget"));
            Prop(document, "hasTarget", BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));
            Prop(document, "hasTarget", BtBlackboardCompareNode.RightKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "hasTarget", BtBlackboardCompareNode.RightBoolProperty, BtPropertyValue.Of(true));

            Prop(document, "canAct", BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.canAct"));
            Prop(document, "canAct", BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));
            Prop(document, "canAct", BtBlackboardCompareNode.RightKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "canAct", BtBlackboardCompareNode.RightBoolProperty, BtPropertyValue.Of(true));

            Prop(document, "actReady", BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.canAct"));
            Prop(document, "actReady", BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));
            Prop(document, "actReady", BtBlackboardCompareNode.RightKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "actReady", BtBlackboardCompareNode.RightBoolProperty, BtPropertyValue.Of(true));

            Prop(document, "act", BtWaitNode.DurationSecondsProperty, BtPropertyValue.Of(Fixed64.FromRatio(1, 2)));
            Prop(document, "hold", BtSetBlackboardNode.KeyProperty, BtPropertyValue.Of("out.hold"));
            Prop(document, "hold", BtSetBlackboardNode.ValueKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "hold", BtSetBlackboardNode.ConstBoolProperty, BtPropertyValue.Of(true));

            // 深度列布局
            var y = 0f;
            foreach (var node in document.Tree.Nodes)
            {
                document.Layout.Add(new BtNodeLayoutData { NodeId = node.Id, X = 0f, Y = y });
                y += 120f;
            }
            return document;
        }

        private static void Node(BtAuthoringSourceDocument document, string id, string type, params string[] childIds)
        {
            var node = new BtNodeDefinition { Id = id, Type = type, Name = id };
            node.ChildIds.AddRange(childIds);
            document.Tree.Nodes.Add(node);
        }

        private static void Prop(BtAuthoringSourceDocument document, string nodeId, string key, BtPropertyValue value)
        {
            document.Tree.Nodes.Find(n => n.Id == nodeId)!.Properties.Set(key, value);
        }
    }
}
