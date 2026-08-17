using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillCastRuntimeServiceDiagnosticsTests
    {
        [Test]
        public void CopyDiagnosticsTo_ExportsOnlyActiveRuntimeSnapshots()
        {
            var service = new MobaSkillCastRuntimeService();
            var aimPosition = Vec3.Zero;
            var aimDirection = Vec3.Forward;
            var firstRequest = new MobaSkillCastRuntimeCreateRequest(
                101, 1, 3, 7, 11, 22, in aimPosition, in aimDirection, 1001L);
            var secondRequest = new MobaSkillCastRuntimeCreateRequest(
                202, 2, 5, 8, 33, 44, in aimPosition, in aimDirection, 1002L);

            var endedRuntime = service.Create(in firstRequest);
            var activeRuntime = service.Create(in secondRequest);
            Assert.That(service.MarkPipelineEnded(endedRuntime.Handle.RuntimeId, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);

            var results = new List<MobaSkillRuntimeDiagnostics>();
            var copied = service.CopyDiagnosticsTo(results);

            Assert.That(copied, Is.EqualTo(1));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Handle, Is.EqualTo(activeRuntime.Handle));
            Assert.That(results[0].SkillId, Is.EqualTo(202));
            Assert.That(results[0].CasterActorId, Is.EqualTo(33));
            Assert.That(results[0].TargetActorId, Is.EqualTo(44));
            Assert.That(results[0].Handle.RootTraceContextId, Is.EqualTo(1002L));
            Assert.That(results[0].IsEnded, Is.False);
        }

        [Test]
        public void TryGetDetailDiagnostics_ExportsInputAndBlackboardValuesWithoutRuntimeReferences()
        {
            var service = new MobaSkillCastRuntimeService();
            var aimPosition = new Vec3(3f, 4f, 5f);
            var aimDirection = new Vec3(0f, 0f, 1f);
            var request = new MobaSkillCastRuntimeCreateRequest(
                303, 3, 2, 9, 55, 66, in aimPosition, in aimDirection, 2001L);
            var runtime = service.Create(in request);
            var scalarKey = new MobaSkillRuntimeBlackboardKey(
                101,
                "diagnostics.scalar",
                MobaSkillRuntimeValueKind.Int,
                MobaSkillRuntimeBlackboardScope.Effect,
                MobaSkillRuntimeBlackboardFlags.Debug,
                7);
            var actorSetKey = new MobaSkillRuntimeBlackboardKey(
                102,
                "diagnostics.targets",
                MobaSkillRuntimeValueKind.ActorIdSet);
            var contextSetKey = new MobaSkillRuntimeBlackboardKey(
                103,
                "diagnostics.contexts",
                MobaSkillRuntimeValueKind.ContextIdSet);

            var handle = runtime.Handle;
            var scalarValue = MobaSkillRuntimeValue.FromInt(12);
            Assert.That(service.SetBlackboardValue(
                in handle,
                in scalarKey,
                in scalarValue), Is.True);
            Assert.That(service.AddBlackboardActorId(in handle, in actorSetKey, 71), Is.True);
            Assert.That(service.AddBlackboardActorId(in handle, in actorSetKey, 72), Is.True);
            Assert.That(service.AddBlackboardContextId(in handle, in contextSetKey, 8001L), Is.True);

            Assert.That(service.TryGetDetailDiagnostics(in handle, out var detail), Is.True);
            Assert.That(detail.Runtime.Handle, Is.EqualTo(runtime.Handle));
            Assert.That(detail.AimPos, Is.EqualTo(aimPosition));
            Assert.That(detail.AimDir, Is.EqualTo(aimDirection));
            Assert.That(detail.BlackboardEntries.Count, Is.EqualTo(3));

            var scalar = FindEntry(detail.BlackboardEntries, scalarKey.Id);
            Assert.That(scalar.IsCollection, Is.False);
            Assert.That(scalar.Value.Kind, Is.EqualTo(MobaSkillRuntimeValueKind.Int));
            Assert.That(scalar.Value.IntValue, Is.EqualTo(12));
            Assert.That(scalar.Key.Scope, Is.EqualTo(MobaSkillRuntimeBlackboardScope.Effect));
            Assert.That(scalar.Key.OwnerModuleId, Is.EqualTo(7));

            var actors = FindEntry(detail.BlackboardEntries, actorSetKey.Id);
            Assert.That(actors.IsCollection, Is.True);
            Assert.That(actors.CollectionCount, Is.EqualTo(2));

            var contexts = FindEntry(detail.BlackboardEntries, contextSetKey.Id);
            Assert.That(contexts.IsCollection, Is.True);
            Assert.That(contexts.CollectionCount, Is.EqualTo(1));

            Assert.That(service.MarkPipelineEnded(runtime.Handle.RuntimeId, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);
            Assert.That(service.TryGetDetailDiagnostics(in handle, out _), Is.False);
        }

        [Test]
        public void AreaRuntime_Unregister_ReleasesSkillRuntimeChild()
        {
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var runtime = CreateRuntime(skillRuntimes, 404, 4001L);
            var areaRuntime = CreateAreaRuntime(skillRuntimes);
            var handle = runtime.Handle;
            var areaId = new AbilityKit.Combat.Projectile.AreaId(901);

            RegisterArea(areaRuntime, areaId, in handle, 8001L);

            Assert.That(skillRuntimes.CountPendingChildren(in handle, MobaSkillRuntimeChildKind.Area), Is.EqualTo(1));
            Assert.That(areaRuntime.Unregister(areaId), Is.True);
            Assert.That(skillRuntimes.CountPendingChildren(in handle, MobaSkillRuntimeChildKind.Area), Is.Zero);
        }

        [Test]
        public void AreaRuntime_Replacement_ReleasesPreviousSkillRuntimeChild()
        {
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var previousRuntime = CreateRuntime(skillRuntimes, 405, 4002L);
            var replacementRuntime = CreateRuntime(skillRuntimes, 406, 4003L);
            var areaRuntime = CreateAreaRuntime(skillRuntimes);
            var previousHandle = previousRuntime.Handle;
            var replacementHandle = replacementRuntime.Handle;
            var areaId = new AbilityKit.Combat.Projectile.AreaId(902);

            RegisterArea(areaRuntime, areaId, in previousHandle, 8101L);
            RegisterArea(areaRuntime, areaId, in replacementHandle, 8102L);

            Assert.That(skillRuntimes.CountPendingChildren(in previousHandle, MobaSkillRuntimeChildKind.Area), Is.Zero);
            Assert.That(skillRuntimes.CountPendingChildren(in replacementHandle, MobaSkillRuntimeChildKind.Area), Is.EqualTo(1));
        }

        [Test]
        public void AreaRuntime_Dispose_ReleasesAllSkillRuntimeChildren()
        {
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var runtime = CreateRuntime(skillRuntimes, 407, 4004L);
            var areaRuntime = CreateAreaRuntime(skillRuntimes);
            var handle = runtime.Handle;

            RegisterArea(areaRuntime, new AbilityKit.Combat.Projectile.AreaId(903), in handle, 8201L);
            RegisterArea(areaRuntime, new AbilityKit.Combat.Projectile.AreaId(904), in handle, 8202L);

            Assert.That(skillRuntimes.CountPendingChildren(in handle, MobaSkillRuntimeChildKind.Area), Is.EqualTo(2));
            areaRuntime.Dispose();
            Assert.That(skillRuntimes.CountPendingChildren(in handle, MobaSkillRuntimeChildKind.Area), Is.Zero);
        }

        [Test]
        public void DamageAttributeSource_AttributionActor_PreservesCompatibilityDefault()
        {
            var resolved = GiveDamagePlanActionModule.TryResolveAttributeSourceActorId(
                DamageAttributeSourceKind.AttributionActor,
                default,
                null,
                77,
                out var actorId,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(actorId, Is.EqualTo(77));
            Assert.That(failure, Is.Null);
            Assert.That(GiveDamageArgs.Default.AttributeSource, Is.EqualTo(DamageAttributeSourceKind.AttributionActor));
        }

        [Test]
        public void DamageAttributeSource_SkillCaster_SeparatesFormulaSourceFromAttributionActor()
        {
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var runtime = CreateRuntime(skillRuntimes, 408, 4005L, casterActorId: 91);
            var handle = runtime.Handle;

            var resolved = GiveDamagePlanActionModule.TryResolveAttributeSourceActorId(
                DamageAttributeSourceKind.SkillCaster,
                in handle,
                skillRuntimes,
                attackerActorId: 77,
                out var actorId,
                out var failure);

            Assert.That(resolved, Is.True);
            Assert.That(actorId, Is.EqualTo(91));
            Assert.That(failure, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DamageAttributeSource_SkillCaster_RejectsMissingRuntimeDependency(bool provideHandle)
        {
            var handle = provideHandle
                ? CreateRuntime(new MobaSkillCastRuntimeService(), 409, 4006L).Handle
                : default;

            var resolved = GiveDamagePlanActionModule.TryResolveAttributeSourceActorId(
                DamageAttributeSourceKind.SkillCaster,
                in handle,
                null,
                attackerActorId: 77,
                out var actorId,
                out var failure);

            Assert.That(resolved, Is.False);
            Assert.That(actorId, Is.Zero);
            Assert.That(failure, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void BuffRuntimeKey_NormalApply_DoesNotMatchDifferentSourceActor()
        {
            var request = new BuffApplyRequest
            {
                BuffId = 701,
                SourceActorId = 11,
            };
            var sameSource = new AbilityKit.Demo.Moba.Components.BuffRuntime
            {
                BuffId = 701,
                SourceId = 11,
            };
            var differentSource = new AbilityKit.Demo.Moba.Components.BuffRuntime
            {
                BuffId = 701,
                SourceId = 12,
            };

            var key = BuffRuntimeKey.MatchApplyRequest(in request);

            Assert.That(key.Matches(sameSource), Is.True);
            Assert.That(key.Matches(differentSource), Is.False);
        }

        [Test]
        public void BuffRuntimeKey_InstanceApply_PreservesSourceContextIdentity()
        {
            var request = new BuffApplyRequest
            {
                BuffId = 702,
                SourceActorId = 11,
                SourceContextId = 9001L,
            };
            var sameInstance = new AbilityKit.Demo.Moba.Components.BuffRuntime
            {
                BuffId = 702,
                SourceId = 11,
                SourceContextId = 9001L,
            };
            var differentInstance = new AbilityKit.Demo.Moba.Components.BuffRuntime
            {
                BuffId = 702,
                SourceId = 11,
                SourceContextId = 9002L,
            };

            var key = BuffRuntimeKey.MatchApplyRequest(in request);

            Assert.That(key.Matches(sameInstance), Is.True);
            Assert.That(key.Matches(differentInstance), Is.False);
        }

        [Test]
        public void ExecutionSnapshotBuilder_EnrichesMissingProvenance()
        {
            var first = CreateSnapshot(sourceActorId: 11, sourceContextId: 1001L);
            var second = CreateSnapshot(
                sourceActorId: 11,
                sourceContextId: 1001L,
                rootContextId: 1002L,
                ownerContextId: 1003L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(21L, 1, 1002L));

            var merged = MobaTriggerExecutionSnapshotBuilder.Create()
                .FromSnapshot(in first)
                .FromSnapshot(in second)
                .Build();

            Assert.That(merged.SourceActorId, Is.EqualTo(11));
            Assert.That(merged.SourceContextId, Is.EqualTo(1001L));
            Assert.That(merged.RootContextId, Is.EqualTo(1002L));
            Assert.That(merged.OwnerContextId, Is.EqualTo(1003L));
            Assert.That(merged.SkillRuntimeHandle, Is.EqualTo(second.SkillRuntimeHandle));
        }

        [TestCase("sourceActorId")]
        [TestCase("sourceContextId")]
        [TestCase("rootContextId")]
        [TestCase("ownerContextId")]
        [TestCase("skillRuntimeHandle")]
        public void ExecutionSnapshotBuilder_RejectsConflictingProvenance(string fieldName)
        {
            var first = CreateSnapshot(
                sourceActorId: 11,
                sourceContextId: 1001L,
                rootContextId: 1002L,
                ownerContextId: 1003L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(21L, 1, 1002L));
            var second = CreateSnapshot(
                sourceActorId: fieldName == "sourceActorId" ? 12 : 11,
                sourceContextId: fieldName == "sourceContextId" ? 2001L : 1001L,
                rootContextId: fieldName == "rootContextId" ? 2002L : 1002L,
                ownerContextId: fieldName == "ownerContextId" ? 2003L : 1003L,
                skillRuntimeHandle: fieldName == "skillRuntimeHandle"
                    ? new MobaSkillCastRuntimeHandle(22L, 1, 1002L)
                    : new MobaSkillCastRuntimeHandle(21L, 1, 1002L));

            var error = Assert.Throws<InvalidOperationException>(() =>
                MobaTriggerExecutionSnapshotBuilder.Create()
                    .FromSnapshot(in first)
                    .FromSnapshot(in second));

            Assert.That(error.Message, Does.Contain(fieldName));
        }

        [Test]
        public void CombatExecutionContextFactory_RejectsConflictingStandaloneSkillRuntime()
        {
            var currentSnapshot = CreateSnapshot(sourceActorId: 11, sourceContextId: 1001L);
            var currentHandle = new MobaSkillCastRuntimeHandle(21L, 1, 1001L);
            var incomingSnapshot = CreateSnapshot(
                sourceActorId: 11,
                sourceContextId: 1001L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(22L, 1, 1001L));
            var executionContext = new MobaCombatExecutionContext(
                null,
                default,
                default,
                currentSnapshot,
                currentHandle,
                0);

            var error = Assert.Throws<InvalidOperationException>(() =>
                executionContext.WithSnapshot(incomingSnapshot, 0));

            Assert.That(error.Message, Does.Contain("skillRuntimeHandle"));
        }

        [Test]
        public void SpawnAreaRuntimeDependencies_RejectMissingWorldServices()
        {
            var resolved = SpawnAreaPlanActionModule.TryResolveRuntimeDependencies(
                null,
                out var areaRuntime,
                out var trace,
                out var failure);

            Assert.That(resolved, Is.False);
            Assert.That(areaRuntime, Is.Null);
            Assert.That(trace, Is.Null);
            Assert.That(failure, Is.Not.Null.And.Not.Empty);
        }

        private static MobaTriggerExecutionSnapshot CreateSnapshot(
            int sourceActorId,
            long sourceContextId,
            long rootContextId = 0L,
            long ownerContextId = 0L,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            return new MobaTriggerExecutionSnapshot(
                EffectContextKind.Skill,
                sourceActorId,
                targetActorId: 20,
                sourceContextId,
                rootContextId,
                ownerContextId,
                triggerId: 0,
                configId: 0,
                frame: 0,
                skillRuntimeHandle);
        }

        private static MobaSkillCastRuntime CreateRuntime(
            MobaSkillCastRuntimeService skillRuntimes,
            int skillId,
            long rootContextId,
            int casterActorId = 10)
        {
            var aimPosition = Vec3.Zero;
            var aimDirection = Vec3.Forward;
            var request = new MobaSkillCastRuntimeCreateRequest(
                skillId, 1, 1, 1, casterActorId, 20, in aimPosition, in aimDirection, rootContextId);
            return skillRuntimes.Create(in request);
        }

        private static MobaAreaRuntimeService CreateAreaRuntime(MobaSkillCastRuntimeService skillRuntimes)
        {
            var areaRuntime = new MobaAreaRuntimeService();
            SetPrivateField(areaRuntime, "_skillRuntimes", skillRuntimes);
            return areaRuntime;
        }

        private static void RegisterArea(
            MobaAreaRuntimeService areaRuntime,
            AbilityKit.Combat.Projectile.AreaId areaId,
            in MobaSkillCastRuntimeHandle handle,
            long sourceContextId)
        {
            areaRuntime.RegisterSpawn(
                areaId,
                701,
                10,
                center: default,
                radius: 2f,
                collisionLayerMask: 0,
                maxTargets: 1,
                frame: 1,
                delayFrames: 0,
                sourceContextId: sourceContextId,
                rootContextId: 8000L,
                ownerContextId: sourceContextId,
                skillRuntimeHandle: handle);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static MobaSkillRuntimeBlackboardEntryDiagnostics FindEntry(
            IReadOnlyList<MobaSkillRuntimeBlackboardEntryDiagnostics> entries,
            int keyId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key.Id == keyId) return entries[i];
            }

            Assert.Fail($"Expected Blackboard entry {keyId}.");
            return default;
        }
    }
}
