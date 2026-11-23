using System;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaTriggerResourceLoadingBaselineTests
    {
        [Test]
        public void Actual_unity_resources_resolve_the_published_console_records()
        {
            const string aggregatePath = "ability/ability_trigger_plans.json";
            var consoleRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
                "src", "AbilityKit.Demo.Moba.Console", "Configs", "luban"));
            var rules = Directory.GetFiles(Path.Combine(consoleRoot, "ability", "rules"), "*.json", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(consoleRoot, file).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(rules, Is.Not.Empty);

            var loader = new ResourcesTextAssetLoader();
            var expected = new TriggerPlanJsonDatabase();
            var paths = new[] { aggregatePath }.Concat(rules);
            foreach (var path in paths)
            {
                var consoleJson = File.ReadAllText(Path.Combine(consoleRoot, path));
                Assert.That(loader.TryLoadText(path, out var resourceJson), Is.True, path);
                Assert.That(JToken.DeepEquals(JToken.Parse(consoleJson), JToken.Parse(resourceJson)), Is.True, path);

                var entry = new TriggerPlanJsonDatabase();
                entry.LoadFromJson(consoleJson, path);
                expected.MergeFrom(entry, replaceExisting: true);
            }

            var builder = new WorldContainerBuilder();
            var blackboards = new DictionaryBlackboardResolver();
            builder.RegisterInstance<ITextAssetLoader>(loader);
            builder.RegisterInstance(MobaTriggerPlanLoadProfile.Default);
            builder.RegisterInstance<IBlackboardResolver>(blackboards);
            builder.RegisterInstance<IOwnerBlackboardStore>(new OwnerBlackboardStore(blackboards));
            builder.RegisterInstance(new MobaPresentationCueSnapshotService(new MobaLogicWorldRunGateService()));
            using var scope = builder.Build().CreateScope();
            var actual = new MobaTriggerPlanDatabaseFactory().Create(scope);

            Assert.That(actual.Records.Select(record => record.TriggerId),
                Is.EquivalentTo(expected.Records.Select(record => record.TriggerId)));
            foreach (var wanted in expected.Records)
            {
                Assert.That(actual.TryGetRecordByTriggerId(wanted.TriggerId, out var loaded), Is.True);
                var context = $"Trigger {wanted.TriggerId}";
                Assert.That(loaded.EventName, Is.EqualTo(wanted.EventName), context);
                Assert.That(loaded.EventId, Is.EqualTo(wanted.EventId), context);
                Assert.That(loaded.Scope, Is.EqualTo(wanted.Scope), context);
                Assert.That(loaded.Plan.Phase, Is.EqualTo(wanted.Plan.Phase), context);
                Assert.That(loaded.Plan.Priority, Is.EqualTo(wanted.Plan.Priority), context);
                Assert.That(loaded.ExecutionRoot?.GetType(), Is.EqualTo(wanted.ExecutionRoot?.GetType()), context);
                Assert.That(loaded.Plan.Actions.Select(action => action.Id.Value),
                    Is.EqualTo(wanted.Plan.Actions.Select(action => action.Id.Value)), context);
            }
        }
    }
}
