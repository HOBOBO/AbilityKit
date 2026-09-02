using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>
    /// Golden 示例：以授权文档形式构建代表性树，供 dotnet 与 Unity EditMode 双侧验收
    /// 「授权 → 校验 → 导出」契约。任何一侧漂移都会在这里先红。
    /// </summary>
    public static class BtAuthoringGoldenExamples
    {
        public const string HeroCombatTreeId = "golden.hero_combat";

        /// <summary>
        /// 结构示意（呼应 MOBA 响应式决策）：
        /// Sequence[ Sequence(感知+条件), Selector[ CastWait, Hold ] ]
        /// </summary>
        public static BtAuthoringSourceDocument BuildHeroCombat()
        {
            var document = new BtAuthoringSourceDocument
            {
                Metadata = { Author = "golden", Description = "Golden example: reactive hero combat tree." },
                Tree = { TreeId = HeroCombatTreeId },
            };

            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "self.hasTarget", Type = BtValueType.Bool, Default = BtPropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "self.canCast", Type = BtValueType.Bool, Default = BtPropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                { Name = "out.hold", Type = BtValueType.Bool, Default = BtPropertyValue.Of(true) });

            Node(document, "root", BtBuiltInNodeTypes.Sequence, "perceive", "arbitrate");
            Node(document, "perceive", BtBuiltInNodeTypes.Sequence, "hasTarget", "canCast");
            Node(document, "hasTarget", BtBuiltInNodeTypes.BlackboardCompare);
            Node(document, "canCast", BtBuiltInNodeTypes.BlackboardCompare);
            Node(document, "arbitrate", BtBuiltInNodeTypes.Selector, "castWait", "hold");
            Node(document, "castWait", BtBuiltInNodeTypes.Wait);
            Node(document, "hold", BtBuiltInNodeTypes.SetBlackboard);
            document.Tree.RootNodeId = "root";

            Prop(document, "hasTarget", BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.hasTarget"));
            Prop(document, "hasTarget", BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));   // ==
            Prop(document, "hasTarget", BtBlackboardCompareNode.RightKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "hasTarget", BtBlackboardCompareNode.RightBoolProperty, BtPropertyValue.Of(true));

            Prop(document, "canCast", BtBlackboardCompareNode.LeftKeyProperty, BtPropertyValue.Of("self.canCast"));
            Prop(document, "canCast", BtBlackboardCompareNode.OpProperty, BtPropertyValue.Of(0L));
            Prop(document, "canCast", BtBlackboardCompareNode.RightKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "canCast", BtBlackboardCompareNode.RightBoolProperty, BtPropertyValue.Of(true));

            Prop(document, "castWait", BtWaitNode.DurationSecondsProperty, BtPropertyValue.Of(Fixed64.FromRatio(1, 2)));
            Prop(document, "hold", BtSetBlackboardNode.KeyProperty, BtPropertyValue.Of("out.hold"));
            Prop(document, "hold", BtSetBlackboardNode.ValueKindProperty, BtPropertyValue.Of(0L));
            Prop(document, "hold", BtSetBlackboardNode.ConstBoolProperty, BtPropertyValue.Of(true));

            // 布局（编辑态）：根节点居上，同层节点横向展开。
            SetLayout(document, ("root", 400f, 40f),
                ("perceive", 180f, 180f), ("arbitrate", 620f, 180f),
                ("hasTarget", 60f, 320f), ("canCast", 280f, 320f),
                ("castWait", 500f, 320f), ("hold", 800f, 320f));

            document.Groups.Add(new BtAuthoringGroupData
                { Id = "g1", Title = "感知", NodeIds = { "perceive", "hasTarget", "canCast" } });
            document.Groups.Add(new BtAuthoringGroupData
                { Id = "g2", Title = "意图仲裁", NodeIds = { "arbitrate", "castWait", "hold" } });

            return document;
        }

        private static void Node(BtAuthoringSourceDocument document, string id, string type, params string[] childIds)
        {
            var node = new BtNodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            document.Tree.Nodes.Add(node);
            document.NodeMetadata.Add(new BtAuthoringNodeMetadata { NodeId = id, DisplayName = id });
        }

        private static void Prop(BtAuthoringSourceDocument document, string nodeId, string key, BtPropertyValue value)
        {
            document.Tree.Nodes.Find(n => n.Id == nodeId)!.Properties.Set(key, value);
        }

        private static void SetLayout(
            BtAuthoringSourceDocument document,
            params (string Id, float X, float Y)[] entries)
        {
            foreach (var entry in entries)
                document.Layout.Add(new BtNodeLayoutData { NodeId = entry.Id, X = entry.X, Y = entry.Y });
        }

        /// <summary>全部 golden 文档。</summary>
        public static List<BtAuthoringSourceDocument> BuildAll() => new() { BuildHeroCombat() };
    }
}
