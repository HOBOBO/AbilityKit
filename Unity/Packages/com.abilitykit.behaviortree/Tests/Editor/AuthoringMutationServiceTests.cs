#if UNITY_EDITOR
#nullable enable

using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEngine;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringMutationServiceTests
    {
        [Test]
        public void PasteSubgraph_RemapsNodesEdgesLayoutMetadataGroupsAndNotes()
        {
            var document = CreateDocument();
            var serialized = AuthoringMutationService.SerializeSubgraph(
                document,
                new[] { "root", "child" },
                new[] { "group" },
                new[] { "note" });

            Assert.That(AuthoringMutationService.TryDeserializeSubgraph(serialized, out var clipboard), Is.True);
            var result = AuthoringMutationService.PasteSubgraph(document, clipboard, new Vector2(1000f, 2000f));

            Assert.That(result.Changed, Is.True);
            Assert.That(result.CreatedNodeIds, Has.Count.EqualTo(2));
            Assert.That(result.NodeIdMap["root"], Is.Not.EqualTo("root"));
            Assert.That(result.NodeIdMap["child"], Is.Not.EqualTo("child"));

            var pastedRoot = document.Tree.Nodes.Find(node => node.Id == result.NodeIdMap["root"]);
            Assert.That(pastedRoot, Is.Not.Null);
            Assert.That(pastedRoot!.ChildIds, Is.EqualTo(new[] { result.NodeIdMap["child"] }));
            Assert.That(pastedRoot.ChildIds, Does.Not.Contain("external"));

            var layout = document.Layout.Find(item => item.NodeId == result.NodeIdMap["root"]);
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout!.X, Is.EqualTo(1000f));
            Assert.That(layout.Y, Is.EqualTo(2000f));

            Assert.That(document.TryGetNodeMetadata(result.NodeIdMap["child"], out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("Child"));
            var group = document.Groups.Find(item => item.Id == result.CreatedGroupIds[0]);
            Assert.That(group!.NodeIds, Is.EqualTo(new[] { result.NodeIdMap["root"], result.NodeIdMap["child"] }));
            var note = document.Notes.Find(item => item.Id == result.CreatedNoteIds[0]);
            Assert.That(note!.Text, Is.EqualTo("note text"));
        }

        [Test]
        public void DeleteSelection_ReportsImpactAndRemovesOwnedModelData()
        {
            var document = CreateDocument();
            var impact = AuthoringMutationService.AnalyzeDelete(
                document,
                new[] { "root" },
                null,
                null);

            Assert.That(impact.DeletesRoot, Is.True);
            Assert.That(impact.DetachedChildNodeIds, Is.EquivalentTo(new[] { "child", "external" }));
            Assert.That(impact.RemovedEdgeCount, Is.EqualTo(2));

            AuthoringMutationService.DeleteSelection(document, new[] { "root" }, null, null);

            Assert.That(document.Tree.RootNodeId, Is.Empty);
            Assert.That(document.Tree.Nodes.Select(node => node.Id), Does.Not.Contain("root"));
            Assert.That(document.Layout.Select(item => item.NodeId), Does.Not.Contain("root"));
            Assert.That(document.NodeMetadata.Select(item => item.NodeId), Does.Not.Contain("root"));
            Assert.That(document.Groups[0].NodeIds, Is.EqualTo(new[] { "child" }));
        }

        [Test]
        public void BatchProperties_DetectsMixedValuesAndAppliesCompatibleField()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var document = new AuthoringSourceDocument();
            document.Tree.Nodes.Add(Probability("a", 25));
            document.Tree.Nodes.Add(Probability("b", 50));

            var model = AuthoringMutationService.AnalyzeBatchProperties(
                document,
                registry,
                new[] { "a", "b" });

            var percent = model.Fields.Single(field => field.Schema.Name == ProbabilityNode.PercentProperty);
            Assert.That(percent.State, Is.EqualTo(AuthoringBatchValueState.Mixed));
            var changed = AuthoringMutationService.ApplyBatchProperty(
                document,
                registry,
                new[] { "a", "b" },
                ProbabilityNode.PercentProperty,
                PropertyValue.Of(75L));

            Assert.That(changed, Is.EqualTo(2));
            Assert.That(document.Tree.Nodes.Select(node => node.Properties.TryGet(ProbabilityNode.PercentProperty, out var value) ? value.Int64Value : 0),
                Is.EqualTo(new[] { 75L, 75L }));
        }

        [Test]
        public void SearchV2_ScoresFuzzyQueryAndAppliesFavoriteRecentAndTagFilters()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var options = new NodeSearchOptions();
            options.FavoriteTypeIds.Add(BuiltInNodeTypes.SetBlackboard);
            options.RecentTypeIds.Add(BuiltInNodeTypes.BlackboardCompare);
            options.TagsByTypeId[BuiltInNodeTypes.SetBlackboard] = new System.Collections.Generic.HashSet<string>
            {
                "blackboard",
                "write",
            };

            var query = new NodeSearchQuery { Text = "setb", FavoritesOnly = true };
            query.Tags.Add("write");
            var results = NodeSearchV2.Search(registry.Descriptors, query, options);

            Assert.That(results.Select(result => result.Descriptor.TypeId),
                Is.EqualTo(new[] { BuiltInNodeTypes.SetBlackboard }));
            Assert.That(results[0].IsFavorite, Is.True);
            Assert.That(results[0].MatchedTags, Does.Contain("write"));
        }

        [Test]
        public void BlackboardUsage_ClassifiesReadsWritesAndTypeChangeImpact()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var document = new AuthoringSourceDocument();
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "score", Type = ValueType.Int64 });
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "source", Type = ValueType.Int64 });
            var compare = new NodeDefinition { Id = "compare", Type = BuiltInNodeTypes.BlackboardCompare };
            compare.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("score"));
            var write = new NodeDefinition { Id = "write", Type = BuiltInNodeTypes.SetBlackboard };
            write.Properties.Set(SetBlackboardNode.KeyProperty, PropertyValue.Of("score"));
            write.Properties.Set(SetBlackboardNode.FromKeyProperty, PropertyValue.Of("source"));
            document.Tree.Nodes.Add(compare);
            document.Tree.Nodes.Add(write);

            var usages = AuthoringMutationService.FindBlackboardUsages(document, registry, "score");

            Assert.That(usages.Select(usage => usage.Access),
                Is.EquivalentTo(new[] { AuthoringBlackboardAccess.Read, AuthoringBlackboardAccess.Write }));
            Assert.That(usages.All(usage => usage.JumpTarget.CanJumpToNode), Is.True);

            var impact = AuthoringMutationService.AnalyzeBlackboardTypeChange(
                document,
                registry,
                "score",
                ValueType.String);
            Assert.That(impact.HasImpact, Is.True);
            Assert.That(impact.Usages, Has.Count.EqualTo(2));
        }

        private static AuthoringSourceDocument CreateDocument()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "test";
            document.Tree.RootNodeId = "root";
            document.Tree.Nodes.Add(Node("root", BuiltInNodeTypes.Sequence, "child", "external"));
            document.Tree.Nodes.Add(Node("child", BuiltInNodeTypes.Succeed));
            document.Tree.Nodes.Add(Node("external", BuiltInNodeTypes.Succeed));
            document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = "root", DisplayName = "Root" });
            document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = "child", DisplayName = "Child" });
            document.Layout.Add(new NodeLayoutData { NodeId = "root", X = 10f, Y = 20f });
            document.Layout.Add(new NodeLayoutData { NodeId = "child", X = 100f, Y = 200f });
            document.Groups.Add(new AuthoringGroupData
            {
                Id = "group",
                Title = "Group",
                X = 0f,
                Y = 0f,
                Width = 240f,
                Height = 180f,
                NodeIds = { "root", "child" },
            });
            document.Notes.Add(new AuthoringNoteData
            {
                Id = "note",
                Text = "note text",
                X = 300f,
                Y = 400f,
            });
            return document;
        }

        private static NodeDefinition Node(string id, string type, params string[] children)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(children);
            return node;
        }

        private static NodeDefinition Probability(string id, long percent)
        {
            var node = new NodeDefinition { Id = id, Type = BuiltInNodeTypes.Probability };
            node.Properties.Set(ProbabilityNode.PercentProperty, PropertyValue.Of(percent));
            return node;
        }
    }
}
#endif
