using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
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
            Assert.That(detail.BlackboardEntries, Has.Count.EqualTo(3));

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
