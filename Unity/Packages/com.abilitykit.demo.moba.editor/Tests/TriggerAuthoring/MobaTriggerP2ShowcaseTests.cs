#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests.TriggerAuthoring
{
    public sealed class MobaTriggerP2ShowcaseTests
    {
        [Test]
        public void ShowcaseSource_RoundTripsAndExportsAllWritableSkillParameters()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", MobaP2SkillProgrammingShowcaseSync.TriggerSourceAssetPath));
            var document = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
            var roundTrip = TriggerAuthoringSourceCodec.Deserialize(
                TriggerAuthoringSourceCodec.Serialize(document));
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(
                MobaP2SkillProgrammingShowcaseSync.ProjectAssetPath);
            Assert.That(project, Is.Not.Null);

            Assert.That(roundTrip.Module.ModuleId, Is.EqualTo(MobaP2SkillProgrammingShowcaseSync.TriggerModuleId));
            Assert.That(roundTrip.Module.Triggers.Select(item => item.Id), Is.EqualTo(new[]
            {
                MobaTriggerAuthoringTestIds.P2ApplySkillParameters,
                MobaTriggerAuthoringTestIds.P2ClearSkillParameters,
            }));
            Assert.That(roundTrip.Module.Triggers.All(item => MobaTriggerAuthoringTestIds.IsReserved(item.Id)), Is.True);

            var applyNodes = Enumerate(roundTrip.Module.Triggers[0].Actions).ToArray();
            var parameterIds = applyNodes
                .Where(item => item.Type == "add_skill_param_modifier")
                .Select(item => item.Arguments.Single(argument => argument.Name == "parameter_id").Value.IntegerValue)
                .ToArray();
            Assert.That(parameterIds, Is.EqualTo(new long[] { 3, 4, 5, 8, 9, 10 }));
            Assert.That(roundTrip.Module.Triggers[1].Actions.Type, Is.EqualTo("remove_skill_param_modifiers"));

            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateForProject(project),
                Events = TriggerEventDescriptorCatalog.FromProject(project),
                ValueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(project),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(project.GlobalBlackboardCatalog),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(project.TemplateCatalog),
            };
            var diagnostics = TriggerAuthoringValidator.Validate(roundTrip.Module, context);
            Assert.That(TriggerAuthoringValidator.HasErrors(diagnostics), Is.False,
                string.Join("\n", diagnostics.Select(item => item.Code + " " + item.Path + ": " + item.Message)));

            var export = TriggerAuthoringRuntimeExporter.Build(roundTrip.Module, context);
            Assert.That(export.Success, Is.True, export.BuildMessage());
        }

        [Test]
        public void ShowcaseAssets_AreImportedAndRegisteredInWorkspaceProject()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(
                MobaP2SkillProgrammingShowcaseSync.ProjectAssetPath);
            var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(
                MobaP2SkillProgrammingShowcaseSync.TriggerModuleAssetPath);

            Assert.That(project, Is.Not.Null);
            Assert.That(module, Is.Not.Null,
                "Run MobaP2SkillProgrammingShowcaseSync.SyncBatch to import the P2 showcase.");
            Assert.That(project.Modules, Does.Contain(module));
            Assert.That(module.Project, Is.SameAs(project));
            Assert.That(module.SourceJsonPath.Replace('\\', '/'),
                Is.EqualTo(MobaP2SkillProgrammingShowcaseSync.TriggerSourceAssetPath));
            Assert.That(module.Module.Triggers, Has.Count.EqualTo(2));
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
    }
}
#endif
