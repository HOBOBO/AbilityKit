using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Room;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using AbilityKit.World.ECS;
using SnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class AuthoritativeStateRecoveryRuntimeTests
    {
        /// <summary>
        /// 在进程内把真实世界投影送入正式恢复端口，验证逻辑名单、插值视图与提交边界。
        /// </summary>
        [Test]
        public void LocalTypedStateSync_RecoversRealWorldAndPresentationFromFixedBaseline()
        {
            const int actorId = 7001;
            const int departingActorId = 7002;
            const int localActorId = 7003;
            const int projectileId = 7004;
            const int buildingId = 7010;
            const int minionId = 7011;
            const int summonId = 7012;
            var playbackPlan = CreateLocalProofPlan();
            using var sessionMarker = CreateUnstartedLogicSession(playbackPlan.World.WorldId);
            using var worlds = new LocalTypedWorldInstaller();
            var sourceWorld = worlds.CreateSourceWorld();
            var sourceActors = Resolve<MobaActorRegistry>(sourceWorld);
            var producer = Resolve<IActorProjectionProducer>(sourceWorld);
            SeedCharacter(sourceWorld, actorId, 10f);
            SeedCharacter(sourceWorld, departingActorId, 20f);
            SeedActor(sourceWorld, projectileId, MobaEntityKind.Projectile, EntityMainType.Projectile, UnitSubType.Bullet,
                new MobaActorSpawnPostSetup { SetFlyingProjectileTag = true, SetOwnerLink = true, OwnerActorId = actorId });
            SeedActor(sourceWorld, buildingId, MobaEntityKind.Building, EntityMainType.Unit, UnitSubType.Tower);
            SeedActor(sourceWorld, minionId, MobaEntityKind.Minion, EntityMainType.Unit, UnitSubType.Minion);
            SeedActor(sourceWorld, summonId, MobaEntityKind.Summon, EntityMainType.Summon, UnitSubType.None,
                new MobaActorSpawnPostSetup { SetSummonMeta = true, SummonId = 1 });

            using var fixture = new Fixture(sessionMarker, worlds);
            var rendered = fixture.Replication.InterpolationController;
            fixture.BeginGeneration(plan: playbackPlan);
            var generation = Guid.NewGuid();
            var initial = CaptureLocalActors(producer);
            var allActorIds = new[] { actorId, departingActorId, projectileId, buildingId, minionId, summonId };
            Assert.That(initial.Select(actor => actor.ActorId), Is.EqualTo(allActorIds));
            Assert.That(initial.Select(actor => (SpawnEntityKind)actor.Kind), Is.EqualTo(new[]
            {
                SpawnEntityKind.Character, SpawnEntityKind.Character, SpawnEntityKind.Projectile,
                SpawnEntityKind.Building, SpawnEntityKind.Minion, SpawnEntityKind.Summon
            }));
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 1, 0, 10, initial));
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            var playbackWorld = fixture.PlaybackWorld;
            Assert.That(playbackWorld, Is.Not.Null.And.Not.SameAs(sourceWorld));
            var playbackActors = Resolve<MobaActorRegistry>(playbackWorld);
            AssertPosition(playbackActors, actorId, 10f);
            Assert.That(playbackActors.Entries.Select(entry => entry.Key),
                Is.EquivalentTo(new[] { actorId, departingActorId, projectileId }));
            AssertProjectedActors(fixture, allActorIds);

            SeedCharacter(playbackWorld, localActorId, 40f);
            fixture.Replication.LastServerAckFrame = 8;
            SeedCharacter(sourceWorld, actorId, 12f);
            var changed = new[] { ToTypedSnapshotActor(producer.ExtractFull(actorId)) };
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 3, 1, 12, changed));
            AssertPosition(playbackActors, actorId, 12f);
            Assert.That(fixture.Replication.LastServerAckFrame, Is.EqualTo(8));
            AssertProjectedActors(fixture, allActorIds);

            SeedCharacter(sourceWorld, actorId, 14f);
            changed = new[] { ToTypedSnapshotActor(producer.ExtractFull(actorId)) };
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 5, 4, 14, changed));
            Assert.That(fixture.Transport.FullStateRequests, Is.EqualTo(new[] { ("snapshot-fixed-baseline-mismatch", 12) }));
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 6, 1, 15, changed));
            AssertPosition(playbackActors, actorId, 12f);
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(12));
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            foreach (var retiredId in allActorIds.Skip(1)) sourceActors.Unregister(retiredId);
            var recovered = CaptureLocalActors(producer);
            Assert.That(recovered.Select(actor => actor.ActorId), Is.EqualTo(new[] { actorId }));
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 7, 0, 16, recovered));
            Assert.That(fixture.PlaybackWorld, Is.SameAs(playbackWorld));
            AssertPosition(playbackActors, actorId, 14f);
            AssertPosition(playbackActors, localActorId, 40f);
            Assert.That(playbackActors.TryGet(departingActorId, out var retired), Is.True);
            Assert.That(retired.hasActorDespawnRequest, Is.True);
            Assert.That(playbackActors.TryGet(projectileId, out var retiredProjectile), Is.True);
            Assert.That(retiredProjectile.hasActorDespawnRequest, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            AssertProjectedActors(fixture, actorId);

            SeedCharacter(sourceWorld, actorId, 17f);
            var accepted = new[] { ToTypedSnapshotActor(producer.ExtractFull(actorId)) };
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 8, 7, 17, accepted));
            AssertPosition(playbackActors, actorId, 17f);
            var unsupported = ActorSnapshot(7999, 0f, (SpawnEntityKind)99);
            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 9, 0, 18, new[] { accepted[0], unsupported }));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(17));
            Assert.That(rendered.BufferedRemoteSnapshotCount, Is.Zero);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.PlaybackWorld, Is.Null);
            Assert.That(fixture.Transport.FullStateRequests.Last(), Is.EqualTo(("state-apply-failed", 18)));

            fixture.Runtime.HandleSnapshot(LocalFixedSnapshot(generation, 9, 0, 18, accepted));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(18));
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            Assert.That(fixture.PlaybackWorld, Is.Not.Null.And.Not.SameAs(playbackWorld));
            AssertPosition(Resolve<MobaActorRegistry>(fixture.PlaybackWorld), actorId, 17f);
            AssertProjectedActors(fixture, actorId);
        }

        /// <summary>
        /// 只有房间声明的权威状态流使用局部逻辑名单，结算帧与未声明的入口不启用该范围。
        /// </summary>
        [TestCase(BattleReplicationRuntime.SyncProfileName, true)]
        [TestCase(AbilityKit.Protocol.Moba.SettledFrames.SettledFrameProtocol.CapabilityProfile, false)]
        [TestCase(null, false)]
        public void ScopedLogicImport_UsesOnlyTheAuthoritativeRoomProfile(string profileName, bool scoped)
        {
            var remote = CreateRemoteCapabilities(profileName);
            var options = new BattleStartPlanOptions(default, BattleHostMode.GatewayRemote,
                default, default, default, default, default, default, default, remoteSyncCapabilities: remote);
            var plan = new BattleStartPlan(in options);
            var actors = new[]
            {
                ActorSnapshot(1, 0f), ActorSnapshot(2, 0f, SpawnEntityKind.Projectile),
                ActorSnapshot(3, 0f, SpawnEntityKind.Minion), ActorSnapshot(4, 0f, SpawnEntityKind.Summon),
                ActorSnapshot(5, 0f, SpawnEntityKind.Building)
            };

            Assert.That(BattleAuthoritativeWorldRecoveryPort.UsesScopedLogicImport(in plan), Is.EqualTo(scoped));
            var imports = BattleAuthoritativeWorldRecoveryPort.CreateLogicImports(actors, scoped);
            Assert.That(imports.Select(actor => actor.ActorId),
                Is.EqualTo(scoped ? new[] { 1, 2 } : new[] { 1, 2, 3, 4, 5 }));
            Assert.That(actors.Select(actor => actor.ActorId), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        private static RoomGatewayNetworkSyncCapabilities CreateRemoteCapabilities(string profileName) =>
            profileName == null ? null : RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
                new WireNetworkSyncCapabilities
                {
                    MetadataVersion = 1,
                    ProfileName = profileName,
                    MinimumSchemaVersion = 0,
                    MaximumSchemaVersion = GatewayStateSyncSnapshot.CurrentSchemaVersion,
                    ClientPlayback = (int)ClientPlaybackCapabilities.AuthoritativeInterpolation,
                    Input = (int)InputPolicy.ImmediateSubmit,
                    Snapshot = (int)SnapshotPolicy.FullSnapshot,
                    Interest = (int)InterestPolicy.AllEntities,
                    Recovery = (int)RecoveryPolicy.RequestFullSnapshot,
                    ServerValidation = (int)ServerValidationPolicy.AuthoritativeOnly
                });

        /// <summary>
        /// 完整基线保留表现对象，全量轮换后按新视图退出；失败候选不发布也不提交。
        /// </summary>
        [Test]
        public void FixedBaseline_PresentationRetainsViewActorsAndRetiresThemOnlyAfterSuccess()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            var generation = Guid.NewGuid();
            var actors = new[]
            {
                ActorSnapshot(7, 1f), ActorSnapshot(8, 2f, SpawnEntityKind.Minion),
                ActorSnapshot(9, 3f, SpawnEntityKind.Summon), ActorSnapshot(10, 4f, SpawnEntityKind.Building)
            };
            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 1, 0, 10, actors: actors));

            var playback = fixture.Replication.InterpolationController;
            playback.Tick(1f);
            Assert.That(playback.TryProjectRemoteFrame(out var projected), Is.True);
            Assert.That(projected.Actors.Select(actor => actor.ActorId), Is.EqualTo(new[] { 7, 8, 9, 10 }));

            fixture.World.ApplySucceeds = false;
            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 2, 0, 11));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(10));
            Assert.That(playback.BufferedRemoteSnapshotCount, Is.Zero);

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 2, 0, 11));
            playback.Tick(1f);
            Assert.That(playback.TryProjectRemoteFrame(out projected), Is.True);
            Assert.That(projected.Actors.Select(actor => actor.ActorId), Is.EqualTo(new[] { 7 }));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(11));
            Assert.That(fixture.Runtime.PendingStateImport, Is.False);
        }

        [Test]
        public void RecoveryGenerationOperation_CancelsAndWaitsForReservedExecution()
        {
            var operation = new RecoveryGenerationOperation();
            operation.Begin();
            var generation = operation.Generation;
            Assert.That(operation.TryReserve(out var token, out var reservation), Is.True);

            var stopped = operation.EndAsync();
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(operation.IsCurrent(generation), Is.False);
            Assert.That(stopped.IsCompleted, Is.False);

            operation.Begin();
            Assert.That(operation.IsCurrent(operation.Generation), Is.True);
            reservation.SetResult(true);
            stopped.GetAwaiter().GetResult();
            Assert.That(operation.IsCurrent(operation.Generation), Is.True);
            operation.EndAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void FullBaseline_ImportsWorldAndEnablesGameplayInput()
        {
            using var fixture = new Fixture();
            var firstFrames = 0;
            fixture.BeginGeneration(firstFrameReceived: () => firstFrames++);
            var snapshot = FullSnapshot(frame: 10, eventWatermark: 4);

            fixture.Runtime.HandleSnapshot(snapshot);

            Assert.That(fixture.World.ImportedFrames, Is.EqualTo(new[] { 10 }));
            Assert.That(fixture.Runtime.PendingStateImport, Is.False);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            Assert.That(firstFrames, Is.EqualTo(1));
        }

        [Test]
        public void ImportFailure_KeepsInputBlockedAndRequestsFullState()
        {
            using var fixture = new Fixture();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();

            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 12));

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Transport.FullStateRequests[0].Reason, Is.EqualTo("state-import-failed"));
            Assert.That(fixture.Transport.FullStateRequests[0].Frame, Is.EqualTo(12));
        }

        /// <summary>
        /// 固定基线失配后，即使增量引用原先正确的全量，也必须等待全量成功导入。
        /// </summary>
        [Test]
        public void FixedBaselineMismatch_RejectsPreviouslyMatchingDeltaUntilFullCommits()
        {
            using var fixture = new Fixture();
            var generation = Guid.NewGuid();
            var publishedFrames = 0;
            fixture.BeginGeneration(firstFrameReceived: () => publishedFrames++);
            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 1, 0, 10));

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 3, 2, 12));

            Assert.That(fixture.Transport.FullStateRequests, Is.EqualTo(new[]
            {
                ("snapshot-fixed-baseline-mismatch", 10)
            }));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Replication.SnapshotAdmission.HasBaseline, Is.False);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 4, 1, 13));

            Assert.That(fixture.World.ImportedFrames, Is.EqualTo(new[] { 10 }));
            Assert.That(fixture.World.AppliedSnapshots, Is.Empty);
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(10));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(publishedFrames, Is.EqualTo(1));

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 5, 0, 14));

            Assert.That(fixture.World.ImportedFrames, Is.EqualTo(new[] { 10, 14 }));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(14));
            Assert.That(fixture.Runtime.PendingStateImport, Is.False);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 6, 5, 15));

            Assert.That(fixture.World.AppliedSnapshots, Has.Count.EqualTo(1));
            Assert.That(fixture.World.AppliedSnapshots[0].Frame, Is.EqualTo(15));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(15));
            Assert.That(publishedFrames, Is.EqualTo(3));
        }

        /// <summary>
        /// 固定基线版本拒绝保持成功游标，并按现有会话策略请求全量。
        /// </summary>
        [Test]
        public void FixedBaselineUnsupportedVersion_RequestsFullWithoutApplying()
        {
            using var fixture = new Fixture();
            var generation = Guid.NewGuid();
            var publishedFrames = 0;
            fixture.BeginGeneration(firstFrameReceived: () => publishedFrames++);
            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 1, 0, 10));

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 2, 0, 11,
                schemaVersion: GatewayStateSyncSnapshot.CurrentSchemaVersion + 1));

            Assert.That(fixture.World.ImportedFrames, Is.EqualTo(new[] { 10 }));
            Assert.That(fixture.World.AppliedSnapshots, Is.Empty);
            Assert.That(publishedFrames, Is.EqualTo(1));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(10));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Transport.FullStateRequests, Is.EqualTo(new[]
            {
                ("snapshot-admission:UnsupportedSchemaVersion", 10)
            }));
        }

        /// <summary>
        /// 应用失败不得移动已应用游标或阻止有效全量恢复。
        /// </summary>
        [TestCase("apply")]
        [TestCase("import")]
        [TestCase("event-baseline")]
        public void FixedBaselineFailure_AllowsEarlierRecoverableFull(string failure)
        {
            using var fixture = new Fixture();
            var generation = Guid.NewGuid();
            var publishedFrames = 0;
            fixture.BeginGeneration(firstFrameReceived: () => publishedFrames++);
            var applyingDelta = failure == "apply";
            if (applyingDelta)
            {
                fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 1, 0, 10));
                fixture.World.ApplySucceeds = false;
            }
            fixture.World.ImportSucceeds = failure != "import";

            fixture.Runtime.HandleSnapshot(FixedSnapshot(generation, 3, applyingDelta ? 1 : 0, 12,
                failure == "event-baseline" ? string.Empty : "epoch-1"));

            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(publishedFrames, Is.EqualTo(applyingDelta ? 1 : 0));
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.Transport.FullStateRequests[0].Reason,
                Is.EqualTo(applyingDelta ? "state-apply-failed" : "state-import-failed"));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(applyingDelta ? 10 : 0));

            fixture.World.ApplySucceeds = true;
            fixture.World.ImportSucceeds = true;
            var earlier = FixedSnapshot(generation, 2, 0, 11);
            Assert.That(fixture.Replication.AuthoritativeSnapshotState.PrepareFixed(in earlier, out _),
                Is.EqualTo(MobaSnapshotAdmissionStatus.Accepted));
            var importsBeforeRetry = fixture.World.ImportedFrames.Count;
            fixture.Runtime.HandleSnapshot(earlier);
            Assert.That(fixture.World.ImportedFrames, Has.Count.EqualTo(importsBeforeRetry + 1));
            Assert.That(fixture.World.ImportedFrames[importsBeforeRetry], Is.EqualTo(11));
            Assert.That(fixture.Replication.SnapshotAdmission.LastAcceptedFrame, Is.EqualTo(11));
            Assert.That(fixture.Runtime.PendingStateImport, Is.False);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.True);
            Assert.That(publishedFrames, Is.EqualTo(applyingDelta ? 2 : 1));
        }

        [Test]
        public void IncrementalSnapshot_AppliesMaterializedAuthoritativeStateAfterBaseline()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(
                frame: 20,
                actors: new[]
                {
                    ActorSnapshot(actorId: 7, x: 1f),
                    ActorSnapshot(actorId: 9, x: 2f)
                }));
            var incremental = new GatewayStateSyncSnapshot(
                1UL,
                21,
                0d,
                false,
                new[] { ActorSnapshot(actorId: 7, x: 3f) },
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                new[] { 9 },
                0L,
                "epoch-1");

            fixture.Runtime.HandleSnapshot(incremental);

            Assert.That(fixture.World.AppliedSnapshots, Has.Count.EqualTo(1));
            var applied = fixture.World.AppliedSnapshots[0];
            Assert.That(applied.Frame, Is.EqualTo(21));
            Assert.That(applied.IsFullSnapshot, Is.True);
            Assert.That(applied.RemovedActorIds, Is.Empty);
            Assert.That(applied.Actors, Has.Length.EqualTo(1));
            Assert.That(applied.Actors[0].ActorId, Is.EqualTo(7));
            Assert.That(applied.Actors[0].X, Is.EqualTo(3f));
        }

        [Test]
        public void AuthoritativeStateApplyFailure_RequestsFullState()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 20));
            fixture.World.ApplySucceeds = false;

            fixture.Runtime.HandleSnapshot(new GatewayStateSyncSnapshot(
                1UL,
                21,
                0d,
                false,
                new[] { ActorSnapshot(actorId: 7, x: 3f) },
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                Array.Empty<int>(),
                0L,
                "epoch-1"));

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.Transport.FullStateRequests[0], Is.EqualTo(("state-apply-failed", 21)));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
        }

        [Test]
        public void Reconnect_ResetInvalidatesTimelineAndClearsInputAck()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 30));
            fixture.Replication.LastServerAckFrame = 29;

            fixture.Runtime.HandleConnectionClosed();
            fixture.Runtime.HandleConnectionEstablished();

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.World.ResetCount, Is.EqualTo(1));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Replication.LastServerAckFrame, Is.Zero);
            Assert.That(
                fixture.Transport.FullStateRequests[0].Reason,
                Is.EqualTo("connection-re-established"));
            Assert.That(
                fixture.Runtime.RecoveryDecision.Action,
                Is.EqualTo(NetworkSessionRecoveryAction.RequestFullSnapshot));
        }

        /// <summary>
        /// 使用真实护盾与本地编码的固定基线，验证接纳后处理、幂等重建及到期清理。
        /// </summary>
        [Test]
        public void AnnieE_StateSyncReconnectRebuildsShieldThenRemovesItOnExpiry()
        {
            const int shieldVfxId = 90033004;
            using var source = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30030401 }, worldId: "annie_e_reconnect_source", heroId: 3003, attributeTemplateId: 3003);
            source.EnterGameAndWarmup();
            var actorId = source.AssertPlayerActorBound();
            var producer = Resolve<IActorProjectionProducer>(source.World);
            var packets = Resolve<IWorldStateSnapshotBatchProvider>(source.World);
            var cues = Resolve<MobaPresentationCueSnapshotService>(source.World);
            var plan = CreateLocalProofPlan();
            using var session = CreateUnstartedLogicSession(plan.World.WorldId);
            using var worlds = new LocalTypedWorldInstaller();
            using var fixture = new Fixture(session, worlds);
            fixture.BeginGeneration(plan: plan);
            var generation = Guid.NewGuid();
            var playback = new EntityWorld();
            var root = playback.Create("annie-reconnect-vfx");
            var actorView = playback.Create("annie-reconnect-target");
            actorView.WithRef(new BattleTransformComponent { Forward = Vector3.forward });
            var lookup = new BattleEntityLookup();
            lookup.Bind(new BattleNetId(actorId), actorView);
            var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
            Assert.IsNotNull(vfxDb);
            var vfx = new BattleVfxManager(vfxDb);
            fixture.Context.EntityWorld = playback;
            var sink = CreateSink();
            using var dispatcher = new SnapshotDispatcher();
            AbilityKit.Demo.Moba.Share.PresentationCueSnapshotRoute.RegisterDecoder(dispatcher);
            fixture.Context.BindSnapshotRouting(dispatcher, null, null);
            using var adapter = dispatcher.Subscribe<AbilityKit.Demo.Moba.Share.PresentationCueData[]>(
                MobaOpCodes.Snapshot.PresentationCue, (packet, entries) => sink.OnPresentationCueSnapshot(packet, entries));
            try
            {
                Import(1, 0);
                var beforeReconnect = fixture.PlaybackWorld;
                Assert.IsNotNull(beforeReconnect);
                var cast = Resolve<SkillCastCoordinator>(source.World).TryCastBySlot(actorId, 3, aimPos: default, aimDir: Vec3.Right);
                Assert.IsTrue(cast.Success, $"Annie E cast failed: {cast.FailReason}");
                for (var tick = 0; tick < 60 && ShieldViewCount() == 0; tick++)
                {
                    source.Tick(1);
                    foreach (var packet in DrainShieldPackets()) dispatcher.Feed(packet);
                }
                Assert.IsTrue(ShieldActive(), "The authority must have a real active shield.");
                Assert.That(ShieldViewCount(), Is.EqualTo(1), "The actual start Cue must play before the reconnect probe.");

                fixture.Runtime.HandleConnectionClosed();
                fixture.Runtime.HandleConnectionEstablished();
                Assert.That(fixture.Transport.FullStateRequests.Last().Reason, Is.EqualTo("connection-re-established"));
                Assert.IsTrue(fixture.Runtime.PendingStateImport);
                sink.Clear();
                sink = CreateSink();
                Assert.That(ShieldViewCount(), Is.Zero, "Recovery must be proven on a view without a cached shield.");

                var recovered = CaptureSnapshot(2, 0);
                fixture.Runtime.HandleSnapshot(recovered);
                Assert.IsFalse(fixture.Runtime.PendingStateImport);
                Assert.That(fixture.PlaybackWorld, Is.Not.Null.And.Not.SameAs(beforeReconnect));
                Assert.That(Resolve<MobaActorRegistry>(fixture.PlaybackWorld).TryGet(actorId, out _), Is.True);
                Assert.IsTrue(ShieldActive(), "The shield must not expire before the recovered full snapshot.");
                var recoveredFrame = source.FrameTime.Frame.Value;
                var rebuiltCount = ShieldViewCount();
                Assert.That(rebuiltCount, Is.EqualTo(1));
                var rebuiltViewId = root.GetChild(0).Id;
                source.Tick(1);
                Import(3, 2);
                Assert.That(ShieldViewCount(), Is.EqualTo(1));
                Assert.AreEqual(rebuiltViewId, root.GetChild(0).Id, "The next active set must reuse the recovered view.");

                var expired = false;
                for (var tick = 0; tick < 180 && !expired; tick++)
                {
                    source.Tick(1);
                    expired = DrainShieldPackets().Any(packet => MobaPresentationCueSnapshotCodec
                        .Deserialize(packet.Snapshot.Value.Payload).Any(entry => IsShield(entry) &&
                            entry.Stage == (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Expired));
                }
                Assert.IsTrue(expired, "The authority must emit a real shield expiry cue.");
                Assert.IsFalse(ShieldActive());
                Import(4, 2);
                Assert.AreEqual(source.FrameTime.Frame.Value, fixture.Replication.SnapshotAdmission.LastAcceptedFrame);
                TestContext.WriteLine($"Annie E: active at recovered frame {recoveredFrame}; expiry={source.FrameTime.Frame.Value}; " +
                    $"rebuilt shield views={rebuiltCount}; views after expiry={ShieldViewCount()}");
                Assert.That(new[] { rebuiltCount, ShieldViewCount() }, Is.EqualTo(new[] { 1, 0 }),
                    "The typed state-sync recovery path must rebuild exactly one shield and remove it at expiry.");
                fixture.Runtime.HandleSnapshot(recovered);
                Assert.That(ShieldViewCount(), Is.Zero, "A stale full snapshot must not bring back the expired shield.");
            }
            finally
            {
                sink.Clear();
                vfx.Clear(root);
            }

            void Import(long id, long baseline) => fixture.Runtime.HandleSnapshot(CaptureSnapshot(id, baseline));

            GatewayStateSyncSnapshot CaptureSnapshot(long id, long baseline)
            {
                var frame = source.FrameTime.Frame;
                Assert.IsTrue(cues.TryCaptureStateSyncBaseline(frame, out var cueBaseline));
                var payload = MobaStateSyncCuePayloadCodec.Encode(0, 1, null, cueBaseline.Payload);
                var envelope = new WireStateSyncSnapshotPush
                {
                    WorldId = 1UL,
                    Frame = frame.Value,
                    IsFullSnapshot = baseline == 0,
                    Actors = CaptureLocalActors(producer).Select(actor => new WireStateSyncActorSnapshot
                    {
                        ActorId = actor.ActorId, X = actor.X, Y = actor.Y, Z = actor.Z, Rotation = actor.Rotation,
                        VelocityX = actor.VelocityX, VelocityZ = actor.VelocityZ, Hp = actor.Hp, HpMax = actor.HpMax,
                        TeamId = actor.TeamId, Kind = actor.Kind, Code = actor.Code, OwnerNetId = actor.OwnerNetId
                    }).ToList(),
                    SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion,
                    PayloadOpCode = MobaStateSyncCuePayloadCodec.OpCode,
                    Payload = FixedBaselineStateSyncProtocol.Encode(generation, id, baseline,
                        MobaStateSyncCuePayloadCodec.CarrierSchemaVersion, payload),
                    EventEpoch = "epoch-1"
                };
                return AbilityKit.Demo.Moba.Share.GatewayStateSyncSnapshotDecoder.Decode(
                    WireRoomGatewayBinary.Serialize(envelope));
            }

            BattleViewEventSink CreateSink() => new BattleViewEventSink(fixture.Context,
                new BattleEntityQuery(playback, lookup), null, vfx, root, null, null);

            bool IsShield(MobaPresentationCueSnapshotEntry entry) =>
                entry.TemplateId == shieldVfxId && entry.TargetActorId == actorId;

            bool ShieldActive() => cues.ActiveCues.Values.Any(cue => IsShield(cue.Entry));

            int ShieldViewCount() => Enumerable.Range(0, root.ChildCount).Select(root.GetChild)
                .Count(child => child.TryGetRef(out BattleVfxComponent effect) && effect.VfxId == shieldVfxId);

            IEnumerable<FramePacket> DrainShieldPackets()
            {
                var frame = source.FrameTime.Frame;
                var snapshots = new List<WorldStateSnapshot>();
                packets.CollectSnapshots(frame, snapshots, 32);
                return snapshots.Where(snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.PresentationCue)
                    .Select(snapshot => new FramePacket(source.World.Id, frame, null, snapshot));
            }
        }

        [Test]
        public void FrameworkRecoverySignal_RoutesToMobaFullStateRequest()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            var signal = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                frame: 18,
                detail: "framework-requested-resync");

            Assert.That(
                fixture.Runtime.TryReport(in signal, out var decision),
                Is.True);
            var execution = fixture.Runtime.ExecuteRecoveryDecisionAsync(decision)
                .GetAwaiter()
                .GetResult();

            Assert.That(execution.Status, Is.EqualTo(NetworkSessionRecoveryExecutionStatus.Executed));
            Assert.That(execution.HasValue, Is.True);
            Assert.That(execution.Value, Is.True);
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(
                fixture.Transport.FullStateRequests[0],
                Is.EqualTo(("framework-requested-resync", 18)));
        }

        [Test]
        public void ReliableEventResync_RepeatedInvalidationInSameWindowIsSuppressed()
        {
            using var fixture = new Fixture();
            var pendingRequest = fixture.Transport.EnqueuePendingFullStateRequest();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 24));

            Assert.That(fixture.ReliableEvents.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);
            var windowId = fixture.ReliableEvents.RecoveryWindowId;
            var invalidBatch = Push(Event(2));

            fixture.Runtime.HandleReliableEvents(invalidBatch);
            fixture.Runtime.HandleReliableEvents(invalidBatch);

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(fixture.Runtime.ReliableEventResyncSuppressedCount, Is.EqualTo(1));
            Assert.That(fixture.ReliableEvents.RecoveryWindowId, Is.EqualTo(windowId));

            pendingRequest.SetResult(false);
        }

        [Test]
        public void ReliableEventResync_NewWindowAllowsRetry()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            Assert.That(fixture.ReliableEvents.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);

            var first = Push(Event(2));
            fixture.Runtime.HandleReliableEvents(first);
            fixture.Runtime.PendingExecution.GetAwaiter().GetResult();
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));

            fixture.ReliableEvents.RequireAuthoritativeBaseline();
            Assert.That(fixture.ReliableEvents.AdoptAuthoritativeBaseline("epoch-1", 0), Is.True);
            var second = Push(Event(2));
            fixture.Runtime.HandleReliableEvents(second);
            fixture.Runtime.PendingExecution.GetAwaiter().GetResult();

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(2));
            Assert.That(fixture.Runtime.ReliableEventResyncSuppressedCount, Is.Zero);
        }

        [Test]
        public void ReplacedGeneration_StaleFrameworkDecisionCannotMutateActiveGeneration()
        {
            using var fixture = new Fixture();
            fixture.BeginGeneration();
            fixture.Runtime.HandleConnectionClosed();
            var correlationContext =
                fixture.Runtime.RecoveryDecision.Signal.CorrelationContext;
            var signal = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                SyncHealthSeverity.Error,
                frame: 22,
                correlationContext: correlationContext,
                detail: "stale-resync");
            Assert.That(
                fixture.Runtime.TryReport(in signal, out var staleDecision),
                Is.True);

            var activeTransport = new ControllableTransport();
            fixture.BeginGeneration(activeTransport);
            var execution = fixture.Runtime.ExecuteRecoveryDecisionAsync(staleDecision)
                .GetAwaiter()
                .GetResult();

            Assert.That(execution.Status, Is.EqualTo(NetworkSessionRecoveryExecutionStatus.Executed));
            Assert.That(execution.Value, Is.False);
            Assert.That(activeTransport.FullStateRequests, Is.Empty);
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
        }

        [Test]
        public void GenerationLifecycle_OwnsRemoteInterpolationFlag()
        {
            using var fixture = new Fixture();

            fixture.BeginGeneration();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.True);

            fixture.Runtime.Dispose();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);
        }

        [Test]
        public void BeginGenerationFailure_RollsBackRecoveryOwnershipAndKeepsInputBlocked()
        {
            using var fixture = new Fixture();
            fixture.World.ThrowOnConfigure = true;

            var exception = Assert.Throws<InvalidOperationException>(() => fixture.BeginGeneration());

            Assert.That(exception.Message, Is.EqualTo("configure-failure"));
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);
        }

        [Test]
        public void ReliableEventInitializationFailure_RollsBackAndAllowsRetry()
        {
            using var fixture = new Fixture();

            Assert.Throws<InvalidOperationException>(() =>
                fixture.BeginGeneration(checkpointStore: new ThrowingCheckpointStore()));

            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.False);

            fixture.BeginGeneration();

            Assert.That(fixture.Context.EnableRemoteInterpolation, Is.True);
        }

        [Test]
        public void ReplacedGeneration_StaleFullStateCompletionCannotAffectActiveGeneration()
        {
            using var fixture = new Fixture();
            var staleCompletion = fixture.Transport.EnqueuePendingFullStateRequest();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();
            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 40));
            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));

            var activeTransport = new ControllableTransport();
            fixture.World.ImportSucceeds = true;
            fixture.BeginGeneration(activeTransport);
            staleCompletion.SetResult(false);
            Task.Delay(50).GetAwaiter().GetResult();

            LogAssert.NoUnexpectedReceived();
            Assert.That(fixture.Runtime.PendingStateImport, Is.True);
            Assert.That(fixture.Context.CanSubmitGameplayInput, Is.False);
            Assert.That(activeTransport.FullStateRequests, Is.Empty);
            Assert.That(fixture.World.ResetCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StopAsync_WaitsForGenerationOwnedRecoveryExecution()
        {
            using var fixture = new Fixture();
            var completion = fixture.Transport.EnqueuePendingFullStateRequest();
            fixture.World.ImportSucceeds = false;
            fixture.BeginGeneration();

            fixture.Runtime.HandleSnapshot(FullSnapshot(frame: 50));
            var pendingExecution = fixture.Runtime.PendingExecution;
            var stopTask = fixture.Runtime.StopAsync();

            Assert.That(fixture.Transport.FullStateRequests, Has.Count.EqualTo(1));
            Assert.That(pendingExecution.IsCompleted, Is.False);
            Assert.That(stopTask.IsCompleted, Is.False);

            completion.SetResult(false);
            yield return new WaitUntil(() => stopTask.IsCompleted);
            stopTask.GetAwaiter().GetResult();

            Assert.That(pendingExecution.IsCompleted, Is.True);
            Assert.That(fixture.Runtime.PendingExecution.IsCompleted, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static BattleStartPlan CreateLocalProofPlan()
        {
            var world = new BattleStartPlanWorldOptions("typed-state-playback", "battle", "offline", "p1", 30, 0);
            var sync = new BattleStartPlanSyncOptions(BattleSyncMode.SnapshotAuthority, default, null);
            var remote = CreateRemoteCapabilities(BattleReplicationRuntime.SyncProfileName);
            var options = new BattleStartPlanOptions(in world, BattleHostMode.GatewayRemote, in sync, default,
                default, default, default, default, default, remoteSyncCapabilities: remote);
            return new BattleStartPlan(in options);
        }

        private static BattleLogicSession CreateUnstartedLogicSession(string worldId) =>
            new BattleLogicSession(new BattleLogicSessionOptions
            {
                WorldId = new WorldId(worldId),
                PlayerId = "p1",
                ScanAllLoadedAssemblies = true,
                AutoCreateWorld = false,
                AutoJoin = false,
            });

        private static T Resolve<T>(IWorld world) where T : class
        {
            Assert.That(world.Services.TryResolve<T>(out var service), Is.True, typeof(T).Name);
            return service;
        }

        private static void SeedCharacter(IWorld world, int actorId, float x)
        {
            var state = new MobaActorStateImport(actorId, x, 0f, 0f, 0f, 100f, 100f, 1,
                (int)SpawnEntityKind.Character, 1001, 0);
            Assert.That(Resolve<MobaLogicWorldStateImporter>(world).Import(new[] { state }, 0, false).Failed, Is.Zero);
        }

        private static void SeedActor(IWorld world, int actorId, MobaEntityKind kind, EntityMainType mainType,
            UnitSubType subType, MobaActorSpawnPostSetup postSetup = default)
        {
            var transform = new Transform3(new Vec3(30f, 0f, 0f), Quat.Identity, Vec3.One);
            var info = new MobaEntityInfo(actorId, kind, in transform, (Team)1, mainType, subType, default);
            var spec = new MobaActorBuildSpec(in info, default, 1, 0);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.PostSetup = postSetup;
            Assert.That(Resolve<IMobaActorSpawnService>(world).TrySpawn(in request, out var actor), Is.True, actor.Error);
        }

        private static GatewayStateSyncActorSnapshot[] CaptureLocalActors(IActorProjectionProducer producer)
        {
            var actors = new List<ActorProjectionData>();
            producer.ExtractAll(actors);
            return actors.Select(ToTypedSnapshotActor).ToArray();
        }

        private static GatewayStateSyncActorSnapshot ToTypedSnapshotActor(ActorProjectionData actor)
        {
            var yaw = Mathf.Atan2(2f * (actor.RotW * actor.RotY + actor.RotX * actor.RotZ),
                1f - 2f * (actor.RotY * actor.RotY + actor.RotZ * actor.RotZ));
            return new GatewayStateSyncActorSnapshot(actor.ActorId, actor.PosX, actor.PosY, actor.PosZ,
                yaw, actor.VelX, actor.VelZ, actor.Hp, actor.HpMax, actor.TeamId, actor.Kind, actor.Code, actor.OwnerNetId);
        }

        private static GatewayStateSyncSnapshot LocalFixedSnapshot(Guid generation, long id, long baseline,
            int frame, GatewayStateSyncActorSnapshot[] actors) =>
            FixedSnapshot(generation, id, baseline, frame, schemaVersion: 1, actors: actors);

        private static void AssertPosition(MobaActorRegistry actors, int actorId, float x)
        {
            Assert.That(actors.TryGet(actorId, out var actor), Is.True, $"Actor {actorId}");
            Assert.That(actor.transform.Value.Position.X, Is.EqualTo(x));
        }

        private static void AssertProjectedActors(Fixture fixture, params int[] actorIds)
        {
            var rendered = fixture.Replication.InterpolationController;
            rendered.Tick(1f);
            Assert.That(rendered.TryProjectRemoteFrame(out var projected), Is.True);
            Assert.That(projected.Actors.Select(actor => actor.ActorId), Is.EqualTo(actorIds));
        }

        private static GatewayStateSyncSnapshot FullSnapshot(
            int frame,
            long eventWatermark = 0L,
            GatewayStateSyncActorSnapshot[] actors = null)
        {
            return new GatewayStateSyncSnapshot(
                1UL,
                frame,
                0d,
                true,
                actors ?? Array.Empty<GatewayStateSyncActorSnapshot>(),
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                Array.Empty<int>(),
                eventWatermark,
                "epoch-1");
        }

        private static GatewayStateSyncSnapshot FixedSnapshot(Guid generation, long snapshotId, long baseSnapshotId,
            int frame, string eventEpoch = "epoch-1",
            int schemaVersion = GatewayStateSyncSnapshot.CurrentSchemaVersion,
            GatewayStateSyncActorSnapshot[] actors = null) => new GatewayStateSyncSnapshot(
            1UL, frame, 0d, baseSnapshotId == 0, actors ?? new[] { ActorSnapshot(7, frame) },
            schemaVersion, eventEpoch: eventEpoch,
            usesFixedBaseline: true, stateStreamGeneration: generation, snapshotId: snapshotId,
            baseSnapshotId: baseSnapshotId);

        private static GatewayStateSyncActorSnapshot ActorSnapshot(int actorId, float x,
            SpawnEntityKind kind = SpawnEntityKind.Character)
        {
            return new GatewayStateSyncActorSnapshot(
                actorId: actorId,
                x: x,
                y: 0f,
                z: 0f,
                rotation: 0f,
                velocityX: 0f,
                velocityZ: 0f,
                hp: 100f,
                hpMax: 100f,
                teamId: 1,
                kind: (int)kind,
                code: 1001,
                ownerNetId: actorId);
        }

        private static WireReliableBattleEventPush Push(params WireReliableBattleEvent[] events)
        {
            return new WireReliableBattleEventPush
            {
                BattleId = "battle-1",
                Epoch = "epoch-1",
                FirstAvailableSequence = events.Length > 0 ? events[0].Sequence : 0,
                Watermark = events.Length > 0 ? events[events.Length - 1].Sequence : 0,
                Events = new List<WireReliableBattleEvent>(events)
            };
        }

        private static WireReliableBattleEvent Event(long sequence)
        {
            return new WireReliableBattleEvent
            {
                EventId = $"event-{sequence}",
                BattleId = "battle-1",
                Epoch = "epoch-1",
                Sequence = sequence,
                SourceFrame = (int)sequence,
                EventType = 1
            };
        }

        private sealed class Fixture : IDisposable
        {
            private readonly NetworkTransport _networkTransport;
            private readonly ReliableBattleEventDeliveryRuntime _reliableEvents;
            private readonly BattleSessionHandles _handles;
            private readonly BattleSimulationRuntime _simulation;

            internal Fixture(BattleLogicSession session = null, IBattleSessionWorldInstaller worldInstaller = null)
            {
                Context = BattleContext.Rent();
                Replication = new BattleReplicationRuntime();
                _networkTransport = new NetworkTransport(new NetworkTransportOptions
                {
                    ConnectionFactory = () => new StubConnection()
                });
                Replication.Build(
                    _networkTransport,
                    30,
                    1UL,
                    "battle-1",
                    default,
                    _ => { },
                    _ => { },
                    () => { },
                    () => { });
                _reliableEvents = new ReliableBattleEventDeliveryRuntime(_ => Task.CompletedTask);
                IBattleAuthoritativeWorldRecoveryPort worldRecovery;
                if (session != null)
                {
                    _handles = new BattleSessionHandles { Session = session };
                    _simulation = new BattleSimulationRuntime(new BattleSessionState(), _handles,
                        worldInstaller);
                    worldRecovery = new BattleAuthoritativeWorldRecoveryPort(_simulation, _handles);
                }
                else
                {
                    World = new TrackingWorldRecoveryPort();
                    worldRecovery = World;
                }
                Transport = new ControllableTransport();
                Runtime = new AuthoritativeStateRecoveryRuntime(
                    Replication,
                    _reliableEvents,
                    worldRecovery);
            }

            internal BattleContext Context { get; }
            internal BattleReplicationRuntime Replication { get; }
            internal ReliableBattleEventDeliveryRuntime ReliableEvents => _reliableEvents;
            internal TrackingWorldRecoveryPort World { get; }
            internal IWorld PlaybackWorld => _handles?.RemoteDriven.World;
            internal ControllableTransport Transport { get; }
            internal AuthoritativeStateRecoveryRuntime Runtime { get; }

            internal void BeginGeneration(
                ControllableTransport transport = null,
                Action firstFrameReceived = null,
                IMobaReliableBattleEventCheckpointStore checkpointStore = null,
                BattleStartPlan plan = default)
            {
                Runtime.BeginGeneration(
                    transport ?? Transport,
                    checkpointStore,
                    Context,
                    in plan,
                    1f / 30f,
                    _ => 0,
                    () => false,
                    _ => { },
                    firstFrameReceived);
            }

            public void Dispose()
            {
                Runtime.Dispose();
                _simulation?.DisposeRemoteDrivenWorld();
                Replication.Dispose();
                _networkTransport.Dispose();
                BattleContext.Return(Context);
            }
        }

        /// <summary>
        /// 测试专用装配：使用真实 Entitas 世界、MOBA 服务和基线控制，不经过正式开局载荷。
        /// </summary>
        private sealed class LocalTypedWorldInstaller : IBattleSessionWorldInstaller, IDisposable
        {
            private readonly List<RemoteDrivenWorldRuntime> _ownedWorlds = new List<RemoteDrivenWorldRuntime>();

            internal IWorld CreateSourceWorld() => CreateWorld(new WorldId("typed-state-source"), "battle", null).World;

            public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
            {
                if (options.Handles.World != null) return;
                var runtimeOptions = new RemoteDrivenWorldRuntimeFactoryOptions(options.Plan, options.FixedDeltaSeconds,
                    0, false, _ => null, _ => null, options.ResolveIdealFrameLimit, null, null);
                options.Handles.BindWorldRuntime(CreateWorld(new WorldId(options.Plan.World.WorldId),
                    options.Plan.World.WorldType, RemoteDrivenRuntimeModuleFactory.Create(runtimeOptions)));
                options.ResetTickState();
            }

            public void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options) =>
                throw new NotSupportedException("This fixture only exercises snapshot-driven playback.");

            private RemoteDrivenWorldRuntime CreateWorld(WorldId worldId, string worldType, HostRuntimeModuleHost modules)
            {
                var worlds = new WorldManager(new RegistryWorldFactory(new WorldTypeRegistry().RegisterEntitasWorld(worldType)));
                var runtimeOptions = new HostRuntimeOptions();
                var runtime = new HostRuntime(worlds, runtimeOptions);
                modules?.InstallAll(runtime, runtimeOptions);

                var services = new WorldContainerBuilder();
                services.AddModule(new DefaultWorldServicesModule());
                services.RegisterType<MobaActorRegistry, MobaActorRegistry>(WorldLifetime.Singleton);
                services.RegisterType<MobaEntityManager, MobaEntityManager>(WorldLifetime.Singleton);
                services.RegisterType<IMobaActorSpawnService, MobaActorSpawnService>(WorldLifetime.Singleton);
                services.RegisterType<IActorProjectionProducer, MobaActorProjectionProducer>(WorldLifetime.Singleton);
                services.Register<MobaLogicWorldStateImporter>(WorldLifetime.Singleton, resolver =>
                    new MobaLogicWorldStateImporter(resolver.Resolve<MobaActorRegistry>(), resolver.Resolve<IMobaActorSpawnService>(), null));
                var options = new WorldCreateOptions(worldId, worldType) { ServiceBuilder = services };
                options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
                var world = new RemoteDrivenWorldRuntime(worldId, worlds, runtime, runtime.CreateWorld(options));
                _ownedWorlds.Add(world);
                return world;
            }

            public void Dispose()
            {
                foreach (var world in _ownedWorlds) world.DestroyWorld();
                _ownedWorlds.Clear();
            }
        }

        private sealed class TrackingWorldRecoveryPort : IBattleAuthoritativeWorldRecoveryPort
        {
            internal bool ImportSucceeds { get; set; } = true;
            internal bool ApplySucceeds { get; set; } = true;
            internal bool ThrowOnConfigure { get; set; }
            internal List<int> ImportedFrames { get; } = new List<int>();
            internal List<GatewayStateSyncSnapshot> AppliedSnapshots { get; } =
                new List<GatewayStateSyncSnapshot>();
            internal int ResetCount { get; private set; }

            public void Configure(
                in BattleStartPlan plan,
                BattleContext context,
                float fixedDeltaSeconds,
                Func<WorldId, int> resolveIdealFrameLimit,
                Func<bool> shouldForceHashMismatch)
            {
                if (ThrowOnConfigure)
                {
                    throw new InvalidOperationException("configure-failure");
                }
            }

            public bool TryImport(in GatewayStateSyncSnapshot snapshot)
            {
                ImportedFrames.Add(snapshot.Frame);
                return ImportSucceeds;
            }

            public bool TryApplyAuthoritativeState(in GatewayStateSyncSnapshot snapshot)
            {
                AppliedSnapshots.Add(snapshot);
                return ApplySucceeds;
            }

            public void ResetAfterReconnect()
            {
                ResetCount++;
            }
        }

        private sealed class ThrowingCheckpointStore : IMobaReliableBattleEventCheckpointStore
        {
            public bool TryLoad(
                string battleId,
                out MobaReliableBattleEventCheckpoint checkpoint)
            {
                throw new InvalidOperationException("checkpoint-load-failure");
            }

            public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
            {
            }
        }

        private sealed class ControllableTransport : IBattleRecoveryTransportOperations
        {
            private readonly Queue<Func<Task<bool>>> _fullStateRequests =
                new Queue<Func<Task<bool>>>();

            internal List<(string Reason, int Frame)> FullStateRequests { get; } =
                new List<(string Reason, int Frame)>();

            internal TaskCompletionSource<bool> EnqueuePendingFullStateRequest()
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _fullStateRequests.Enqueue(() => completion.Task);
                return completion;
            }

            public Task<long> AcknowledgeReliableEventsAsync(string epoch, long sequence)
            {
                return Task.FromResult(sequence);
            }

            public Task<bool> RequestFullStateSyncAsync(
                string reason,
                int lastAuthoritativeFrame)
            {
                FullStateRequests.Add((reason, lastAuthoritativeFrame));
                return _fullStateRequests.Count > 0
                    ? _fullStateRequests.Dequeue().Invoke()
                    : Task.FromResult(true);
            }

            public void Disconnect()
            {
            }
        }

        private sealed class StubConnection : IConnection
        {
            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                State = ConnectionState.Connected;
                Connected?.Invoke();
            }

            public void Close()
            {
                State = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }

            public void Tick(float deltaTime)
            {
            }

            public void Send(
                uint opCode,
                ArraySegment<byte> payload,
                ushort flags = 0,
                uint seq = 0)
            {
            }

            public void Dispose()
            {
                Close();
            }
        }
    }
}
