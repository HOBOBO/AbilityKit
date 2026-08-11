using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Continuous;
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
        public void AreaEventArgs_PreservesLifecycleParentAndSkillRuntimeAcrossContextViews()
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
            Assert.AreEqual(1002L, origin.EffectiveParentContextId);
            Assert.AreEqual(1001L, origin.EffectiveRootContextId);
            Assert.AreEqual(handle, origin.SkillRuntimeHandle);

            Assert.IsTrue(payload.TryGetLineageContext(out var lineage));
            Assert.AreEqual(1002L, lineage.SourceContextId);
            Assert.AreEqual(1001L, lineage.RootContextId);

            Assert.IsTrue(payload.TryGetContextSource(out var source));
            Assert.AreEqual(1002L, source.ParentContextId);
            Assert.AreEqual(1001L, source.RootContextId);
            Assert.AreEqual(handle, source.SkillRuntimeHandle);
        }

        [Test]
        public void PersistentContextSnapshot_PreservesGenerationCheckedHandleWithoutClaimingLiveRuntime()
        {
            var handle = new MobaSkillCastRuntimeHandle(runtimeId: 41, generation: 2, rootTraceContextId: 1001);
            var liveSource = new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime,
                EffectContextKind.Skill,
                MobaTraceKind.SkillCast,
                sourceActorId: 7,
                targetActorId: 8,
                sourceContextId: 1002,
                parentContextId: 1002,
                rootContextId: 1001,
                ownerContextId: 1002,
                configId: 4001,
                triggerId: 0,
                frame: 11,
                runtimeKind: "Skill",
                runtimeConfigId: 4001,
                hasLiveRuntime: true,
                skillRuntimeHandle: handle);

            var snapshot = MobaPersistentContextSourceSnapshot.FromContextSource(in liveSource);

            Assert.IsTrue(snapshot.TryGetContextSource(out var captured));
            Assert.AreEqual(MobaContextSourceBoundary.Snapshot, captured.Boundary);
            Assert.IsFalse(captured.HasLiveRuntime);
            Assert.AreEqual(handle, captured.SkillRuntimeHandle);
            Assert.AreEqual(handle.RuntimeId, captured.SkillRuntimeHandle.RuntimeId);
            Assert.AreEqual(handle.Generation, captured.SkillRuntimeHandle.Generation);
            Assert.AreEqual(handle.RootTraceContextId, captured.SkillRuntimeHandle.RootTraceContextId);
        }

        [Test]
        public void SummonSourceContext_UsesFormalSummonContextKind()
        {
            var sourceContext = SummonSourceContextBuilder.Create()
                .WithActors(sourceActorId: 7, summonActorId: 8)
                .WithSummonConfig(50010101)
                .WithSourceContext(2002)
                .WithRootContext(2001)
                .WithOwnerContext(2002)
                .Build();

            Assert.IsTrue(sourceContext.TryGetLineageContext(out var lineage));
            Assert.AreEqual(EffectContextKind.Summon, lineage.ContextKind);
            Assert.AreEqual(MobaTraceKind.SummonSpawn, lineage.OriginKind);
            Assert.AreEqual(2002L, lineage.SourceContextId);
            Assert.AreEqual(2001L, lineage.RootContextId);
        }

        [Test]
        public void GameplayOriginBuilder_LifecycleNodeBecomesImmediateParentAndPreservesBoundaries()
        {
            var upstream = new MobaGameplayOrigin(
                sourceActorId: 7,
                targetActorId: 8,
                immediateKind: MobaTraceKind.EffectExecution,
                immediateConfigId: 3001,
                immediateContextId: 1002,
                parentContextId: 1001,
                rootContextId: 901,
                ownerContextId: 902);

            var origin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in upstream)
                .WithLifecycleNode(MobaTraceKind.ProjectileLaunch, 4001, 2001)
                .Build();

            Assert.AreEqual(MobaTraceKind.ProjectileLaunch, origin.ImmediateKind);
            Assert.AreEqual(4001, origin.ImmediateConfigId);
            Assert.AreEqual(2001L, origin.ImmediateContextId);
            Assert.AreEqual(2001L, origin.ParentContextId);
            Assert.AreEqual(2001L, origin.EffectiveParentContextId);
            Assert.AreEqual(901L, origin.EffectiveRootContextId);
            Assert.AreEqual(902L, origin.OwnerContextId);
        }

        [Test]
        public void GameplayOriginBuilder_LifecycleNodeRejectsZeroContext()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MobaGameplayOriginBuilder.Create()
                    .WithLifecycleNode(MobaTraceKind.ProjectileLaunch, 4001, 0));
        }

        [Test]
        public void GameplayOriginBuilder_LifecycleNodeCompletesMissingBoundaries()
        {
            var origin = MobaGameplayOriginBuilder.Create()
                .WithActors(sourceActorId: 7, targetActorId: 8)
                .WithLifecycleNode(MobaTraceKind.SummonSpawn, 5001, 2001)
                .Build();

            Assert.AreEqual(2001L, origin.EffectiveParentContextId);
            Assert.AreEqual(2001L, origin.EffectiveRootContextId);
            Assert.AreEqual(2001L, origin.OwnerContextId);
        }

        [Test]
        public void ProjectileSourceContextBuilder_LaunchContextDoesNotSkipLifecycleNode()
        {
            var upstream = new MobaGameplayOrigin(
                sourceActorId: 7,
                targetActorId: 8,
                immediateKind: MobaTraceKind.EffectExecution,
                immediateConfigId: 3001,
                immediateContextId: 1002,
                parentContextId: 1001,
                rootContextId: 901,
                ownerContextId: 902);

            var sourceContext = ProjectileSourceContextBuilder.Create()
                .WithActors(sourceActorId: 7, initialTargetActorId: 8)
                .WithProjectileConfig(4001)
                .WithRootContext(901)
                .WithOwnerContext(902)
                .WithOrigin(in upstream)
                .WithLaunchContext(2001)
                .Build();

            Assert.IsTrue(sourceContext.TryGetOrigin(out var origin));
            Assert.AreEqual(MobaTraceKind.ProjectileLaunch, origin.ImmediateKind);
            Assert.AreEqual(2001L, origin.ImmediateContextId);
            Assert.AreEqual(2001L, origin.EffectiveParentContextId);
            Assert.AreEqual(901L, origin.EffectiveRootContextId);
            Assert.AreEqual(902L, origin.OwnerContextId);
        }

        [Test]
        public void ProjectileLinkService_LauncherRecordConsumesRetainWithoutLosingSource()
        {
            const int launcherActorId = 31;
            var runtimeHandle = new MobaSkillCastRuntimeHandle(
                runtimeId: 41,
                generation: 2,
                rootTraceContextId: 1001);
            var child = new MobaSkillRuntimeChildRef(
                MobaSkillRuntimeChildKind.ProjectileLauncher,
                launcherActorId,
                traceContextId: 2001,
                configId: 4001);
            var retainHandle = new MobaSkillRuntimeRetainHandle(
                retainId: 51,
                in runtimeHandle,
                in child);
            var source = ProjectileSourceContextBuilder.Create()
                .WithActors(sourceActorId: 7, initialTargetActorId: 8)
                .WithProjectileConfig(4002)
                .WithSourceContext(2001)
                .WithRootContext(1001)
                .WithOwnerContext(1002)
                .WithSkillRuntime(in runtimeHandle)
                .Build();
            var links = new MobaProjectileLinkService();

            links.BindLauncherSource(launcherActorId, in source);
            links.BindLauncherRetain(launcherActorId, in retainHandle);

            Assert.IsTrue(links.TryConsumeLauncherRetain(launcherActorId, out var consumed));
            Assert.AreEqual(retainHandle, consumed);
            Assert.IsFalse(links.TryGetLauncherRetain(launcherActorId, out _));
            Assert.IsTrue(links.TryGetLauncherSource(launcherActorId, out var capturedSource));
            Assert.AreEqual(source.SourceContextId, capturedSource.SourceContextId);

            links.UnlinkLauncher(launcherActorId);

            Assert.IsFalse(links.TryGetLauncherSource(launcherActorId, out _));
            Assert.IsFalse(links.TryGetLauncherRetain(launcherActorId, out _));
        }

        [Test]
        public void SummonSourceContextBuilder_SpawnContextDoesNotSkipLifecycleNode()
        {
            var upstream = new MobaGameplayOrigin(
                sourceActorId: 7,
                targetActorId: 8,
                immediateKind: MobaTraceKind.EffectExecution,
                immediateConfigId: 3001,
                immediateContextId: 1002,
                parentContextId: 1001,
                rootContextId: 901,
                ownerContextId: 902);

            var sourceContext = SummonSourceContextBuilder.Create()
                .WithActors(sourceActorId: 7, summonActorId: 9)
                .WithSummonConfig(5001)
                .WithRootContext(901)
                .WithOwnerContext(902)
                .WithOrigin(in upstream)
                .WithSpawnContext(2001)
                .Build();

            Assert.IsTrue(sourceContext.TryGetOrigin(out var origin));
            Assert.AreEqual(MobaTraceKind.SummonSpawn, origin.ImmediateKind);
            Assert.AreEqual(2001L, origin.ImmediateContextId);
            Assert.AreEqual(2001L, origin.EffectiveParentContextId);
            Assert.AreEqual(901L, origin.EffectiveRootContextId);
            Assert.AreEqual(902L, origin.OwnerContextId);
        }

        [Test]
        public void EffectLineageResolver_ActorOnlyPayloadCreatesRootInputWithoutFakeTraceIds()
        {
            var payload = new ActorOnlyPayload(sourceActorId: 17, targetActorId: 23);

            var lineage = MobaEffectLineageInputResolver.Resolve(payload);
            var context = MobaCombatExecutionContextFactory.Create(
                payload,
                in lineage,
                default,
                frame: 11);

            Assert.AreEqual(17, lineage.SourceActorId);
            Assert.AreEqual(23, lineage.TargetActorId);
            Assert.AreEqual(0L, lineage.ParentContextId);
            Assert.AreEqual(0L, lineage.RootContextId);
            Assert.AreEqual(0L, lineage.OwnerContextId);
            Assert.IsTrue(lineage.CanCreateRootExecution);
            Assert.IsFalse(lineage.HasExecutionSource);
            Assert.IsTrue(context.IsValid);
            Assert.IsFalse(context.HasExecutionSource);

            var condition = MobaTriggerConditionContext.Create(
                in context,
                skillRuntimes: null,
                frame: 11);
            var request = condition.ToExecutionRequest(triggerId: 7001);
            Assert.AreEqual(0L, request.ParentContextId);
            Assert.AreEqual(0L, request.RootContextId);
            Assert.AreEqual(17, request.SourceActorId);
        }

        [Test]
        public void ContextSourceResolver_ConsistentFormalProvidersResolveOnce()
        {
            var source = new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Execution,
                EffectContextKind.Trigger,
                MobaTraceKind.EffectExecution,
                17,
                23,
                3002L,
                3002L,
                3001L,
                3002L,
                7001,
                9001,
                11,
                null,
                0,
                false,
                default);
            var payload = new ConsistentFormalSourcePayload(in source);

            Assert.IsTrue(payload.TryResolveContextSource(out var resolved));
            Assert.AreEqual(source.SourceActorId, resolved.SourceActorId);
            Assert.AreEqual(source.SourceContextId, resolved.SourceContextId);
            Assert.AreEqual(source.RootContextId, resolved.RootContextId);
            Assert.AreEqual(source.OwnerContextId, resolved.OwnerContextId);
        }

        [Test]
        public void ContextSourceResolver_ConflictingFormalProvidersFailFast()
        {
            var combatSource = new MobaCombatContextSource(
                EffectContextKind.Trigger,
                MobaTraceKind.EffectExecution,
                17,
                23,
                3002L,
                3001L,
                3002L,
                7001,
                triggerId: 9001,
                frame: 11);
            var conflictingSource = new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Execution,
                EffectContextKind.Trigger,
                MobaTraceKind.EffectExecution,
                17,
                23,
                4002L,
                4002L,
                4001L,
                4002L,
                7001,
                9001,
                11,
                null,
                0,
                false,
                default);
            var payload = new ConflictingFormalSourcePayload(in combatSource, in conflictingSource);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                payload.TryResolveContextSource(out _));
            StringAssert.Contains("Conflicting formal context providers", exception.Message);
        }

        [Test]
        public void CombatExecutionContext_WithExecutionRootPromotesAllFormalContextViews()
        {
            var payload = new ActorOnlyPayload(sourceActorId: 17, targetActorId: 23);
            var lineage = MobaEffectLineageInputResolver.Resolve(payload);
            var context = MobaCombatExecutionContextFactory.Create(
                payload,
                in lineage,
                default,
                frame: 11);

            var promoted = context.WithExecutionRoot(
                rootContextId: 9001L,
                effectConfigId: 7001);

            Assert.IsTrue(promoted.HasExecutionSource);
            Assert.AreEqual(9001L, promoted.ParentContextId);
            Assert.AreEqual(9001L, promoted.RootContextId);
            Assert.AreEqual(9001L, promoted.OwnerContextId);
            Assert.AreEqual(7001, promoted.ConfigId);
            Assert.AreEqual(9001L, promoted.LineageInput.ParentContextId);
            Assert.AreEqual(9001L, promoted.Origin.ImmediateContextId);
            Assert.AreEqual(9001L, promoted.ExecutionSnapshot.SourceContextId);
            Assert.IsTrue(promoted.TryGetContextSource(out var source));
            Assert.AreEqual(9001L, source.SourceContextId);
            Assert.AreEqual(9001L, source.ParentContextId);
            Assert.AreEqual(9001L, source.RootContextId);
            Assert.AreEqual(9001L, source.OwnerContextId);

            var condition = MobaTriggerConditionContext.Create(
                in promoted,
                skillRuntimes: null,
                frame: 11);
            var request = condition.ToExecutionRequest(triggerId: 7001);
            Assert.AreEqual(9001L, request.ParentContextId);
            Assert.AreEqual(9001L, request.RootContextId);
        }

        [Test]
        public void ContinuousContextLifecycleBinder_DoesNotEndBorrowedParentContext()
        {
            var trace = new MobaTraceRegistry();
            try
            {
                var parentContextId = trace.CreateRootContext(
                    MobaTraceKind.EffectExecution,
                    configId: 7001,
                    sourceActorId: 17,
                    targetActorId: 23);
                var lineage = new MobaEffectLineageInput(
                    EffectContextKind.Trigger,
                    MobaTraceKind.EffectExecution,
                    sourceActorId: 17,
                    targetActorId: 23,
                    parentContextId,
                    rootContextId: parentContextId,
                    ownerContextId: 0L,
                    originConfigId: 7001);
                var context = new MobaCombatExecutionContext(
                    payload: null,
                    lineage,
                    origin: default,
                    executionSnapshot: default,
                    skillRuntimeHandle: default,
                    frame: 0);
                var continuous = new BorrowedContextContinuous(context);
                var binderType = typeof(MobaContinuousManager).Assembly.GetType(
                    "AbilityKit.Demo.Moba.Services.MobaContinuousContextLifecycleBinder",
                    throwOnError: true);
                var binder = (IContinuousLifecycleBinder)Activator.CreateInstance(
                    binderType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { trace, null },
                    culture: null);

                binder.OnEnded(continuous, ContinuousEndReason.Completed, manager: null);

                Assert.IsTrue(trace.TryGetNodeSnapshot(parentContextId, out var snapshot));
                Assert.IsFalse(snapshot.IsEnded, "Continuous must not end its borrowed parent trace node.");
            }
            finally
            {
                trace.Dispose();
            }
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

        private sealed class ConsistentFormalSourcePayload : IMobaCombatContextSource, IMobaContextSourceProvider
        {
            private readonly MobaCombatContextSource _combatSource;
            private readonly MobaContextSourceView _source;

            public ConsistentFormalSourcePayload(in MobaContextSourceView source)
            {
                _source = source;
                _combatSource = new MobaCombatContextSource(
                    source.ContextKind,
                    source.TraceKind,
                    source.SourceActorId,
                    source.TargetActorId,
                    source.SourceContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    source.ConfigId,
                    source.TriggerId,
                    source.Frame,
                    source.SkillRuntimeHandle,
                    source.RuntimeKind,
                    source.RuntimeConfigId,
                    source.HasLiveRuntime);
            }

            public bool TryGetCombatContextSource(out MobaCombatContextSource source)
            {
                source = _combatSource;
                return source.IsValid;
            }

            public bool TryGetContextSource(out MobaContextSourceView source)
            {
                source = _source;
                return source.IsValid;
            }
        }

        private sealed class ConflictingFormalSourcePayload : IMobaCombatContextSource, IMobaContextSourceProvider
        {
            private readonly MobaCombatContextSource _combatSource;
            private readonly MobaContextSourceView _source;

            public ConflictingFormalSourcePayload(
                in MobaCombatContextSource combatSource,
                in MobaContextSourceView source)
            {
                _combatSource = combatSource;
                _source = source;
            }

            public bool TryGetCombatContextSource(out MobaCombatContextSource source)
            {
                source = _combatSource;
                return source.IsValid;
            }

            public bool TryGetContextSource(out MobaContextSourceView source)
            {
                source = _source;
                return source.IsValid;
            }
        }

        private sealed class ActorOnlyPayload : IMobaActorContextProvider
        {
            private readonly int _sourceActorId;
            private readonly int _targetActorId;

            public ActorOnlyPayload(int sourceActorId, int targetActorId)
            {
                _sourceActorId = sourceActorId;
                _targetActorId = targetActorId;
            }

            public bool TryGetSourceActorId(out int actorId)
            {
                actorId = _sourceActorId;
                return actorId > 0;
            }

            public bool TryGetTargetActorId(out int actorId)
            {
                actorId = _targetActorId;
                return actorId > 0;
            }
        }

        private sealed class BorrowedContextContinuous : MobaContinuousRuntimeBase, IMobaContinuousExecutionContextProvider
        {
            private readonly MobaCombatExecutionContext _context;
            private readonly IContinuousConfig _config = new TestContinuousConfig();

            public BorrowedContextContinuous(in MobaCombatExecutionContext context)
            {
                _context = context;
            }

            public override IContinuousConfig Config => _config;

            public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
            {
                context = _context;
                return context.IsValid;
            }

            public bool TryGetContextSource(out MobaContextSourceView source)
            {
                return _context.TryGetContextSource(out source);
            }
        }

        private sealed class TestContinuousConfig : IContinuousConfig
        {
            public string Id => "borrowed-context-test";
            public long OwnerId => 17L;
            public bool CanBeInterrupted => true;
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
