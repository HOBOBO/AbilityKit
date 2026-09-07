using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.Deterministic;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Authoring
{
    public static class AuthoringGoldenExamples
    {
        public const string HeroCombatTreeId = "golden.hero_combat";

        public static AuthoringSourceDocument BuildHeroCombat()
        {
            var document = new AuthoringSourceDocument
            {
                Metadata = { Author = "golden", Description = "Golden example: reactive hero combat tree." },
                Tree = { TreeId = HeroCombatTreeId },
            };

            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "self.hasTarget", Type = ValueType.Bool, Default = PropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "self.canCast", Type = ValueType.Bool, Default = PropertyValue.Of(false) });
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "out.hold", Type = ValueType.Bool, Default = PropertyValue.Of(true) });

            Node(document, "root", BuiltInNodeTypes.Sequence, "perceive", "arbitrate");
            Node(document, "perceive", BuiltInNodeTypes.Sequence, "hasTarget", "canCast");
            Node(document, "hasTarget", BuiltInNodeTypes.BlackboardCompare);
            Node(document, "canCast", BuiltInNodeTypes.BlackboardCompare);
            Node(document, "arbitrate", BuiltInNodeTypes.Selector, "castWait", "hold");
            Node(document, "castWait", BuiltInNodeTypes.Wait);
            Node(document, "hold", BuiltInNodeTypes.SetBlackboard);
            document.Tree.RootNodeId = "root";

            Prop(document, "hasTarget", "leftKey", PropertyValue.Of("self.hasTarget"));
            Prop(document, "hasTarget", "op", PropertyValue.Of(0L));
            Prop(document, "hasTarget", "rightKind", PropertyValue.Of(0L));
            Prop(document, "hasTarget", "rightBool", PropertyValue.Of(true));

            Prop(document, "canCast", "leftKey", PropertyValue.Of("self.canCast"));
            Prop(document, "canCast", "op", PropertyValue.Of(0L));
            Prop(document, "canCast", "rightKind", PropertyValue.Of(0L));
            Prop(document, "canCast", "rightBool", PropertyValue.Of(true));

            Prop(document, "castWait", "durationSeconds", PropertyValue.Of(Fixed64.FromRatio(1, 2)));
            Prop(document, "hold", "key", PropertyValue.Of("out.hold"));
            Prop(document, "hold", "valueKind", PropertyValue.Of(0L));
            Prop(document, "hold", "constBool", PropertyValue.Of(true));

            SetLayout(document, ("root", 400f, 40f),
                ("perceive", 180f, 180f), ("arbitrate", 620f, 180f),
                ("hasTarget", 60f, 320f), ("canCast", 280f, 320f),
                ("castWait", 500f, 320f), ("hold", 800f, 320f));

            document.Groups.Add(new AuthoringGroupData
                { Id = "g1", Title = "感知", NodeIds = { "perceive", "hasTarget", "canCast" } });
            document.Groups.Add(new AuthoringGroupData
                { Id = "g2", Title = "意图仲裁", NodeIds = { "arbitrate", "castWait", "hold" } });

            return document;
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

        private static void SetLayout(
            AuthoringSourceDocument document,
            params (string Id, float X, float Y)[] entries)
        {
            foreach (var entry in entries)
                document.Layout.Add(new NodeLayoutData { NodeId = entry.Id, X = entry.X, Y = entry.Y });
        }

        public static List<AuthoringSourceDocument> BuildAll() => new() { BuildHeroCombat() };
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.AuthoringGoldenExamples.", false)]
    public static class BtAuthoringGoldenExamples
    {
#pragma warning disable CS0618
        public const string HeroCombatTreeId = AuthoringGoldenExamples.HeroCombatTreeId;

        public static BtAuthoringSourceDocument BuildHeroCombat()
            => AuthoringCompatibility.ToLegacy(AuthoringGoldenExamples.BuildHeroCombat());

        public static List<BtAuthoringSourceDocument> BuildAll()
        {
            var documents = AuthoringGoldenExamples.BuildAll();
            var result = new List<BtAuthoringSourceDocument>(documents.Count);
            foreach (var document in documents)
            {
                result.Add(AuthoringCompatibility.ToLegacy(document));
            }
            return result;
        }
#pragma warning restore CS0618
    }
}
