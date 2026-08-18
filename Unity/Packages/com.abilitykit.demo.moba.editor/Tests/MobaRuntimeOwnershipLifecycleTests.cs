using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaRuntimeOwnershipLifecycleTests
    {
        [Test]
        public void SkillRuntime_NormalRelease_FinalizesWithExactlyOnceEvents()
        {
            var service = new MobaSkillCastRuntimeService();
            var events = new SkillLifecycleRecorder();
            service.LifecycleHooks.Register(events);
            var runtime = CreateSkillRuntime(service, 101);
            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, 1001L, 2001L, 301);

            Assert.That(service.RetainChild(runtime.Handle, child, out var retain), Is.True);
            Assert.That(service.MarkPipelineEnded(runtime.Handle, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);
            Assert.That(service.MarkPipelineEnded(runtime.Handle, MobaSkillRuntimeEndReason.PipelineCompleted), Is.False);
            Assert.That(service.ReleaseChild(retain), Is.True);

            Assert.That(service.Count, Is.Zero);
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.PipelineEnded), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.WaitingChildren), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.ChildReleased), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.Finalized), Is.EqualTo(1));
        }

        [Test]
        public void SkillRuntime_ForceTerminate_RevokesCapabilitiesAndRejectsStaleRelease()
        {
            var service = new MobaSkillCastRuntimeService();
            var events = new SkillLifecycleRecorder();
            service.LifecycleHooks.Register(events);
            var runtime = CreateSkillRuntime(service, 102);
            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, 1002L, 2002L, 302);

            Assert.That(service.RetainChild(runtime.Handle, child, out var retain), Is.True);
            Assert.That(service.ForceTerminate(runtime.Handle), Is.True);

            Assert.That(runtime.PendingChildren, Is.Zero);
            Assert.That(service.ReleaseChild(retain), Is.False);
            Assert.That(service.Count, Is.Zero);
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.ForceTerminated), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.ChildReleased), Is.EqualTo(1));
            Assert.That(events.ForcedChildReleaseCount, Is.EqualTo(1));
            Assert.That(events.FinalizedPendingChildren, Is.Zero);
        }

        [Test]
        public void SkillRuntime_Clear_FinalizesWithoutReusingCapabilityIdentity()
        {
            var service = new MobaSkillCastRuntimeService();
            var events = new SkillLifecycleRecorder();
            service.LifecycleHooks.Register(events);
            var first = CreateSkillRuntime(service, 103);
            var firstChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Summon, 1003L);
            Assert.That(service.RetainChild(first.Handle, firstChild, out var staleRetain), Is.True);

            service.Clear();
            service.Clear();
            var second = CreateSkillRuntime(service, 104);
            var secondChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Summon, 1004L);
            Assert.That(service.RetainChild(second.Handle, secondChild, out var secondRetain), Is.True);

            Assert.That(second.RuntimeId, Is.GreaterThan(first.RuntimeId));
            Assert.That(second.Generation, Is.GreaterThan(first.Generation));
            Assert.That(secondRetain.RetainId, Is.GreaterThan(staleRetain.RetainId));
            Assert.That(service.ReleaseChild(staleRetain), Is.False);
            Assert.That(service.CountPendingChildren(second.Handle), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.Cleared), Is.EqualTo(1));
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.Finalized), Is.EqualTo(1));

            service.Dispose();
        }

        [Test]
        public void SkillRuntime_RetainCapability_AuthenticatesChildIdentity()
        {
            var service = new MobaSkillCastRuntimeService();
            var runtime = CreateSkillRuntime(service, 105);
            var retainedChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, 1005L);
            var otherChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, 1006L);
            Assert.That(service.RetainChild(runtime.Handle, retainedChild, out var retain), Is.True);
            var forged = new MobaSkillRuntimeRetainHandle(retain.RetainId, retain.Runtime, otherChild);

            Assert.That(service.ReleaseChild(forged), Is.False);
            Assert.That(service.CountPendingChildren(runtime.Handle), Is.EqualTo(1));
            Assert.That(service.ReleaseChild(retain), Is.True);
        }

        [Test]
        public void ProjectileLinks_UnlinkClearAndDispose_ReleaseEachOwnedRetainOnce()
        {
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var events = new SkillLifecycleRecorder();
            skillRuntimes.LifecycleHooks.Register(events);
            var runtime = CreateSkillRuntime(skillRuntimes, 106);
            var links = new MobaProjectileLinkService();
            Inject(links, "_skillRuntimes", skillRuntimes);

            var firstProjectile = new ProjectileId(41);
            var firstChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, firstProjectile.Value);
            Assert.That(skillRuntimes.RetainChild(runtime.Handle, firstChild, out var firstRetain), Is.True);
            links.Link(firstProjectile, 401);
            links.BindRetain(firstProjectile, firstRetain);
            links.UnlinkByProjectileId(firstProjectile);

            var consumedProjectile = new ProjectileId(42);
            var consumedChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, consumedProjectile.Value);
            Assert.That(skillRuntimes.RetainChild(runtime.Handle, consumedChild, out var consumedRetain), Is.True);
            links.Link(consumedProjectile, 402);
            links.BindRetain(consumedProjectile, consumedRetain);
            Assert.That(links.TryConsumeRetain(consumedProjectile, out var consumed), Is.True);
            Assert.That(skillRuntimes.ReleaseChild(consumed), Is.True);
            links.UnlinkByProjectileId(consumedProjectile);

            const int launcherActorId = 403;
            var launcherSource = ProjectileSourceContextBuilder.Create()
                .WithActors(1, 2)
                .WithProjectileConfig(306)
                .WithSourceContext(2403L)
                .WithRootContext(2403L)
                .WithOwnerContext(2403L)
                .WithSkillRuntime(runtime.Handle)
                .Build();
            var launcherChild = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.ProjectileLauncher, launcherActorId);
            Assert.That(skillRuntimes.RetainChild(runtime.Handle, launcherChild, out var launcherRetain), Is.True);
            links.BindLauncherSource(launcherActorId, launcherSource);
            links.BindLauncherRetain(launcherActorId, launcherRetain);

            links.Clear();
            links.Dispose();

            Assert.That(skillRuntimes.CountPendingChildren(runtime.Handle), Is.Zero);
            Assert.That(events.Count(MobaSkillRuntimeLifecycleEventKind.ChildReleased), Is.EqualTo(3));
        }

        [Test]
        public void BuffRecovery_ReleasesOldOwnershipAndTransactionallyReacquiresIt()
        {
            var contexts = new Contexts();
            var actors = new MobaActorRegistry();
            var runtimeContexts = new MobaRuntimeContextService();
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var actor = contexts.actor.CreateEntity();
            actor.AddActorId(501);
            actors.Register(501, actor);
            var parent = CreateSkillRuntime(skillRuntimes, 107);
            var oldRuntime = CreateBuffRuntime(601, 501, 701, 2501L, parent.Handle);
            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, oldRuntime.SourceContextId, oldRuntime.SourceContextId, oldRuntime.BuffId);
            Assert.That(skillRuntimes.RetainChild(parent.Handle, child, out var oldRetain), Is.True);
            oldRuntime.SkillRuntimeRetainHandle = oldRetain;
            actor.AddBuffs(new List<BuffRuntime> { oldRuntime });
            var recovery = new MobaBuffStateRecoveryProvider(actors, runtimeContexts, skillRuntimes);
            var frame = new FrameIndex(9);

            var payload = recovery.ExportState(frame);
            recovery.ImportState(frame, payload);

            Assert.That(actor.buffs.Active, Has.Count.EqualTo(1));
            var restored = actor.buffs.Active[0];
            Assert.That(restored.SkillRuntimeHandle, Is.EqualTo(parent.Handle));
            Assert.That(restored.SkillRuntimeRetainHandle.IsValid, Is.True);
            Assert.That(skillRuntimes.ReleaseChild(oldRetain), Is.False);
            Assert.That(skillRuntimes.CountPendingChildren(parent.Handle), Is.EqualTo(1));

            recovery.ImportState(frame, Array.Empty<byte>());
            Assert.That(actor.buffs.Active, Is.Null);
            Assert.That(skillRuntimes.CountPendingChildren(parent.Handle), Is.Zero);

            runtimeContexts.Dispose();
            skillRuntimes.Dispose();
            actors.Dispose();
            contexts.actor.DestroyAllEntities();
        }

        [Test]
        public void BuffRecovery_InvalidParentRuntime_FailsBeforeMutation()
        {
            var contexts = new Contexts();
            var actors = new MobaActorRegistry();
            var runtimeContexts = new MobaRuntimeContextService();
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var actor = contexts.actor.CreateEntity();
            actor.AddActorId(502);
            actors.Register(502, actor);
            var invalidParent = new MobaSkillCastRuntimeHandle(9999L, 77, 8888L);
            var existing = CreateBuffRuntime(602, 502, 702, 2502L, invalidParent);
            actor.AddBuffs(new List<BuffRuntime> { existing });
            var recovery = new MobaBuffStateRecoveryProvider(actors, runtimeContexts, skillRuntimes);
            var frame = new FrameIndex(10);

            var payload = recovery.ExportState(frame);
            var exception = Assert.Throws<InvalidOperationException>(() => recovery.ImportState(frame, payload));

            Assert.That(exception.Message, Does.Contain("parent runtime not found"));
            Assert.That(actor.buffs.Active, Has.Count.EqualTo(1));
            Assert.That(actor.buffs.Active[0], Is.SameAs(existing));
            Assert.That(skillRuntimes.Count, Is.Zero);

            runtimeContexts.Dispose();
            skillRuntimes.Dispose();
            actors.Dispose();
            contexts.actor.DestroyAllEntities();
        }

        [Test]
        public void BuffRecovery_UnsupportedVersionAndMalformedPayload_DoNotMutateLiveState()
        {
            var contexts = new Contexts();
            var actors = new MobaActorRegistry();
            var runtimeContexts = new MobaRuntimeContextService();
            var skillRuntimes = new MobaSkillCastRuntimeService();
            var actor = contexts.actor.CreateEntity();
            actor.AddActorId(503);
            actors.Register(503, actor);
            var existing = CreateBuffRuntime(603, 503, 703, 2503L, default);
            actor.AddBuffs(new List<BuffRuntime> { existing });
            var recovery = new MobaBuffStateRecoveryProvider(actors, runtimeContexts, skillRuntimes);
            var frame = new FrameIndex(11);
            var unsupported = recovery.ExportState(frame);
            Buffer.BlockCopy(
                BitConverter.GetBytes(MobaBuffStateRecoveryProvider.CurrentPayloadVersion - 1),
                0,
                unsupported,
                0,
                sizeof(int));

            Assert.Throws<InvalidOperationException>(() => recovery.PrepareRestore(frame, unsupported));
            Assert.Throws<InvalidOperationException>(() => recovery.ImportState(frame, unsupported));
            Assert.Throws<InvalidOperationException>(() => recovery.ImportState(frame, new byte[] { 0x7f, 0x01, 0x02 }));

            Assert.That(actor.buffs.Active, Has.Count.EqualTo(1));
            Assert.That(actor.buffs.Active[0], Is.SameAs(existing));
            Assert.That(skillRuntimes.Count, Is.Zero);

            runtimeContexts.Dispose();
            skillRuntimes.Dispose();
            actors.Dispose();
            contexts.actor.DestroyAllEntities();
        }

        [Test]
        public void SummonSpawn_ParentRetainFailure_RollsBackActorAndEndsSpawnTraceOnce()
        {
            using (var scope = new SummonTestScope())
            {
                var invalidHandle = new MobaSkillCastRuntimeHandle(9998L, 76, scope.RootTraceContextId);
                var source = scope.CreateSource(invalidHandle);

                Assert.That(scope.Service.TrySummon(1, SummonTestScope.SummonId, Vec3.Zero, source), Is.False);
                Assert.That(scope.ActorSpawn.RollbackCount, Is.EqualTo(1));
                Assert.That(scope.Service.ActiveCount, Is.Zero);
                Assert.That(scope.TraceEvents.SummonTraceEndedCount, Is.EqualTo(1));
                Assert.That(scope.SkillRuntimes.Count, Is.Zero);
            }
        }

        [Test]
        public void SummonClearAndDispose_ReleaseRetainAndEndActiveTraceExactlyOnce()
        {
            using (var scope = new SummonTestScope())
            {
                var parent = CreateSkillRuntime(scope.SkillRuntimes, 108, scope.RootTraceContextId);
                var source = scope.CreateSource(parent.Handle);
                Assert.That(scope.Service.TrySummon(1, SummonTestScope.SummonId, Vec3.Zero, source), Is.True);
                Assert.That(scope.SkillRuntimes.CountPendingChildren(parent.Handle), Is.EqualTo(1));

                scope.Service.Clear();
                scope.Service.Clear();
                scope.Service.Dispose();

                Assert.That(scope.SkillRuntimes.CountPendingChildren(parent.Handle), Is.Zero);
                Assert.That(scope.TraceEvents.SummonTraceEndedCount, Is.EqualTo(1));
            }
        }

        private static MobaSkillCastRuntime CreateSkillRuntime(MobaSkillCastRuntimeService service, int skillId, long rootTraceContextId = 0L)
        {
            var aimPos = Vec3.Zero;
            var aimDir = Vec3.Forward;
            var request = new MobaSkillCastRuntimeCreateRequest(skillId, 1, 1, 1, 1, 2, aimPos, aimDir, rootTraceContextId);
            return service.Create(request);
        }

        private static BuffRuntime CreateBuffRuntime(int buffId, int targetActorId, int sourceActorId, long sourceContextId, MobaSkillCastRuntimeHandle skillHandle)
        {
            var origin = new MobaGameplayOrigin(
                sourceActorId,
                targetActorId,
                MobaTraceKind.BuffApply,
                buffId,
                sourceContextId,
                sourceContextId,
                sourceContextId,
                sourceContextId,
                skillHandle);
            return new BuffRuntime
            {
                BuffId = buffId,
                Remaining = 5f,
                IntervalRemainingSeconds = 1f,
                SourceId = sourceActorId,
                StackCount = 1,
                SourceContextId = sourceContextId,
                Origin = origin,
                ContextSource = MobaContextSourceView.FromOrigin(origin, MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary.Snapshot, false, "Buff", buffId),
                SkillRuntimeHandle = skillHandle,
            };
        }

        private static void Inject(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        private sealed class SkillLifecycleRecorder : IMobaSkillRuntimeLifecycleHook
        {
            private readonly Dictionary<MobaSkillRuntimeLifecycleEventKind, int> _counts = new Dictionary<MobaSkillRuntimeLifecycleEventKind, int>();

            public int ForcedChildReleaseCount { get; private set; }
            public int FinalizedPendingChildren { get; private set; } = -1;

            public int Count(MobaSkillRuntimeLifecycleEventKind kind)
            {
                return _counts.TryGetValue(kind, out var count) ? count : 0;
            }

            public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
            {
                _counts[lifecycleEvent.Kind] = Count(lifecycleEvent.Kind) + 1;
                if (lifecycleEvent.Kind == MobaSkillRuntimeLifecycleEventKind.ChildReleased && lifecycleEvent.Forced)
                {
                    ForcedChildReleaseCount++;
                }
                if (lifecycleEvent.Kind == MobaSkillRuntimeLifecycleEventKind.Finalized)
                {
                    FinalizedPendingChildren = lifecycleEvent.Runtime.PendingChildren;
                }
            }
        }

        private sealed class SummonTestScope : IDisposable
        {
            public const int SummonId = 801;
            private const int CasterId = 1;

            private readonly Contexts _contexts = new Contexts();
            private readonly MobaActorRegistry _registry = new MobaActorRegistry();
            private readonly MobaEntityManager _entities = new MobaEntityManager(null);

            public SummonTestScope()
            {
                SkillRuntimes = new MobaSkillCastRuntimeService();
                Trace = new MobaTraceRegistry();
                TraceEvents = new TraceEventRecorder(SummonId);
                Trace.AttachDiagnosticCollector(TraceEvents);
                RootTraceContextId = Trace.CreateRootContext(MobaTraceKind.SkillCast, 901, CasterId, 0);
                var caster = _contexts.actor.CreateEntity();
                caster.AddActorId(CasterId);
                caster.AddTransform(Transform3.Identity);
                _registry.Register(CasterId, caster);
                _entities.Register(CasterId, caster, Team.None, EntityMainType.Unit, UnitSubType.Hero, default);
                ActorSpawn = new RecordingSummonActorSpawnService(_contexts.actor, _registry);
                var actorIds = new ActorIdAllocator();
                actorIds.Reset(100);

                Service = new MobaSummonService();
                Inject(Service, "_actorIds", actorIds);
                Inject(Service, "_registry", _registry);
                Inject(Service, "_entities", _entities);
                Inject(Service, "_config", CreateSummonConfigs());
                Inject(Service, "_actorSpawn", ActorSpawn);
                Inject(Service, "_frameTime", new FixedFrameTime());
                Inject(Service, "_trace", Trace);
                Inject(Service, "_skillRuntimes", SkillRuntimes);
            }

            public MobaSummonService Service { get; }
            public MobaSkillCastRuntimeService SkillRuntimes { get; }
            public MobaTraceRegistry Trace { get; }
            public TraceEventRecorder TraceEvents { get; }
            public RecordingSummonActorSpawnService ActorSpawn { get; }
            public long RootTraceContextId { get; }

            public SummonSourceContext CreateSource(MobaSkillCastRuntimeHandle handle)
            {
                var origin = new MobaGameplayOrigin(
                    CasterId,
                    0,
                    MobaTraceKind.SkillCast,
                    901,
                    RootTraceContextId,
                    RootTraceContextId,
                    RootTraceContextId,
                    RootTraceContextId,
                    handle);
                return SummonSourceContextBuilder.Create()
                    .WithActors(CasterId, 0)
                    .WithSummonConfig(SummonId)
                    .WithSourceContext(RootTraceContextId)
                    .WithRootContext(RootTraceContextId)
                    .WithOwnerContext(RootTraceContextId)
                    .WithSkillRuntime(handle)
                    .WithOrigin(origin)
                    .Build();
            }

            public void Dispose()
            {
                Service.Dispose();
                SkillRuntimes.Dispose();
                Trace.EndContext(RootTraceContextId, TraceLifecycleReason.Cancelled);
                _registry.Dispose();
                _entities.Dispose();
                _contexts.actor.DestroyAllEntities();
            }

            private static MobaConfigDatabase CreateSummonConfigs()
            {
                var configs = new MobaConfigDatabase();
                var result = configs.ReloadFromDtoArrays(
                    new Dictionary<Type, Array>
                    {
                        [typeof(SummonDTO)] = new[]
                        {
                            new SummonDTO
                            {
                                Id = SummonId,
                                Name = "runtime-ownership-summon",
                                UnitSubType = (int)UnitSubType.Minion,
                                MaxAlivePerOwner = 1,
                                AttrScales = Array.Empty<SummonAttrScaleDTO>(),
                                SkillIds = Array.Empty<int>(),
                                PassiveSkillIds = Array.Empty<int>(),
                                DefaultComponentTemplateIds = Array.Empty<int>(),
                                Tags = Array.Empty<int>(),
                            },
                        },
                    },
                    strict: false);
                Assert.That(result.Succeeded, Is.True, result.Error);
                return configs;
            }
        }

        private sealed class RecordingSummonActorSpawnService : IMobaActorSpawnService, IMobaActorSpawnTransactionService
        {
            private readonly ActorContext _actors;
            private readonly MobaActorRegistry _registry;

            public RecordingSummonActorSpawnService(ActorContext actors, MobaActorRegistry registry)
            {
                _actors = actors;
                _registry = registry;
            }

            public int RollbackCount { get; private set; }

            public bool TrySpawn(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
            {
                var entity = _actors.CreateEntity();
                entity.AddActorId(request.Spec.Info.ActorId);
                entity.AddTransform(Transform3.Identity);
                entity.AddSummonMeta(request.PostSetup.SummonId, request.PostSetup.DespawnOnOwnerDie);
                _registry.Register(request.Spec.Info.ActorId, entity);
                result = new MobaActorSpawnResult(true, request.Spec.Info.ActorId, entity, request.Spec, null);
                return true;
            }

            public bool TrySpawnUnpublished(in MobaActorSpawnRequest request, out MobaActorSpawnResult result) => TrySpawn(request, out result);
            public void Publish(in MobaActorSpawnResult result) { }

            public void Rollback(in MobaActorSpawnResult result)
            {
                RollbackCount++;
                _registry.Unregister(result.ActorId);
                if (result.Entity != null && result.Entity.isEnabled) result.Entity.Destroy();
            }

            public void Dispose() { }
        }

        private sealed class TraceEventRecorder : IMobaBattleDiagnosticEventSink
        {
            private readonly int _summonConfigId;

            public TraceEventRecorder(int summonConfigId)
            {
                _summonConfigId = summonConfigId;
            }

            public int SummonTraceEndedCount { get; private set; }

            public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
            {
                if (draft.Kind == BattleDiagnosticEventKind.TraceNodeEnded && draft.ConfigId == _summonConfigId)
                {
                    SummonTraceEndedCount++;
                }
                return true;
            }
        }

        private sealed class FixedFrameTime : IFrameTime
        {
            public FrameIndex Frame => new FrameIndex(7);
            public float DeltaTime => 1f / 30f;
            public float Time => Frame.Value * DeltaTime;
            public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
            public FrameIndex TimeToFrame(float time) => new FrameIndex((int)Math.Round(time / DeltaTime));
        }
    }
}
