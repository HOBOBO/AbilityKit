using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.Deterministic;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Authoring
{
    public static class AuthoringTemplates
    {
        public const string EmptyId = "空树（Succeed 根）";
        public const string ReactiveLoopId = "反应式决策骨架";
        public const string GoldenHeroCombatId = "Golden：英雄战斗示例";

        public static List<(string DisplayName, Func<AuthoringSourceDocument> Build)> Catalog() => new()
        {
            (EmptyId, BuildEmpty),
            (ReactiveLoopId, BuildReactiveLoop),
            (GoldenHeroCombatId, AuthoringGoldenExamples.BuildHeroCombat),
        };

        public static AuthoringSourceDocument BuildEmpty()
        {
            var document = new AuthoringSourceDocument
            {
                Metadata = { Description = "Empty tree: single succeed root." },
            };
            Node(document, "root", BuiltInNodeTypes.Succeed);
            document.Tree.RootNodeId = "root";
            document.Layout.Add(new NodeLayoutData { NodeId = "root", X = 400, Y = 60 });
            return document;
        }

        public static AuthoringSourceDocument BuildReactiveLoop()
        {
            var document = new AuthoringSourceDocument
            {
                Metadata = { Description = "Reactive loop skeleton: perceive, arbitrate, act or hold." },
            };

            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "self.hasTarget", Type = ValueType.Bool, Default = PropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "self.canAct", Type = ValueType.Bool, Default = PropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "out.hold", Type = ValueType.Bool, Default = PropertyValue.Of(true) });

            Node(document, "root", BuiltInNodeTypes.Sequence, "perceive", "arbitrate");
            Node(document, "perceive", BuiltInNodeTypes.Sequence, "hasTarget", "canAct");
            Node(document, "arbitrate", BuiltInNodeTypes.Selector, "actBranch", "hold");
            Node(document, "actBranch", BuiltInNodeTypes.Sequence, "actReady", "act");
            Node(document, "hold", BuiltInNodeTypes.SetBlackboard);
            Node(document, "act", BuiltInNodeTypes.Wait);
            Node(document, "hasTarget", BuiltInNodeTypes.BlackboardCompare);
            Node(document, "canAct", BuiltInNodeTypes.BlackboardCompare);
            Node(document, "actReady", BuiltInNodeTypes.BlackboardCompare);
            document.Tree.RootNodeId = "root";

            Prop(document, "hasTarget", "leftKey", PropertyValue.Of("self.hasTarget"));
            Prop(document, "hasTarget", "op", PropertyValue.Of(0L));
            Prop(document, "hasTarget", "rightKind", PropertyValue.Of(0L));
            Prop(document, "hasTarget", "rightBool", PropertyValue.Of(true));

            Prop(document, "canAct", "leftKey", PropertyValue.Of("self.canAct"));
            Prop(document, "canAct", "op", PropertyValue.Of(0L));
            Prop(document, "canAct", "rightKind", PropertyValue.Of(0L));
            Prop(document, "canAct", "rightBool", PropertyValue.Of(true));

            Prop(document, "actReady", "leftKey", PropertyValue.Of("self.canAct"));
            Prop(document, "actReady", "op", PropertyValue.Of(0L));
            Prop(document, "actReady", "rightKind", PropertyValue.Of(0L));
            Prop(document, "actReady", "rightBool", PropertyValue.Of(true));

            Prop(document, "act", "durationSeconds", PropertyValue.Of(Fixed64.FromRatio(1, 2)));
            Prop(document, "hold", "key", PropertyValue.Of("out.hold"));
            Prop(document, "hold", "valueKind", PropertyValue.Of(0L));
            Prop(document, "hold", "constBool", PropertyValue.Of(true));

            SetLayout(document, ("root", 400f, 40f),
                ("perceive", 180f, 180f), ("arbitrate", 620f, 180f),
                ("hasTarget", 60f, 320f), ("canAct", 280f, 320f),
                ("actBranch", 500f, 320f), ("hold", 800f, 320f),
                ("actReady", 500f, 460f), ("act", 720f, 460f));
            return document;
        }

        private static void SetLayout(
            AuthoringSourceDocument document,
            params (string Id, float X, float Y)[] entries)
        {
            foreach (var entry in entries)
                document.Layout.Add(new NodeLayoutData { NodeId = entry.Id, X = entry.X, Y = entry.Y });
        }

        private static void Node(AuthoringSourceDocument document, string id, string type, params string[] childIds)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            document.Tree.Nodes.Add(node);
            document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = id, DisplayName = id });
        }

        private static void Prop(AuthoringSourceDocument document, string nodeId, string key, PropertyValue value)
        {
            document.Tree.Nodes.Find(n => n.Id == nodeId)!.Properties.Set(key, value);
        }
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.AuthoringTemplates.", false)]
    public static class BtAuthoringTemplates
    {
#pragma warning disable CS0618
        public const string EmptyId = AuthoringTemplates.EmptyId;
        public const string ReactiveLoopId = AuthoringTemplates.ReactiveLoopId;
        public const string GoldenHeroCombatId = AuthoringTemplates.GoldenHeroCombatId;

        public static List<(string DisplayName, Func<BtAuthoringSourceDocument> Build)> Catalog() => new()
        {
            (EmptyId, BuildEmpty),
            (ReactiveLoopId, BuildReactiveLoop),
            (GoldenHeroCombatId, BtAuthoringGoldenExamples.BuildHeroCombat),
        };

        public static BtAuthoringSourceDocument BuildEmpty()
            => AuthoringCompatibility.ToLegacy(AuthoringTemplates.BuildEmpty());

        public static BtAuthoringSourceDocument BuildReactiveLoop()
            => AuthoringCompatibility.ToLegacy(AuthoringTemplates.BuildReactiveLoop());
#pragma warning restore CS0618
    }
}
