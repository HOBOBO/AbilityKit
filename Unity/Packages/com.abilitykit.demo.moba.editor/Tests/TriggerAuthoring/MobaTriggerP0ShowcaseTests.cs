#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests.TriggerAuthoring
{
    public sealed class MobaTriggerP0ShowcaseTests
    {
        private const string ProjectPath =
            "Assets/AbilityKit/MobaTriggerAuthoring/MobaTriggerAuthoringProject.asset";
        private const string ModulePath =
            "Assets/AbilityKit/MobaTriggerAuthoring/Packages/ability_moba_tests_p0_showcase.Module.asset";
        private const string SourcePath =
            "Assets/AbilityKit/MobaTriggerAuthoring/Showcases/moba-p0-complex-skill.trigger.json";

        [Test]
        public void ShowcaseSource_RoundTripsAndExportsAllP0AuthoringCapabilities()
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourcePath));
            var document = TriggerAuthoringSourceCodec.ReadFile(absolutePath);
            var roundTrip = TriggerAuthoringSourceCodec.Deserialize(
                TriggerAuthoringSourceCodec.Serialize(document));
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectPath);
            Assert.That(project, Is.Not.Null);

            var trigger = roundTrip.Module.Triggers.Single();
            Assert.That(trigger.Id, Is.EqualTo(MobaTriggerAuthoringTestIds.P0ComplexShowcase));
            Assert.That(MobaTriggerAuthoringTestIds.IsReserved(trigger.Id), Is.True);
            var nodes = Enumerate(trigger.Actions).ToArray();
            Assert.That(nodes.Any(node => node.Type == "for_each"), Is.True);
            Assert.That(nodes.Any(node => node.Type == "random"), Is.True);
            Assert.That(nodes.Count(node => node.Type == "weighted"), Is.EqualTo(2));
            Assert.That(nodes.Any(node => node.Type == "scheduled"), Is.True);
            Assert.That(nodes.Any(node => node.Arguments.Any(argument =>
                argument.Name == "magnitude_capture" &&
                argument.Value.Source == TriggerValueSource.Context &&
                argument.Value.Path == "skill_runtime:target.showcase_snapshot_damage")), Is.True);
            Assert.That(nodes.Any(node => node.Arguments.Any(argument =>
                argument.Value.Source == TriggerValueSource.Expression &&
                argument.Value.Expression.Contains("skill_runtime.cast.showcase_combo"))), Is.True);

            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateForProject(project),
                Events = TriggerEventDescriptorCatalog.FromProject(project),
                ValueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(project),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(project.GlobalBlackboardCatalog),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(project.TemplateCatalog)
            };
            var diagnostics = TriggerAuthoringValidator.Validate(roundTrip.Module, context);
            Assert.That(TriggerAuthoringValidator.HasErrors(diagnostics), Is.False,
                string.Join("\n", diagnostics.Select(item => item.Code + " " + item.Path + ": " + item.Message)));

            var export = TriggerAuthoringRuntimeExporter.Build(roundTrip.Module, context);
            Assert.That(export.Success, Is.True, export.BuildMessage());
            var runtime = new TriggerPlanJsonDatabase();
            runtime.LoadFromJson(TriggerAuthoringRuntimeExporter.Serialize(export.Database), "moba-p0-showcase");
            Assert.That(runtime.TryGetExecutionRootByTriggerId(trigger.Id, out var root), Is.True);
            Assert.That(root, Is.TypeOf<SequenceTriggerPlanExecutable>());
            var runtimeNodes = Enumerate(root).ToArray();
            Assert.That(runtimeNodes.Any(node => node is ForEachTriggerPlanExecutable), Is.True);
            Assert.That(runtimeNodes.Any(node => node is RandomTriggerPlanExecutable), Is.True);
            Assert.That(runtimeNodes.Any(node => node is ScheduledTriggerPlanExecutable), Is.True);
        }

        [Test]
        public void ShowcaseAsset_IsImportedAndRegisteredInWorkspaceProject()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectPath);
            var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(ModulePath);

            Assert.That(project, Is.Not.Null);
            Assert.That(module, Is.Not.Null,
                "Run TriggerAuthoringMobaMigration.SyncP0ShowcaseBatch after editing the showcase source.");
            Assert.That(project.Modules, Does.Contain(module));
            Assert.That(module.Project, Is.SameAs(project));
            Assert.That(module.Module.Triggers.Single().Id,
                Is.EqualTo(MobaTriggerAuthoringTestIds.P0ComplexShowcase));
            Assert.That(module.SourceJsonPath.Replace('\\', '/'), Is.EqualTo(SourcePath));
        }

        private static IEnumerable<TriggerNodeData> Enumerate(TriggerNodeData node)
        {
            if (node == null) yield break;
            yield return node;
            foreach (var child in node.Children)
                foreach (var descendant in Enumerate(child))
                    yield return descendant;
            foreach (var child in node.ElseChildren)
                foreach (var descendant in Enumerate(child))
                    yield return descendant;
        }

        private static IEnumerable<ITriggerPlanExecutable> Enumerate(ITriggerPlanExecutable node)
        {
            if (node == null) yield break;
            yield return node;
            if (node is CompositeTriggerPlanExecutableBase composite)
            {
                foreach (var child in composite.Children)
                    foreach (var descendant in Enumerate(child))
                        yield return descendant;
            }
            else if (node is ForEachTriggerPlanExecutable forEach)
            {
                foreach (var descendant in Enumerate(forEach.Child)) yield return descendant;
            }
            else if (node is ScheduledTriggerPlanExecutable scheduled)
            {
                foreach (var descendant in Enumerate(scheduled.Child)) yield return descendant;
            }
            else if (node is IfTriggerPlanExecutable conditional)
            {
                foreach (var descendant in Enumerate(conditional.ThenBranch)) yield return descendant;
                foreach (var descendant in Enumerate(conditional.ElseBranch)) yield return descendant;
            }
        }
    }
}
#endif
