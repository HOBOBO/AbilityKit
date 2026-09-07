using System.Linq;
using AbilityKit.HFSM;
using NUnit.Framework;
using UnityEngine;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Graph;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    public sealed class DefinitionExporterTests
    {
        [Test]
        public void ExportsGraphBindingFieldsToCanonicalDefinitionJson()
        {
            var graph = CreateGraph(out var idle, out var attack, out var edge);
            try
            {
                idle.AddLogicAction("LegacyTick");
                idle.NextBehaviorKey = "combat.idle";
                edge.ConditionConfigJson = "{\"Conditions\":[{}]}";
                edge.NextConditionKey = "combat.ready";
                edge.NextActionKey = "combat.transition";
                edge.NextTriggerId = "attack";
                edge.NextMinimumActiveDurationRaw = 4294967296L;

                var catalog = new BindingCatalog();
                catalog.Register(new BindingDescriptor(BindingKind.State, "combat.idle", "Idle"));
                catalog.Register(new BindingDescriptor(BindingKind.Condition, "combat.ready", "Ready"));
                catalog.Register(new BindingDescriptor(BindingKind.Action, "combat.transition", "Transition"));

                var result = DefinitionExporter.Export(graph, catalog);

                Assert.That(result.IsSuccess, Is.True, string.Join("\n", result.Issues));
                var restored = DefinitionJson.Load(result.Json);
                Assert.That(restored.ComputeDefinitionHash(), Is.EqualTo(result.Definition.ComputeDefinitionHash()));
                var transition = restored.Machines.Single().Transitions.Single();
                Assert.That(transition.TriggerId, Is.EqualTo("attack"));
                Assert.That(transition.ConditionKey, Is.EqualTo("combat.ready"));
                Assert.That(transition.ActionKey, Is.EqualTo("combat.transition"));
                Assert.That(transition.MinimumActiveDurationRaw, Is.EqualTo(4294967296L));
                Assert.That(restored.Machines.Single().States.Single(state => state.Id == idle.Id).BehaviorKey,
                    Is.EqualTo("combat.idle"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void UnknownBindingDescriptorBlocksExport()
        {
            var graph = CreateGraph(out var idle, out _, out _);
            try
            {
                idle.AddLogicAction("LegacyTick");
                idle.NextBehaviorKey = "missing.binding";

                var result = DefinitionExporter.Export(graph, new BindingCatalog());

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Json, Is.Empty);
                Assert.That(result.Issues.Any(issue => issue.Code == "HFSMNEXT001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void EditorCatalogDiscoversMetadataWithoutConstruction()
        {
            AbilityKit.HFSM.Editor.EditorBindingCatalog.Reset();

            Assert.That(
                AbilityKit.HFSM.Editor.EditorBindingCatalog.Catalog.Contains(
                    BindingKind.State,
                    "tests.editor.metadata"),
                Is.True);
        }

        [Test]
        public void VersionedCatalogAssetBuildsMetadataAndReportsDuplicateKeys()
        {
            var asset = ScriptableObject.CreateInstance<AbilityKit.HFSM.Editor.BindingCatalogAsset>();
            try
            {
                asset.AddEntry(new AbilityKit.HFSM.Editor.BindingCatalogEntry
                {
                    Kind = BindingKind.State,
                    Key = "combat.idle",
                    DisplayName = "Idle"
                });
                asset.AddEntry(new AbilityKit.HFSM.Editor.BindingCatalogEntry
                {
                    Kind = BindingKind.State,
                    Key = "combat.idle",
                    DisplayName = "Duplicate"
                });

                var catalog = asset.BuildCatalog();
                Assert.That(catalog.Contains(BindingKind.State, "combat.idle"), Is.True);
                Assert.That(catalog.Issues.Any(issue => issue.Code == "HFSMBIND001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ConfiguredCatalogAssetOverridesReflectionCatalog()
        {
            var previous = AbilityKit.HFSM.Editor.EditorBindingCatalog.ConfiguredAsset;
            var asset = ScriptableObject.CreateInstance<AbilityKit.HFSM.Editor.BindingCatalogAsset>();
            try
            {
                asset.AddEntry(new AbilityKit.HFSM.Editor.BindingCatalogEntry
                {
                    Kind = BindingKind.State,
                    Key = "tests.asset.metadata",
                    DisplayName = "Asset Metadata"
                });

                AbilityKit.HFSM.Editor.EditorBindingCatalog.SetConfiguredAsset(asset);
                Assert.That(
                    AbilityKit.HFSM.Editor.EditorBindingCatalog.Catalog.Contains(
                        BindingKind.State, "tests.asset.metadata"),
                    Is.True);
            }
            finally
            {
                AbilityKit.HFSM.Editor.EditorBindingCatalog.SetConfiguredAsset(previous);
                AbilityKit.HFSM.Editor.EditorBindingCatalog.Reset();
                Object.DestroyImmediate(asset);
            }
        }

        private static GraphAsset CreateGraph(
            out StateNode idle,
            out StateNode attack,
            out TransitionEdge edge)
        {
            var graph = ScriptableObject.CreateInstance<GraphAsset>();
            graph.GraphName = "combat";
            var root = graph.CreateStateMachine("Root", Vector2.zero);
            idle = graph.CreateState("Idle", Vector2.zero);
            attack = graph.CreateState("Attack", Vector2.one);
            root.AddChildNode(idle.Id);
            root.AddChildNode(attack.Id);
            root.DefaultStateId = idle.Id;
            edge = graph.CreateTransition(idle.Id, attack.Id);
            root.AddTransition(edge.Id);
            return graph;
        }

        [Binding(BindingKind.State, "tests.editor.metadata", "Editor Metadata")]
        private sealed class EditorMetadataOnly
        {
        }
    }
}
