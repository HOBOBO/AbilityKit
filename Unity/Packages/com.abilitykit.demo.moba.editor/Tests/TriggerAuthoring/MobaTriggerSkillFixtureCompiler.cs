#if UNITY_EDITOR
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests.TriggerAuthoring
{
    internal static class MobaTriggerSkillFixtureCompiler
    {
        private const string ProjectAssetPath =
            "Assets/AbilityKit/MobaTriggerAuthoring/MobaTriggerAuthoringProject.asset";
        private const string FixtureDirectory =
            "Packages/com.abilitykit.demo.moba.editor/Tests/Fixtures/TriggerAuthoring";

        public static TriggerPlanJsonDatabase Compile(string fixtureFileName, int expectedTriggerId)
        {
            Assert.That(MobaTriggerAuthoringTestIds.IsReserved(expectedTriggerId), Is.True,
                $"Trigger {expectedTriggerId} is outside the reserved MOBA authoring test range.");

            var fixturePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                FixtureDirectory,
                fixtureFileName));
            var document = TriggerAuthoringSourceCodec.ReadFile(fixturePath);
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectAssetPath);
            Assert.That(project, Is.Not.Null, "MOBA Trigger Authoring project asset is required.");

            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateForProject(project),
                Events = TriggerEventDescriptorCatalog.FromProject(project),
                ValueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(project),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                    project.GlobalBlackboardCatalog),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(project.TemplateCatalog)
            };
            var export = TriggerAuthoringRuntimeExporter.Build(document.Module, context);

            Assert.That(export.Success, Is.True, export.BuildMessage());
            Assert.That(export.ExportedTriggerCount, Is.EqualTo(1));
            Assert.That(export.Database.Triggers[0].TriggerId, Is.EqualTo(expectedTriggerId));

            var runtimeDatabase = new TriggerPlanJsonDatabase();
            runtimeDatabase.LoadFromJson(
                TriggerAuthoringRuntimeExporter.Serialize(export.Database),
                Path.GetFileNameWithoutExtension(fixtureFileName));
            Assert.That(runtimeDatabase.TryGetExecutionRootByTriggerId(expectedTriggerId, out _), Is.True);
            return runtimeDatabase;
        }

        public static void CompileAndMerge(
            MobaSkillConfigTestHarness harness,
            string fixtureFileName,
            int expectedTriggerId)
        {
            var runtimeDatabase = Compile(fixtureFileName, expectedTriggerId);
            harness.TriggerPlans.MergeFrom(runtimeDatabase, replaceExisting: true);
            harness.TriggerPlans.ConfigureOwnerBlackboards(
                harness.World.Services.Resolve<IOwnerBlackboardStore>(),
                releaseExisting: true);
        }
    }
}
#endif
