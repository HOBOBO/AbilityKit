using System.Linq;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase.Tests
{
    public sealed class HfsmShowcaseExportTests
    {
        [Test]
        public void BothEditableGraphsExportValidatedDefinitions()
        {
            var patrol = Editor.HfsmShowcaseDocuments.BuildPatrol();
            var combat = Editor.HfsmShowcaseDocuments.BuildCombat();
            try
            {
                foreach (var graph in new[] { patrol, combat })
                {
                    var report = DefinitionExporter.Export(graph, Editor.HfsmShowcaseDocuments.BuildCatalog());
                    Assert.That(report.IsSuccess, Is.True, string.Join("\n", report.Issues));
                    var loaded = DefinitionJson.Load(report.Json);
                    Assert.That(loaded.ComputeDefinitionHash(), Is.EqualTo(report.Definition.ComputeDefinitionHash()));
                    var seed = Resources.Load<TextAsset>("hfsm_showcase_actions");
                    Assert.That(seed, Is.Not.Null);
                    var actions = CompositeActionCatalog.LoadJson(seed.text);
                    foreach (var machine in loaded.Machines)
                        foreach (var state in machine.States)
                            if (state.BehaviorKey.StartsWith("showcase.patrol.") ||
                                state.BehaviorKey.StartsWith("showcase.combat."))
                                Assert.That(actions.Get(state.BehaviorKey), Is.Not.Null);
                }
                Assert.That(DefinitionExporter.Export(combat, Editor.HfsmShowcaseDocuments.BuildCatalog())
                    .Definition.Machines.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(patrol);
                Object.DestroyImmediate(combat);
            }
        }

        [Test]
        public void EditorActionAssetRoundTripsCompositeTreeAndCanBeEdited()
        {
            var seed = Resources.Load<TextAsset>("hfsm_showcase_actions");
            Assert.That(seed, Is.Not.Null);
            var source = ScriptableObject.CreateInstance<HfsmShowcaseActionAsset>();
            var copy = ScriptableObject.CreateInstance<HfsmShowcaseActionAsset>();
            try
            {
                source.LoadJson(seed.text);
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(
                    UnityEditor.EditorJsonUtility.ToJson(source), copy);
                var catalog = CompositeActionCatalog.LoadJson(copy.ExportJson());
                var strike = catalog.Get("showcase.combat.strike");
                Assert.That(strike.Actions.Count, Is.GreaterThan(3));
                strike.Actions[0].Message = "edited windup";
                copy.LoadJson(catalog.SaveJson());
                Assert.That(CompositeActionCatalog.LoadJson(copy.ExportJson())
                    .Get("showcase.combat.strike").Actions[0].Message, Is.EqualTo("edited windup"));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void GraphEditsChangeExportAndRuntimeSnapshotRestores()
        {
            var graph = Editor.HfsmShowcaseDocuments.BuildCombat();
            var go = new GameObject("TestAgent");
            try
            {
                var initial = DefinitionExporter.Export(graph, Editor.HfsmShowcaseDocuments.BuildCatalog());
                Assert.That(initial.IsSuccess, Is.True, string.Join("\n", initial.Issues));
                graph.Edges.First().Priority += 1;
                var edited = DefinitionExporter.Export(graph, Editor.HfsmShowcaseDocuments.BuildCatalog());
                Assert.That(edited.IsSuccess, Is.True, string.Join("\n", edited.Issues));
                Assert.That(edited.Definition.ComputeDefinitionHash(), Is.Not.EqualTo(initial.Definition.ComputeDefinitionHash()));

                var agent = go.AddComponent<HfsmShowcaseAgent>();
                agent.HasTarget = true;
                agent.TargetDistance = 1;
                var machine = new StateMachineRuntime<HfsmShowcaseAgent>(agent, edited.Definition,
                    HfsmShowcaseAgent.CreateBindings());
                machine.Initialize(0, Fixed64.Zero);
                machine.Tick(1, Fixed64.FromRatio(1, 10));
                var saved = machine.CaptureSnapshot();
                Assert.That(machine.GetActivePath().Count, Is.EqualTo(2));
                agent.Health = 0;
                machine.Tick(2, Fixed64.FromRatio(2, 10));
                Assert.That(machine.GetActivePath().Count, Is.EqualTo(1));
                machine.RestoreSnapshot(saved);
                Assert.That(machine.CurrentFrame, Is.EqualTo(1));
                Assert.That(machine.GetActivePath().Count, Is.EqualTo(2));
                machine.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(graph);
            }
        }
    }
}
