using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Deterministic;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class CompleteSampleContractTests
    {
        private const string SampleJsonPath =
            "Packages/com.abilitykit.behaviortree/Samples~/CompleteRuntimeObservation/Authoring/complete_runtime_observation.authoring.json";

        [Test]
        public void CompleteSample_AuthoringJsonLoadsExportsAndCreatesRuntime()
        {
            Assert.That(File.Exists(SampleJsonPath), Is.True, SampleJsonPath);
            var document = AuthoringJson.Load(File.ReadAllText(SampleJsonPath));
            Assert.That(document.Tree.TreeId, Is.EqualTo("sample.complete_runtime_observation"));
            Assert.That(document.Tree.Nodes, Has.Count.EqualTo(26));
            Assert.That(document.Layout, Has.Count.EqualTo(document.Tree.Nodes.Count));
            Assert.That(document.Groups, Has.Count.EqualTo(3));
            Assert.That(document.Notes, Has.Count.EqualTo(3));

            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var json = TreeExporter.Export(document, registry, out var errors);
            Assert.That(errors, Is.Empty);
            Assert.That(json, Is.Not.Empty);

            using var runtime = TreeRuntime.Create(
                TreeJson.Load(json),
                registry,
                options: new TreeRunOptions { DebugName = "sample-contract" });
            runtime.Enable();
            runtime.Update(1, Fixed64.FromRatio(1, 30));
            Assert.That(runtime.IsEnabled, Is.True);
            Assert.That(runtime.Blackboard.GetString("out.mode"), Is.EqualTo("Patrol"));
        }

        [Test]
        public void CompleteSample_RoundTripsThroughAuthoringAsset()
        {
            var source = File.ReadAllText(SampleJsonPath);
            var asset = UnityEngine.ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                asset.ImportJson(source);
                var loaded = asset.LoadDocument();
                Assert.That(loaded.Tree.TreeId, Is.EqualTo("sample.complete_runtime_observation"));
                Assert.That(loaded.NodeMetadata, Is.Not.Empty);
                Assert.That(loaded.Layout, Has.Count.EqualTo(26));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
