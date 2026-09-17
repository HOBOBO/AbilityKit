using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Deterministic;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillEconomyServiceTests
    {
        [Test]
        public void WorldContainer_ResolvesEconomyWithoutDiagnosticSink()
        {
            var contexts = new Contexts();
            var registry = new MobaActorRegistry();
            var runtimes = new MobaSkillCastRuntimeService();
            var time = new MutableFrameTime();
            var lookup = new MobaActorLookupService(
                new ActorIdIndex(contexts),
                registry,
                new MobaEntityManager(null),
                contexts);
            using var container = new WorldContainerBuilder()
                .RegisterExternalInstance(runtimes)
                .RegisterExternalInstance(lookup)
                .RegisterExternalInstance<IFrameTime>(time)
                .RegisterType<MobaSkillEconomyService, MobaSkillEconomyService>()
                .Build();

            var economy = container.Resolve<MobaSkillEconomyService>();

            Assert.That(economy, Is.Not.Null);
            Assert.That(economy.PendingTransactionCount, Is.Zero);
        }

        [Test]
        public void SkillSnapshotCodec_RoundTripsChargeAndActorCooldownFields()
        {
            var source = new MobaSkillStateSnapshotEntry
            {
                ActorId = 10,
                SkillId = EconomyTestScope.SkillOneId,
                MaxCharges = 3,
                CurrentCharges = 1,
                ChargeRecoveryRemainingMs = 750,
                SharedCooldownRemainingMs = 400,
                GlobalCooldownRemainingMs = 200,
            };

            var restored = MobaSkillStateSnapshotCodec.Deserialize(
                MobaSkillStateSnapshotCodec.Serialize(new[] { source }));

            Assert.That(restored, Has.Length.EqualTo(1));
            Assert.That(restored[0].MaxCharges, Is.EqualTo(3));
            Assert.That(restored[0].CurrentCharges, Is.EqualTo(1));
            Assert.That(restored[0].ChargeRecoveryRemainingMs, Is.EqualTo(750));
            Assert.That(restored[0].SharedCooldownRemainingMs, Is.EqualTo(400));
            Assert.That(restored[0].GlobalCooldownRemainingMs, Is.EqualTo(200));
        }

        [Test]
        public void CancelBeforeCommit_RefundsResourceAndChargeExactlyOnce()
        {
            using var scope = new EconomyTestScope();
            var context = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var specification = scope.Reservation(maxCharges: 2, chargeRecoveryMs: 1000);

            Assert.That(scope.Economy.TryReserve(context, specification, out var failure), Is.True, failure);
            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(90)));
            Assert.That(scope.SkillOne.CurrentCharges, Is.EqualTo(1));

            Assert.That(scope.Runtimes.Cancel(context.RuntimeHandle), Is.True);
            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(100)));
            Assert.That(scope.SkillOne.CurrentCharges, Is.EqualTo(2));
            Assert.That(scope.Economy.PendingTransactionCount, Is.EqualTo(0));
            Assert.That(scope.Runtimes.Cancel(context.RuntimeHandle), Is.False);
            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(100)));
        }

        [Test]
        public void EconomyDiagnostics_RecordResourceCooldownCommitAndRefundTimeline()
        {
            using var scope = new EconomyTestScope();
            var committed = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var specification = scope.Reservation(
                cooldownMs: 800, cooldownGroup: "mobility",
                sharedCooldownMs: 600, globalCooldownMs: 300);

            Assert.That(scope.Economy.TryReserve(committed, specification, out var failure), Is.True, failure);
            Assert.That(scope.Economy.Commit(committed.RuntimeHandle, "cast.release"), Is.True);
            Assert.That(scope.Diagnostics.Drafts, Has.Count.EqualTo(2));
            Assert.That(scope.Diagnostics.Drafts[0].Payload.TryGetSkillExecution(out var reserved), Is.True);
            Assert.That(reserved.Stage, Is.EqualTo(BattleDiagnosticSkillExecutionStage.EconomyReserved));
            Assert.That(reserved.CommandId, Is.GreaterThan(0));
            Assert.That(reserved.ResourceBeforeRaw, Is.EqualTo(Fixed64.FromInt32(100).RawValue));
            Assert.That(reserved.ResourceAfterRaw, Is.EqualTo(Fixed64.FromInt32(90).RawValue));
            Assert.That(scope.Diagnostics.Drafts[1].Payload.TryGetSkillExecution(out var committedPayload), Is.True);
            Assert.That(committedPayload.Stage, Is.EqualTo(BattleDiagnosticSkillExecutionStage.EconomyCommitted));
            Assert.That(committedPayload.CommandId, Is.EqualTo(reserved.CommandId));
            Assert.That(committedPayload.CooldownMs, Is.EqualTo(800));
            Assert.That(committedPayload.SharedCooldownMs, Is.EqualTo(600));
            Assert.That(committedPayload.GlobalCooldownMs, Is.EqualTo(300));

            scope.Diagnostics.Drafts.Clear();
            var refunded = scope.CreateCast(slot: 2, skillId: EconomyTestScope.SkillTwoId);
            Assert.That(scope.Economy.TryReserve(refunded, scope.Reservation(), out failure), Is.True, failure);
            Assert.That(scope.Runtimes.Cancel(refunded.RuntimeHandle), Is.True);
            Assert.That(scope.Diagnostics.Drafts, Has.Count.EqualTo(2));
            Assert.That(scope.Diagnostics.Drafts[1].Payload.TryGetSkillExecution(out var refundPayload), Is.True);
            Assert.That(refundPayload.Stage, Is.EqualTo(BattleDiagnosticSkillExecutionStage.EconomyRefunded));
            Assert.That(refundPayload.ResourceBeforeRaw, Is.EqualTo(Fixed64.FromInt32(80).RawValue));
            Assert.That(refundPayload.ResourceAfterRaw, Is.EqualTo(Fixed64.FromInt32(90).RawValue));
        }

        [Test]
        public void CommitThenCancel_KeepsCostAndStartsAllCooldowns()
        {
            using var scope = new EconomyTestScope();
            var context = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var specification = scope.Reservation(
                cooldownMs: 800, cooldownGroup: "mobility", sharedCooldownMs: 600, globalCooldownMs: 300);

            Assert.That(scope.Economy.TryReserve(context, specification, out var failure), Is.True, failure);
            Assert.That(scope.Economy.Commit(context.RuntimeHandle, "cast.release"), Is.True);
            Assert.That(scope.Economy.Commit(context.RuntimeHandle, "cast.release.again"), Is.True);
            Assert.That(scope.Runtimes.Cancel(context.RuntimeHandle), Is.True);

            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(90)));
            Assert.That(scope.SkillOne.CooldownEndTimeMs, Is.EqualTo(800));
            Assert.That(scope.Economy.GetCooldownGroupEndTimeMs(EconomyTestScope.ActorId, "mobility"), Is.EqualTo(600));
            Assert.That(scope.Economy.GetGlobalCooldownEndTimeMs(EconomyTestScope.ActorId), Is.EqualTo(300));
            Assert.That(scope.Economy.PendingTransactionCount, Is.EqualTo(0));
        }

        [Test]
        public void PipelineCompletion_ImplicitlyCommitsUncommittedReservation()
        {
            using var scope = new EconomyTestScope();
            var context = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            Assert.That(scope.Economy.TryReserve(context, scope.Reservation(cooldownMs: 450), out _), Is.True);

            Assert.That(scope.Runtimes.MarkPipelineEnded(
                context.RuntimeHandle, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);

            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(90)));
            Assert.That(scope.SkillOne.CooldownEndTimeMs, Is.EqualTo(450));
            Assert.That(scope.Economy.PendingTransactionCount, Is.EqualTo(0));
        }

        [Test]
        public void SharedCooldownAndGlobalCooldown_BlockRelatedCastsOnly()
        {
            using var scope = new EconomyTestScope();
            var first = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var firstSpec = scope.Reservation(
                startSkillCooldown: false, cooldownGroup: "mobility", sharedCooldownMs: 600, globalCooldownMs: 300);
            Assert.That(scope.Economy.TryReserve(first, firstSpec, out _), Is.True);
            Assert.That(scope.Economy.Commit(first.RuntimeHandle), Is.True);
            scope.Runtimes.MarkPipelineEnded(first.RuntimeHandle, MobaSkillRuntimeEndReason.PipelineCompleted);

            var grouped = scope.CreateCast(slot: 2, skillId: EconomyTestScope.SkillTwoId);
            Assert.That(scope.Economy.TryReserve(grouped, scope.Reservation(startSkillCooldown: false, cooldownGroup: "mobility"), out var groupedFailure), Is.False);
            Assert.That(groupedFailure, Does.Contain("Shared cooldown"));
            scope.Runtimes.Cancel(grouped.RuntimeHandle);

            var global = scope.CreateCast(slot: 2, skillId: EconomyTestScope.SkillTwoId);
            Assert.That(scope.Economy.TryReserve(global, scope.Reservation(startSkillCooldown: false, cooldownGroup: "utility"), out var globalFailure), Is.False);
            Assert.That(globalFailure, Does.Contain("Global cooldown"));
            scope.Runtimes.Cancel(global.RuntimeHandle);

            var bypass = scope.CreateCast(slot: 2, skillId: EconomyTestScope.SkillTwoId);
            var bypassSpec = scope.Reservation(startSkillCooldown: false, cooldownGroup: "utility");
            bypassSpec.IgnoreGlobalCooldown = true;
            Assert.That(scope.Economy.TryReserve(bypass, bypassSpec, out var bypassFailure), Is.True, bypassFailure);
        }

        [Test]
        public void Charges_RecoverLazilyAtDeterministicBoundaries()
        {
            using var scope = new EconomyTestScope();
            var specification = scope.Reservation(maxCharges: 3, chargeRecoveryMs: 1000, startSkillCooldown: false);
            for (var i = 0; i < 3; i++)
            {
                var cast = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
                Assert.That(scope.Economy.TryReserve(cast, specification, out var failure), Is.True, failure);
                scope.Runtimes.MarkPipelineEnded(cast.RuntimeHandle, MobaSkillRuntimeEndReason.PipelineCompleted);
            }

            var blocked = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            Assert.That(scope.Economy.TryReserve(blocked, specification, out var blockedFailure), Is.False);
            Assert.That(blockedFailure, Does.Contain("charges"));
            scope.Runtimes.Cancel(blocked.RuntimeHandle);

            scope.Time.TimeSeconds = 0.999f;
            var early = scope.Economy.GetAvailability(EconomyTestScope.ActorId, scope.SkillOne, 1, null, false);
            Assert.That(early.Available, Is.False);
            scope.Time.TimeSeconds = 1f;
            var ready = scope.Economy.GetAvailability(EconomyTestScope.ActorId, scope.SkillOne, 1, null, false);
            Assert.That(ready.Available, Is.True);
            Assert.That(scope.SkillOne.CurrentCharges, Is.EqualTo(1));
            Assert.That(scope.SkillOne.NextChargeRecoveryTimeMs, Is.EqualTo(2000));
        }

        [Test]
        public void ConsumeOperation_AbortsWhenChannelTickCannotPay()
        {
            using var scope = new EconomyTestScope();
            scope.Mana.Current = Fixed64.FromInt32(5);
            var context = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var phase = new SkillEconomyPhase(
                new AbilityKit.Pipeline.AbilityPipelinePhaseId("channel.tick.cost"),
                new SkillEconomyPhaseDTO
                {
                    Operation = (int)SkillEconomyOperation.ConsumeResource,
                    ResourceType = (int)ResourceType.Mana,
                    ResourceAmount = 3,
                    UseResolvedResourceCost = false,
                });

            phase.Execute(context);
            Assert.That(context.IsAborted, Is.False);
            phase.Reset();
            phase.Execute(context);

            Assert.That(context.IsAborted, Is.True);
            Assert.That(context.FailReason, Does.Contain("Insufficient"));
            Assert.That(scope.Mana.Current, Is.EqualTo(Fixed64.FromInt32(2)));
        }

        [Test]
        public void EconomyRollback_RestoresActorCooldownState()
        {
            using var scope = new EconomyTestScope();
            var cast = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            var specification = scope.Reservation(startSkillCooldown: false, cooldownGroup: "rollback", sharedCooldownMs: 700, globalCooldownMs: 400);
            Assert.That(scope.Economy.TryReserve(cast, specification, out _), Is.True);
            Assert.That(scope.Economy.Commit(cast.RuntimeHandle), Is.True);
            var provider = new MobaSkillEconomyRollbackProvider(scope.Economy);
            var payload = provider.Export(new FrameIndex(0));

            scope.Time.TimeSeconds = 2f;
            var replacement = scope.CreateCast(slot: 2, skillId: EconomyTestScope.SkillTwoId);
            var replacementSpec = scope.Reservation(startSkillCooldown: false, cooldownGroup: "other", sharedCooldownMs: 50, globalCooldownMs: 50);
            Assert.That(scope.Economy.TryReserve(replacement, replacementSpec, out _), Is.True);
            Assert.That(scope.Economy.Commit(replacement.RuntimeHandle), Is.True);

            provider.Import(new FrameIndex(0), payload);

            Assert.That(scope.Economy.GetCooldownGroupEndTimeMs(EconomyTestScope.ActorId, "rollback"), Is.EqualTo(700));
            Assert.That(scope.Economy.GetCooldownGroupEndTimeMs(EconomyTestScope.ActorId, "other"), Is.EqualTo(0));
            Assert.That(scope.Economy.GetGlobalCooldownEndTimeMs(EconomyTestScope.ActorId), Is.EqualTo(400));
        }

        [Test]
        public void SkillCooldownRollback_RestoresChargeConfigurationAndProgress()
        {
            using var scope = new EconomyTestScope();
            var cast = scope.CreateCast(slot: 1, skillId: EconomyTestScope.SkillOneId);
            Assert.That(scope.Economy.TryReserve(
                cast, scope.Reservation(maxCharges: 3, chargeRecoveryMs: 900, cooldownGroup: "charges"), out _), Is.True);
            var provider = new MobaSkillCooldownRollbackProvider(scope.Registry);
            var payload = provider.Export(new FrameIndex(0));

            scope.SkillOne.MaxCharges = 9;
            scope.SkillOne.CurrentCharges = 0;
            scope.SkillOne.ChargeRecoveryMs = 1;
            scope.SkillOne.NextChargeRecoveryTimeMs = 1;
            scope.SkillOne.CooldownGroupId = 0;
            scope.SkillOne.ChargesConfigured = false;
            provider.Import(new FrameIndex(0), payload);

            Assert.That(scope.SkillOne.MaxCharges, Is.EqualTo(3));
            Assert.That(scope.SkillOne.CurrentCharges, Is.EqualTo(2));
            Assert.That(scope.SkillOne.ChargeRecoveryMs, Is.EqualTo(900));
            Assert.That(scope.SkillOne.NextChargeRecoveryTimeMs, Is.EqualTo(900));
            Assert.That(scope.SkillOne.CooldownGroupId, Is.Not.EqualTo(0));
            Assert.That(scope.SkillOne.ChargesConfigured, Is.True);
        }

        private sealed class EconomyTestScope : IDisposable
        {
            public const int ActorId = 801201;
            public const int SkillOneId = 801210;
            public const int SkillTwoId = 801211;

            private readonly Contexts _contexts = new Contexts();
            private readonly ActorIdIndex _index;
            private readonly MobaActorRegistry _registry = new MobaActorRegistry();
            private readonly MobaEntityManager _entities = new MobaEntityManager(null);
            private readonly WorldContainer _container;
            private int _sequence;

            public EconomyTestScope()
            {
                _index = new ActorIdIndex(_contexts);
                var actor = _contexts.actor.CreateEntity();
                actor.AddActorId(ActorId);
                SkillOne = NewSkill(SkillOneId);
                SkillTwo = NewSkill(SkillTwoId);
                actor.AddSkillLoadout(new[] { SkillOne, SkillTwo }, Array.Empty<PassiveSkillRuntime>());
                Mana = new ResourceState { Current = Fixed64.FromInt32(100), LastMax = Fixed64.FromInt32(100) };
                actor.AddResourceContainer(new ResourceContainer
                {
                    Map = new Dictionary<ResourceType, ResourceState> { [ResourceType.Mana] = Mana }
                }, true);
                _registry.Register(ActorId, actor);

                var lookup = new MobaActorLookupService(_index, _registry, _entities, _contexts);
                Runtimes = new MobaSkillCastRuntimeService();
                Time = new MutableFrameTime();
                Diagnostics = new DiagnosticDraftRecorder();
                Economy = new MobaSkillEconomyService(Runtimes, lookup, Time, Diagnostics);
                _container = new WorldContainerBuilder()
                    .RegisterExternalInstance(Runtimes)
                    .RegisterExternalInstance(Economy)
                    .RegisterExternalInstance<IFrameTime>(Time)
                    .Build();
            }

            public ActiveSkillRuntime SkillOne { get; }
            public ActiveSkillRuntime SkillTwo { get; }
            public ResourceState Mana { get; }
            public MobaSkillCastRuntimeService Runtimes { get; }
            public MobaSkillEconomyService Economy { get; }
            public DiagnosticDraftRecorder Diagnostics { get; }
            public MutableFrameTime Time { get; }
            public MobaActorRegistry Registry => _registry;

            public SkillPipelineContext CreateCast(int slot, int skillId)
            {
                var aim = Vec3.Zero;
                var direction = Vec3.Forward;
                var runtimeRequest = new MobaSkillCastRuntimeCreateRequest(
                    skillId, slot, 1, ++_sequence, ActorId, 0, in aim, in direction, 900000L + _sequence);
                var runtime = Runtimes.Create(in runtimeRequest);
                var request = new SkillCastRequest(skillId, slot, ActorId, 0, in aim, in direction, _container, null, null, null);
                var triggerContext = SkillCastContextBuilder.Create()
                    .FromRequest(in request)
                    .WithSkillLevel(1)
                    .WithSequence(_sequence)
                    .Build();
                var handle = runtime.Handle;
                triggerContext.RuntimeHandle = handle;
                triggerContext.RuntimeId = handle.RuntimeId;
                triggerContext.DiagnosticCommandId = 4400L + _sequence;
                triggerContext.ResolvedConfiguration = new ResolvedSkillCastConfiguration(
                    skillId, 1, ResourceType.Mana, 10, 500, true);
                var context = new SkillPipelineContext();
                context.Initialize(new object(), in request, triggerContext);
                return context;
            }

            public SkillEconomyPhaseDTO Reservation(
                int maxCharges = 1,
                int chargeRecoveryMs = 0,
                bool startSkillCooldown = true,
                int cooldownMs = 500,
                string cooldownGroup = null,
                int sharedCooldownMs = 0,
                int globalCooldownMs = 0)
            {
                return new SkillEconomyPhaseDTO
                {
                    Operation = (int)SkillEconomyOperation.ReserveCast,
                    ResourceType = (int)ResourceType.Mana,
                    ResourceAmount = 10,
                    UseResolvedResourceCost = false,
                    MaxCharges = maxCharges,
                    ChargeCost = 1,
                    ChargeRecoveryMs = chargeRecoveryMs,
                    StartSkillCooldown = startSkillCooldown,
                    SkillCooldownMs = cooldownMs,
                    UseResolvedSkillCooldown = false,
                    CooldownGroup = cooldownGroup,
                    SharedCooldownMs = sharedCooldownMs,
                    GlobalCooldownMs = globalCooldownMs,
                    RefundBeforeCommit = true,
                };
            }

            public void Dispose()
            {
                Economy.Dispose();
                Runtimes.Dispose();
                _container.Dispose();
                _index.Dispose();
                _contexts.actor.DestroyAllEntities();
                _registry.Dispose();
                _entities.Dispose();
            }

            private static ActiveSkillRuntime NewSkill(int id)
            {
                return new ActiveSkillRuntime { SkillId = id, Level = 1, MaxCharges = 1, CurrentCharges = 1 };
            }
        }

        private sealed class MutableFrameTime : IFrameTime
        {
            public float TimeSeconds;
            public FrameIndex Frame => new FrameIndex((int)Math.Round(TimeSeconds * 1000f));
            public float DeltaTime => 0.001f;
            public float Time => TimeSeconds;
            public float FrameToTime(FrameIndex frame) => frame.Value / 1000f;
            public FrameIndex TimeToFrame(float time) => new FrameIndex((int)Math.Round(time * 1000f));
        }

        private sealed class DiagnosticDraftRecorder : IMobaBattleDiagnosticEventSink
        {
            public List<MobaBattleDiagnosticEventDraft> Drafts { get; } =
                new List<MobaBattleDiagnosticEventDraft>();

            public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
            {
                Drafts.Add(draft);
                return true;
            }
        }
    }
}
