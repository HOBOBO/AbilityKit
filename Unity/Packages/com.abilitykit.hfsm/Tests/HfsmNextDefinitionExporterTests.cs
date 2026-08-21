using System.Linq;
using AbilityKit.HFSM;
using NUnit.Framework;
using UnityEngine;
using UnityHFSM.Editor.Export;
using UnityHFSM.Graph;

namespace AbilityKit.Tests
{
    public sealed class HfsmNextDefinitionExporterTests
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

                var catalog = new HfsmBindingCatalog();
                catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.State, "combat.idle", "Idle"));
                catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.Condition, "combat.ready", "Ready"));
                catalog.Register(new HfsmBindingDescriptor(HfsmBindingKind.Action, "combat.transition", "Transition"));

                var result = HfsmNextDefinitionExporter.Export(graph, catalog);

                Assert.That(result.IsSuccess, Is.True, string.Join("\n", result.Issues));
                var restored = HfsmDefinitionJson.Load(result.Json);
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

                var result = HfsmNextDefinitionExporter.Export(graph, new HfsmBindingCatalog());

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
            UnityHFSM.Editor.HfsmEditorBindingCatalog.Reset();

            Assert.That(
                UnityHFSM.Editor.HfsmEditorBindingCatalog.Catalog.Contains(
                    HfsmBindingKind.State,
                    "tests.editor.metadata"),
                Is.True);
        }

        [Test]
        public void VersionedCatalogAssetBuildsMetadataAndReportsDuplicateKeys()
        {
            var asset = ScriptableObject.CreateInstance<UnityHFSM.Editor.HfsmBindingCatalogAsset>();
            try
            {
                asset.AddEntry(new UnityHFSM.Editor.HfsmBindingCatalogEntry
                {
                    Kind = HfsmBindingKind.State,
                    Key = "combat.idle",
                    DisplayName = "Idle"
                });
                asset.AddEntry(new UnityHFSM.Editor.HfsmBindingCatalogEntry
                {
                    Kind = HfsmBindingKind.State,
                    Key = "combat.idle",
                    DisplayName = "Duplicate"
                });

                var catalog = asset.BuildCatalog();
                Assert.That(catalog.Contains(HfsmBindingKind.State, "combat.idle"), Is.True);
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
            var asset = ScriptableObject.CreateInstance<UnityHFSM.Editor.HfsmBindingCatalogAsset>();
            try
            {
                asset.AddEntry(new UnityHFSM.Editor.HfsmBindingCatalogEntry
                {
                    Kind = HfsmBindingKind.State,
                    Key = "tests.asset.metadata",
                    DisplayName = "Asset Metadata"
                });

                UnityHFSM.Editor.HfsmEditorBindingCatalog.SetConfiguredAsset(asset);
                Assert.That(
                    UnityHFSM.Editor.HfsmEditorBindingCatalog.Catalog.Contains(
                        HfsmBindingKind.State, "tests.asset.metadata"),
                    Is.True);
            }
            finally
            {
                UnityHFSM.Editor.HfsmEditorBindingCatalog.SetConfiguredAsset(null);
                UnityHFSM.Editor.HfsmEditorBindingCatalog.Reset();
                Object.DestroyImmediate(asset);
            }
        }

        private static HfsmGraphAsset CreateGraph(
            out HfsmStateNode idle,
            out HfsmStateNode attack,
            out HfsmTransitionEdge edge)
        {
            var graph = ScriptableObject.CreateInstance<HfsmGraphAsset>();
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

        [HfsmBinding(HfsmBindingKind.State, "tests.editor.metadata", "Editor Metadata")]
        private sealed class EditorMetadataOnly
        {
        }
    }
}
