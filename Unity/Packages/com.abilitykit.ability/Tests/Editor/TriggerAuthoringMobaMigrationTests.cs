#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringMobaMigrationTests
    {
        [Test]
        public void ConvertFiles_MapsLegacyAliasesConditionsAndExecutionControl()
        {
            var root = Path.Combine(Path.GetTempPath(), "AbilityKitMobaTriggerMigration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "trigger_42.json");
            File.WriteAllText(path,
                "{\"id\":42,\"name\":\"Rule\",\"event\":\"unit.die\",\"execution\":\"once\",\"conditions\":[" +
                "{\"type\":\"arg_gte\",\"left_var_domain\":\"gameplay\",\"left_var_key\":\"1001\",\"value\":5}]," +
                "\"actions\":[{\"type\":\"add_buff\",\"buff_id\":101,\"targetActorId\":7}]}" );
            try
            {
                var module = TriggerAuthoringMobaMigration.ConvertFiles(
                    new TriggerAuthoringMobaMigration.PackageDefinition(
                        "gameplay", "gameplay", "test", "gameplay.test", "Test", TriggerModuleKind.Custom),
                    root,
                    new[] { path });
                var trigger = module.Triggers.Single();

                Assert.That(trigger.EntryMode, Is.EqualTo(TriggerEntryMode.Event));
                Assert.That(trigger.ExecutionControl.Mode, Is.EqualTo("once"));
                Assert.That(trigger.Condition.Arguments[0].Value.Source, Is.EqualTo(TriggerValueSource.Context));
                Assert.That(trigger.Condition.Arguments[0].Value.Path, Is.EqualTo("gameplay:1001"));
                Assert.That(trigger.Actions.Arguments.Single(item => item.Name == "buff_ids").Value.IntegerListValue,
                    Is.EqualTo(new long[] { 101 }));
                Assert.That(trigger.Actions.Arguments.Exists(item => item.Name == "target_actor_id"), Is.True);

                var compile = TriggerAuthoringRuntimeExporter.Build(module, new TriggerAuthoringValidationContext
                {
                    Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                    Events = new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents()),
                    GlobalBlackboard = new TriggerGlobalBlackboardDescriptorCatalog(
                        TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys())
                });
                Assert.That(compile.Success, Is.True, compile.BuildMessage());
                Assert.That(compile.Database.Triggers[0].ExecutionControl.Mode, Is.EqualTo("once"));
                Assert.That(compile.Database.Triggers[0].Predicate.Nodes[0].Left.DomainId, Is.EqualTo("gameplay"));
                Assert.That(compile.Database.Triggers[0].Predicate.Nodes[0].Left.Key, Is.EqualTo("1001"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ConvertFiles_UsesEventPayloadTypeForComparisonOperands()
        {
            var root = Path.Combine(Path.GetTempPath(), "AbilityKitMobaTriggerMigration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "trigger_43.json");
            File.WriteAllText(path,
                "{\"id\":43,\"name\":\"Skill filter\",\"event\":\"skill.cast.start\"," +
                "\"conditions\":[{\"type\":\"arg_eq\",\"arg_name\":\"skill.id\",\"value\":1001}]," +
                "\"actions\":[{\"type\":\"noop\"}]}" );
            try
            {
                var module = TriggerAuthoringMobaMigration.ConvertFiles(
                    new TriggerAuthoringMobaMigration.PackageDefinition(
                        "skills", "ability", "moba.skills", "ability.moba.skills", "Skills", TriggerModuleKind.Ability),
                    root,
                    new[] { path });
                var condition = module.Triggers.Single().Condition;
                var left = condition.Arguments.Single(item => item.Name == "left").Value;
                var right = condition.Arguments.Single(item => item.Name == "right").Value;

                Assert.That(left.Source, Is.EqualTo(TriggerValueSource.Payload));
                Assert.That(left.Path, Is.EqualTo("skill.id"));
                Assert.That(left.Type, Is.EqualTo(TriggerValueType.Integer));
                Assert.That(right.Source, Is.EqualTo(TriggerValueSource.Constant));
                Assert.That(right.Type, Is.EqualTo(TriggerValueType.Integer));
                Assert.That(right.IntegerValue, Is.EqualTo(1001));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ConvertFiles_MapsEmptyActionListToDisabledSequence()
        {
            var root = Path.Combine(Path.GetTempPath(), "AbilityKitMobaTriggerMigration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "trigger_44.json");
            File.WriteAllText(path,
                "{\"id\":44,\"name\":\"Empty entry\",\"event\":\"\",\"actions\":[]}" );
            try
            {
                var module = TriggerAuthoringMobaMigration.ConvertFiles(
                    new TriggerAuthoringMobaMigration.PackageDefinition(
                        "passives", "passive", "moba.passives", "passive.moba.passives", "Passives", TriggerModuleKind.Passive),
                    root,
                    new[] { path });

                Assert.That(module.Triggers.Single().Actions.Type, Is.EqualTo("seq"));
                Assert.That(module.Triggers.Single().Actions.Enabled, Is.False);
                Assert.That(module.Triggers.Single().Enabled, Is.False);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CheckedInMobaSources_AllConvertValidateAndCompile()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            var legacyRoot = Path.Combine(projectRoot, TriggerAuthoringMobaMigration.LegacySourceRoot);
            var definitions = new[]
            {
                new TriggerAuthoringMobaMigration.PackageDefinition("skills", "ability", "moba.skills", "ability.moba.skills", "Skills", TriggerModuleKind.Ability),
                new TriggerAuthoringMobaMigration.PackageDefinition("buffs", "buff", "moba.buffs", "buff.moba.buffs", "Buffs", TriggerModuleKind.Buff),
                new TriggerAuthoringMobaMigration.PackageDefinition("passives", "passive", "moba.passives", "passive.moba.passives", "Passives", TriggerModuleKind.Passive),
                new TriggerAuthoringMobaMigration.PackageDefinition("gameplay", "gameplay", "moba.rules", "gameplay.moba.rules", "Gameplay", TriggerModuleKind.Custom)
            };
            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents()),
                GlobalBlackboard = new TriggerGlobalBlackboardDescriptorCatalog(
                    TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys())
            };
            var sourceCount = 0;
            var triggerCount = 0;
            var ids = new HashSet<int>();

            foreach (var definition in definitions)
            {
                var files = Directory.GetFiles(
                    Path.Combine(legacyRoot, definition.LegacyDirectory),
                    "*.json",
                    SearchOption.TopDirectoryOnly);
                sourceCount += files.Length;
                var module = TriggerAuthoringMobaMigration.ConvertFiles(definition, legacyRoot, files);
                var diagnostics = TriggerAuthoringValidator.Validate(module, context);
                var compile = TriggerAuthoringRuntimeExporter.Build(module, context);
                Assert.That(TriggerAuthoringValidator.HasErrors(diagnostics), Is.False,
                    string.Join(Environment.NewLine, diagnostics.Select(item => item.Code + " " + item.Path + ": " + item.Message)));
                Assert.That(compile.Success, Is.True, compile.BuildMessage());
                triggerCount += module.Triggers.Count;
                foreach (var trigger in module.Triggers)
                    Assert.That(ids.Add(trigger.Id), Is.True, "Duplicate trigger ID " + trigger.Id);
            }

            Assert.That(sourceCount, Is.EqualTo(87));
            Assert.That(triggerCount, Is.EqualTo(99));
        }
    }
}
#endif
