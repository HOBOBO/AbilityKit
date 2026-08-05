using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaTriggerPlanPayloadCompatibilityTests
    {
        private const string EventName = "test.payload.compatibility";
        private const int TriggerId = 990001;

        [Test]
        public void ValidateDatabase_RejectsFieldUnsupportedByConcreteEventArgs()
        {
            var report = ValidatePayloadField<AttackInfo>(
                MobaBattlePayloadAccessor.SupportsAttackInfoField,
                MobaBattlePayloadFields.DamageValue);

            AssertReportContainsCode(report, "moba.trigger.plan.payload_field_incompatible");
            Assert.IsTrue(report.ShouldBlockStartup);
        }

        [Test]
        public void ValidateDatabase_AcceptsFieldSupportedByConcreteEventArgs()
        {
            var report = ValidatePayloadField<DamageResult>(
                MobaBattlePayloadAccessor.SupportsDamageResultField,
                MobaBattlePayloadFields.DamageValue);

            AssertReportDoesNotContainCode(report, "moba.trigger.plan.payload_field_incompatible");
            Assert.AreEqual(0, report.ErrorCount, report.FormatAllEntries());
        }

        [Test]
        public void ValidateDatabase_EmptyDatabaseBlocksStartup()
        {
            var report = new MobaRuntimeValidationReport();

            MobaTriggerPlanIntegrityValidator.ValidateDatabase(
                new TriggerPlanJsonDatabase(),
                eventRegistry: null,
                payloadRegistry: null,
                report);

            AssertReportContainsCode(report, "moba.trigger.plan.empty");
            Assert.AreEqual(1, report.ErrorCount, report.FormatAllEntries());
            Assert.IsTrue(report.ShouldBlockStartup, report.FormatAllEntries());
        }

        [Test]
        public void ResourcesJsonProfile_DefaultIsStrict()
        {
            Assert.IsTrue(ResourcesJsonMobaConfigLoadProfile.Default.Strict);
            Assert.IsFalse(new ResourcesJsonMobaConfigLoadProfile(strict: false).Strict);
        }

        [Test]
        public void LubanDeserializer_NonArrayJsonRootThrowsWithDtoAndRootType()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                LubanConfigGroupDeserializer.Instance.DeserializeFromText("{\"Code\": 1}", typeof(SkillDTO)));

            StringAssert.Contains(typeof(SkillDTO).FullName, exception.Message);
            StringAssert.Contains("Object", exception.Message);
        }

        [Test]
        public void DefaultTriggerPlanLoadProfile_EnablesDirectoryFailFast()
        {
            Assert.IsTrue(MobaTriggerPlanLoadProfile.Default.FailFastOnDirectoryLoad);
            Assert.IsFalse(new MobaTriggerPlanLoadProfile(Array.Empty<TriggerPlanLoadEntry>()).FailFastOnDirectoryLoad);
        }

        [Test]
        public void TriggerPlanDirectoryLoader_FailFastOptionThrowsForMissingFile()
        {
            var loader = new TriggerPlanDirectoryLoader(new InMemoryDirectoryTextLoader("plans/missing.json"));
            var options = new TriggerPlanDirectoryLoadOptions { ThrowOnFileParseError = true };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                loader.LoadDirectory("plans", "*.json", options));

            StringAssert.Contains("plans/missing.json", exception.Message);
        }

        [Test]
        public void AreaEventArgs_PreservesSkillRuntimeAcrossOriginAndContextSource()
        {
            var handle = new MobaSkillCastRuntimeHandle(runtimeId: 41, generation: 2, rootTraceContextId: 1001);
            var payload = new AreaEventArgs
            {
                EventId = "area.delay",
                TemplateId = 40020301,
                OwnerActorId = 7,
                SourceContextId = 1002,
                RootContextId = 1001,
                OwnerContextId = 1002,
                SkillRuntimeHandle = handle,
            };

            Assert.IsTrue(payload.TryGetOrigin(out var origin));
            Assert.AreEqual(handle, origin.SkillRuntimeHandle);
            Assert.IsTrue(payload.TryGetContextSource(out var source));
            Assert.AreEqual(handle, source.SkillRuntimeHandle);
        }

        private static MobaRuntimeValidationReport ValidatePayloadField<TArgs>(
            Func<int, bool> supportsField,
            string fieldName)
        {
            var fieldId = MobaBattlePayloadFields.FieldId(fieldName);
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: TriggerId,
                predicateId: new FunctionId(1),
                predicateArgs: new[] { NumericValueRef.PayloadField(fieldId) },
                actions: Array.Empty<ActionCallPlan>());

            var database = new TriggerPlanJsonDatabase();
            var record = new TriggerPlanJsonDatabase.Record(
                TriggerId,
                EventName,
                StableStringId.Get("event:" + EventName),
                TriggerPlanScope.OwnerBound,
                in plan);
            database.AddRecord(in record);

            var eventRegistry = new MobaEventSubscriptionRegistry();
            eventRegistry.RegisterExact<TArgs>(EventName);

            var payloadRegistry = new PayloadAccessorRegistry();
            var battleAccessor = new MobaBattlePayloadAccessor();
            if (typeof(TArgs) == typeof(AttackInfo))
            {
                payloadRegistry.RegisterIntAccessor<AttackInfo>(battleAccessor, supportsField);
            }
            else if (typeof(TArgs) == typeof(DamageResult))
            {
                payloadRegistry.RegisterIntAccessor<DamageResult>(battleAccessor, supportsField);
                payloadRegistry.RegisterDoubleAccessor<DamageResult>(battleAccessor, supportsField);
            }
            else
            {
                Assert.Fail("Unsupported test event args type: " + typeof(TArgs).Name);
            }

            var report = new MobaRuntimeValidationReport();
            MobaTriggerPlanIntegrityValidator.ValidateDatabase(
                database,
                eventRegistry,
                payloadRegistry,
                report);
            return report;
        }

        private static void AssertReportContainsCode(MobaRuntimeValidationReport report, string code)
        {
            for (var i = 0; i < report.Entries.Count; i++)
            {
                if (string.Equals(report.Entries[i].Code, code, StringComparison.Ordinal)) return;
            }

            Assert.Fail("Expected validation code was not reported: " + code + Environment.NewLine + report.FormatAllEntries());
        }

        private sealed class InMemoryDirectoryTextLoader : IFileSystemTextLoader
        {
            private readonly string _path;

            public InMemoryDirectoryTextLoader(string path)
            {
                _path = path;
            }

            public IEnumerable<string> GetFiles(string directory, string pattern)
            {
                return new[] { _path };
            }

            public bool TryLoad(string id, out string text)
            {
                text = null;
                return false;
            }
        }

        private static void AssertReportDoesNotContainCode(MobaRuntimeValidationReport report, string code)
        {
            for (var i = 0; i < report.Entries.Count; i++)
            {
                Assert.AreNotEqual(code, report.Entries[i].Code, report.FormatAllEntries());
            }
        }
    }
}
