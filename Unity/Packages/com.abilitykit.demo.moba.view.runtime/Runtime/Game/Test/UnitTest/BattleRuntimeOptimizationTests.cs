using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Configuration;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services.Snapshot;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using MobaProjectileEventSnapshotEntry = AbilityKit.Protocol.Moba.StateSync.MobaProjectileEventSnapshotEntry;
using ProtocolProjectileEventKind = AbilityKit.Protocol.Moba.StateSync.ProjectileEventKind;
using BattleLogicSessionOptions = AbilityKit.Game.Battle.BattleLogicSessionOptions;
using BattleStartPlan = AbilityKit.Game.Flow.BattleStartPlan;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Network.Abstractions;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleRuntimeOptimizationTests
    {
        [Test]
        public void BattleProjectileShellPool_FirstRentCreatesOnlyRequestedInstance()
        {
            var created = 0;
            var pool = new BattleProjectileShellPool(_ =>
            {
                created++;
                return new GameObject("projectile-shell");
            }, capacityPerTemplate: 8);

            var instance = pool.Rent(30060301);

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, created, "A lazy bucket must not synchronously prewarm eight shells in the spawn frame.");
            pool.Return(instance);
            pool.Clear();
        }

        [Test]
        public void BattleVfxGameObjectPool_FirstRentCreatesOnlyRequestedInstance()
        {
            var created = 0;
            var pool = new BattleVfxGameObjectPool(_ =>
            {
                created++;
                return new GameObject("projectile-vfx");
            }, capacityPerVfxId: 16);

            Assert.IsTrue(pool.TryRent(90006003, out var instance));

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, created, "A lazy bucket must not synchronously prewarm sixteen VFX objects in the spawn frame.");
            pool.Return(90006003, instance);
            pool.Clear();
        }

        [Test]
        public void BattleProjectileViewEventHandler_ConfiguredVfxSuppressesFallbackShell()
        {
            var db = new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [90006003] = new VfxDTO { Id = 90006003, Resource = "missing/ying_zheng_sword", DurationMs = 30000 }
            });
            var manager = new BattleVfxManager(db);
            var world = new EntityWorld();
            var root = world.Create("vfxRoot");
            var lookup = new BattleEntityLookup();
            var query = new BattleEntityQuery(world, lookup);
            var ctx = BattleContext.Rent();
            ctx.EntityWorld = world;
            var shellCreates = 0;
            var shellPool = new BattleProjectileShellPool(_ =>
            {
                shellCreates++;
                return new GameObject("fallback-shell");
            });

            try
            {
                var handler = new BattleProjectileViewEventHandler(ctx, query, manager, in root, shellPool: shellPool);
                var spawn = new MobaProjectileEventSnapshotEntry
                {
                    Kind = (int)ProtocolProjectileEventKind.Spawn,
                    ProjectileId = 42,
                    ProjectileActorId = 10042,
                    TemplateId = 30060301,
                    ForwardX = 1f,
                };

                handler.HandleSnapshot(new[] { spawn });

                Assert.AreEqual(0, shellCreates, "A successfully spawned configured VFX must be the only projectile visual.");
            }
            finally
            {
                shellPool.Clear();
                if (root.IsValid) world.DestroyRecursive(root.Id);
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void DefaultBattleDebugFacade_InvokesInjectedSessionProvider()
        {
            var invoked = false;
            var facade = new DefaultBattleDebugFacade(() =>
            {
                invoked = true;
                return null;
            });

            var resolved = facade.TryGetSession(out var session);

            Assert.IsTrue(invoked);
            Assert.IsFalse(resolved);
            Assert.IsNull(session);
        }

        [Test]
        public void BattleDamageTextFormatter_FormatsDamageAndHealWithoutUnityAdapterState()
        {
            var formatter = new BattleDamageTextFormatter();

            var damageFormatted = formatter.TryFormat(-12.4f, false, out var damage);
            var healFormatted = formatter.TryFormat(0.4f, true, out var heal);
            var zeroFormatted = formatter.TryFormat(0f, false, out _);

            Assert.IsTrue(damageFormatted);
            Assert.AreEqual("-12", damage.Text);
            Assert.IsFalse(damage.IsHeal);
            Assert.IsTrue(healFormatted);
            Assert.AreEqual("+0.4", heal.Text);
            Assert.IsTrue(heal.IsHeal);
            Assert.IsFalse(zeroFormatted);
        }

        [Test]
        public void BattlePresentationCueResolver_CreatesPureSpawnRequestWithoutUnityState()
        {
            var resolver = new BattlePresentationCueResolver();
            var cue = CreatePresentationCue(
                PresentationCueStage.Started,
                requestKey: "skill-hit",
                vfxId: 3001,
                templateId: 0,
                sourceActorId: 11,
                targetActorId: 22,
                targets: new[] { 33 },
                positions: new[] { new SnapshotVec3(1f, 2f, 3f) },
                offsetX: 0.5f,
                offsetY: 1.5f,
                offsetZ: -0.25f);

            var decision = resolver.Resolve(in cue);

            Assert.AreEqual(BattlePresentationCueDecisionKind.Play, decision.Kind);
            Assert.IsFalse(decision.IsNone);
            Assert.AreEqual(3001, decision.SpawnRequest.VfxId);
            Assert.AreEqual(11, decision.SpawnRequest.SourceActorId);
            Assert.AreEqual(22, decision.SpawnRequest.TargetActorId);
            Assert.AreEqual(33, decision.SpawnRequest.FirstTargetActorId);
            Assert.IsTrue(decision.SpawnRequest.HasExplicitPosition);
            Assert.AreEqual(1f, decision.SpawnRequest.ExplicitPosition.X);
            Assert.AreEqual(2f, decision.SpawnRequest.ExplicitPosition.Y);
            Assert.AreEqual(3f, decision.SpawnRequest.ExplicitPosition.Z);
            Assert.AreEqual(0.5f, decision.SpawnRequest.Offset.X);
            Assert.AreEqual(1.5f, decision.SpawnRequest.Offset.Y);
            Assert.AreEqual(-0.25f, decision.SpawnRequest.Offset.Z);
        }

        [Test]
        public void BattlePresentationCueResolver_PreservesScaleAndRadiusNumericParam()
        {
            var resolver = new BattlePresentationCueResolver();
            var cue = CreatePresentationCue(
                PresentationCueStage.Started,
                requestKey: "buff-loop",
                vfxId: 90002003,
                templateId: 90002003,
                scale: 1.25f,
                numericParamKeys: new[] { 99, 2 },
                numericParamValues: new[] { 3f, 8f });

            var decision = resolver.Resolve(in cue);

            Assert.AreEqual(BattlePresentationCueDecisionKind.Play, decision.Kind);
            Assert.AreEqual(1.25f, decision.SpawnRequest.Scale, 0.0001f);
            Assert.AreEqual(8f, decision.SpawnRequest.Radius, 0.0001f);
        }

        [Test]
        public void BattlePresentationCueResolver_PreservesDurationOverrideForLongRunningBuffVfx()
        {
            var resolver = new BattlePresentationCueResolver();
            var cue = CreatePresentationCue(
                PresentationCueStage.Started,
                requestKey: "buff-loop",
                vfxId: 90002003,
                templateId: 90002003,
                durationMsOverride: 5000);

            var decision = resolver.Resolve(in cue);

            Assert.AreEqual(BattlePresentationCueDecisionKind.Play, decision.Kind);
            Assert.AreEqual(5000, decision.SpawnRequest.DurationMsOverride);
        }

        [Test]
        public void BattleVfxManager_UsesPositiveDurationOverrideInsteadOfResourceDuration()
        {
            var db = new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [90002003] = new VfxDTO { Id = 90002003, Resource = "missing/test_vfx", DurationMs = 700 }
            });
            var manager = new BattleVfxManager(db);
            var world = new EntityWorld();
            var root = world.Create("vfxRoot");

            try
            {
                var created = manager.TryCreateVfxEntity(
                    world,
                    root,
                    90002003,
                    default,
                    0,
                    Vector3.zero,
                    Quaternion.identity,
                    5000,
                    out var entity);

                Assert.IsTrue(created);
                Assert.IsTrue(entity.TryGetRef(out BattleVfxLifetimeComponent lifetime));
                Assert.AreEqual(5f, lifetime.ExpireAtTime - Time.time, 0.05f);
            }
            finally
            {
                world.DestroyRecursive(root.Id);
            }
        }

        [Test]
        public void BattlePresentationCueVfxSpawner_RefreshesPositiveDurationOverrideAndPreservesZeroOverride()
        {
            var context = BattleContext.Rent();
            var world = new EntityWorld();
            var entity = world.Create("cueVfx");
            var lifetime = new BattleVfxLifetimeComponent { ExpireAtTime = Time.time + 1f };
            entity.WithRef(lifetime);
            context.EntityWorld = world;

            try
            {
                var spawner = new BattlePresentationCueVfxSpawner(context, null, default);

                spawner.Update(entity.Id, scale: 1f, radius: 1f, durationMsOverride: 2500);
                Assert.AreEqual(2.5f, lifetime.ExpireAtTime - Time.time, 0.05f);

                var refreshedExpireAtTime = lifetime.ExpireAtTime;
                spawner.Update(entity.Id, scale: 1f, radius: 1f, durationMsOverride: 0);
                Assert.AreEqual(refreshedExpireAtTime, lifetime.ExpireAtTime, 0.0001f);
            }
            finally
            {
                world.DestroyRecursive(entity.Id);
                BattleContext.Return(context);
            }
        }

        [Test]
        public void BattlePresentationCueViewEventHandler_StartRefreshStop_ReusesAndDestroysVfxEntity()
        {
            const int vfxId = 90002004;
            var context = BattleContext.Rent();
            var world = new EntityWorld();
            var vfxRoot = world.Create("cueVfxRoot");
            context.EntityWorld = world;
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [vfxId] = new VfxDTO { Id = vfxId, Resource = "missing/cue_vfx", DurationMs = 500 }
            }));
            var handler = new BattlePresentationCueViewEventHandler(context, null, manager, in vfxRoot);
            var start = CreatePresentationCue(
                PresentationCueStage.Started,
                requestKey: "cue-lifecycle",
                vfxId: vfxId,
                templateId: 0,
                durationMsOverride: 1000);
            var requestKey = BattlePresentationCueRequestKey.From(in start);

            try
            {
                handler.HandleSnapshot(new[] { start });
                var vfxEntityId = GetActiveCueEntityId(handler, requestKey);
                Assert.IsTrue(world.IsAlive(vfxEntityId));
                Assert.IsTrue(world.Wrap(vfxEntityId).TryGetRef(out BattleVfxLifetimeComponent initialLifetime));
                Assert.AreEqual(1f, initialLifetime.ExpireAtTime - Time.time, 0.05f);

                var refresh = CreatePresentationCue(
                    PresentationCueStage.Refreshed,
                    requestKey: "cue-lifecycle",
                    vfxId: vfxId,
                    templateId: 0,
                    durationMsOverride: 2500);
                handler.HandleSnapshot(new[] { refresh });

                var refreshedEntityId = GetActiveCueEntityId(handler, requestKey);
                Assert.AreEqual(vfxEntityId, refreshedEntityId);
                Assert.IsTrue(world.Wrap(refreshedEntityId).TryGetRef(out BattleVfxLifetimeComponent refreshedLifetime));
                Assert.AreEqual(2.5f, refreshedLifetime.ExpireAtTime - Time.time, 0.05f);

                var stop = CreatePresentationCue(
                    PresentationCueStage.Removed,
                    requestKey: "cue-lifecycle",
                    vfxId: vfxId,
                    templateId: 0);
                handler.HandleSnapshot(new[] { stop });

                Assert.IsFalse(world.IsAlive(vfxEntityId));
                Assert.Throws<System.InvalidOperationException>(() => GetActiveCueEntityId(handler, requestKey));
            }
            finally
            {
                world.DestroyRecursive(vfxRoot.Id);
                BattleContext.Return(context);
            }
        }

        [Test]
        public void BattleVfxManager_DestroysVfxByFollowTargetActorIdOnly()
        {
            var db = new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [90004002] = new VfxDTO { Id = 90004002, Resource = "missing/mozi_projectile", DurationMs = 30000 },
                [90004004] = new VfxDTO { Id = 90004004, Resource = "missing/mozi_crater", DurationMs = 30000 }
            });
            var manager = new BattleVfxManager(db);
            var world = new EntityWorld();
            var root = world.Create("vfxRoot");

            try
            {
                Assert.IsTrue(manager.TryCreateVfxEntity(
                    world,
                    root,
                    90004002,
                    default,
                    10042,
                    Vector3.zero,
                    Quaternion.identity,
                    out var projectileVfx));
                Assert.IsTrue(manager.TryCreateVfxEntity(
                    world,
                    root,
                    90004004,
                    default,
                    10043,
                    Vector3.forward,
                    Quaternion.identity,
                    out var otherVfx));

                var destroyed = manager.DestroyVfxByFollowTargetActorId(root, 10042);

                Assert.AreEqual(1, destroyed);
                Assert.IsFalse(world.IsAlive(projectileVfx.Id));
                Assert.IsTrue(world.IsAlive(otherVfx.Id));
            }
            finally
            {
                world.DestroyRecursive(root.Id);
            }
        }

        [Test]
        public void BattleProjectileViewEventHandler_StopsFollowingVfxOnExitSnapshot()
        {
            var db = new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [90004002] = new VfxDTO { Id = 90004002, Resource = "missing/mozi_projectile", DurationMs = 30000 }
            });
            var manager = new BattleVfxManager(db);
            var world = new EntityWorld();
            var root = world.Create("vfxRoot");
            var lookup = new BattleEntityLookup();
            var query = new BattleEntityQuery(world, lookup);
            var ctx = BattleContext.Rent();
            ctx.EntityWorld = world;

            try
            {
                Assert.IsTrue(manager.TryCreateVfxEntity(
                    world,
                    root,
                    90004002,
                    default,
                    10042,
                    Vector3.zero,
                    Quaternion.identity,
                    out var projectileVfx));
                var handler = new BattleProjectileViewEventHandler(
                    ctx,
                    query,
                    manager,
                    in root);
                var exit = new MobaProjectileEventSnapshotEntry
                {
                    Kind = (int)ProtocolProjectileEventKind.Exit,
                    ProjectileId = 42,
                    ProjectileActorId = 10042,
                    TemplateId = 30040201,
                };

                handler.HandleSnapshot(new[] { exit });

                Assert.IsFalse(
                    world.IsAlive(projectileVfx.Id),
                    "Projectile exit should stop its following VFX without waiting for an actor-despawn snapshot.");
            }
            finally
            {
                if (root.IsValid) world.DestroyRecursive(root.Id);
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void BattlePresentationCueResolver_StopsByStableRequestKeyAndIgnoresMissingVfx()
        {
            var resolver = new BattlePresentationCueResolver();
            var startWithoutVfx = CreatePresentationCue(PresentationCueStage.Started, requestKey: "buff-loop", vfxId: 0, templateId: 0);
            var stop = CreatePresentationCue(PresentationCueStage.Removed, requestKey: "buff-loop", vfxId: 0, templateId: 0);

            var ignoredStart = resolver.Resolve(in startWithoutVfx);
            var stopDecision = resolver.Resolve(in stop);

            Assert.IsTrue(ignoredStart.IsNone);
            Assert.AreEqual(BattlePresentationCueDecisionKind.Stop, stopDecision.Kind);
            Assert.IsFalse(stopDecision.IsNone);
            Assert.IsTrue(stopDecision.SpawnRequest.IsEmpty);
            Assert.AreEqual(BattlePresentationCueRequestKey.From(in startWithoutVfx), stopDecision.RequestKey);
        }

        [Test]
        public void BattleProjectileSnapshotDeduplicator_DropsReplayedProjectileEvent()
        {
            var deduplicator = new BattleProjectileSnapshotDeduplicator();
            var entry = new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProtocolProjectileEventKind.Spawn,
                ProjectileId = 42,
                ProjectileActorId = 10042,
                TemplateId = 30020101,
                LauncherActorId = 2,
                X = 1f,
                Y = 0f,
                Z = 3f,
            };

            Assert.IsTrue(deduplicator.ShouldHandle(in entry));
            Assert.IsFalse(deduplicator.ShouldHandle(in entry));
        }

        [Test]
        public void BattleProjectileSnapshotDeduplicator_AllowsDifferentProjectileIdsInSameFrame()
        {
            var deduplicator = new BattleProjectileSnapshotDeduplicator();
            var first = new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProtocolProjectileEventKind.Spawn,
                ProjectileId = 42,
                TemplateId = 30020101,
                LauncherActorId = 2,
            };
            var second = first;
            second.ProjectileId = 43;

            Assert.IsTrue(deduplicator.ShouldHandle(in first));
            Assert.IsTrue(deduplicator.ShouldHandle(in second));
        }

        [Test]
        public void BattleProjectileSnapshotDeduplicator_ReclaimsFiftyFiveCompletedProjectileLifecycles()
        {
            var deduplicator = new BattleProjectileSnapshotDeduplicator();

            for (var projectileId = 1; projectileId <= 55; projectileId++)
            {
                var spawn = new MobaProjectileEventSnapshotEntry
                {
                    Kind = (int)ProtocolProjectileEventKind.Spawn,
                    ProjectileId = projectileId,
                    ProjectileActorId = 100000 + projectileId,
                    TemplateId = 30060301,
                    LauncherActorId = 31060301,
                };
                var hit = spawn;
                hit.Kind = (int)ProtocolProjectileEventKind.Hit;
                var exit = spawn;
                exit.Kind = (int)ProtocolProjectileEventKind.Exit;

                Assert.IsTrue(deduplicator.ShouldHandle(in spawn));
                Assert.IsTrue(deduplicator.ShouldHandle(in hit));
                Assert.AreEqual(2, deduplicator.Count);

                deduplicator.ForgetLifecycle(in exit);
                Assert.AreEqual(0, deduplicator.Count);

                // Exit cleanup is intentionally idempotent because replay/reconnect can resend it.
                deduplicator.ForgetLifecycle(in exit);
                Assert.AreEqual(0, deduplicator.Count);
            }
        }

        [Test]
        public void BattleProjectileVfxResolver_DoesNotCreateUnconfiguredHitOrExitPlaceholder()
        {
            var resolver = new BattleProjectileVfxResolver();

            Assert.AreEqual(0, resolver.ResolveSnapshotVfxId(30020101, (int)ProtocolProjectileEventKind.Hit));
            Assert.AreEqual(0, resolver.ResolveSnapshotVfxId(30020101, (int)ProtocolProjectileEventKind.Exit));
        }

        [Test]
        public void BattleProjectileSnapshotVfxResolver_UsesSnapshotForwardForSpawnRotation()
        {
            var resolver = new BattleProjectileSnapshotVfxResolver(followTargets: null);
            var entry = new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProtocolProjectileEventKind.Spawn,
                TemplateId = 30020101,
                ForwardX = 1f,
                ForwardY = 0f,
                ForwardZ = 0f,
            };

            Assert.IsTrue(resolver.TryResolve(in entry, out var spec));
            var rotatedForward = spec.Rotation * Vector3.forward;

            Assert.AreEqual(1f, rotatedForward.x, 0.0001f);
            Assert.AreEqual(0f, rotatedForward.y, 0.0001f);
            Assert.AreEqual(0f, rotatedForward.z, 0.0001f);
        }

        [Test]
        public void BattleProjectileSnapshotVfxResolver_PreservesProjectileActorIdForDelayedFollowBinding()
        {
            var resolver = new BattleProjectileSnapshotVfxResolver(followTargets: null);
            var entry = new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProtocolProjectileEventKind.Spawn,
                TemplateId = 30020101,
                ProjectileActorId = 10042,
            };

            Assert.IsTrue(resolver.TryResolve(in entry, out var spec));

            Assert.AreEqual(10042, spec.FollowTargetActorId);
        }

        [Test]
        public void BattleVfxFollowTargetPositionResolver_RebindsDelayedProjectileActorTarget()
        {
            var world = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var query = new BattleEntityQuery(world, lookup);
            var projectile = world.Create("projectile");
            projectile.WithRef(new BattleTransformComponent
            {
                Position = new Vector3(3f, 0f, 4f),
                Forward = Vector3.right,
            });
            lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(10042), projectile);

            var vfx = world.Create("vfx");
            vfx.WithRef(new BattleViewFollowComponent
            {
                Target = default,
                TargetActorId = 10042,
                Offset = Vector3.zero,
            });

            var resolver = new BattleVfxFollowTargetPositionResolver();
            var resolved = resolver.TryResolve(world, vfx, null, query, out var position, out var forward);

            Assert.IsTrue(resolved);
            Assert.AreEqual(projectile.Id, vfx.GetRef<BattleViewFollowComponent>().Target);
            Assert.AreEqual(3f, position.x, 0.0001f);
            Assert.AreEqual(0f, position.y, 0.0001f);
            Assert.AreEqual(4f, position.z, 0.0001f);
            Assert.AreEqual(1f, forward.x, 0.0001f);
            Assert.AreEqual(0f, forward.y, 0.0001f);
            Assert.AreEqual(0f, forward.z, 0.0001f);
        }

        [Test]
        public void GameFlowDomain_UsesInjectedRuntimeServicesForSettingsPersistence()
        {
            var runtime = new TestGameFlowRuntimeServices();
            var flow = new GameFlowDomain(runtime, new TestMobaFeatureFactoryProvider(), new TestLogSink());

            flow.StartWithPersistentSettingsSync();

            Assert.AreEqual(1, runtime.LoadPersistentSettingsSyncCount);
            Assert.AreEqual(0, runtime.LoadPersistentSettingsCount);
            Assert.AreSame(flow.Settings, runtime.LastSyncSettings);
        }

        [Test]
        public void GameFlowDomain_InstallsFeaturesFromInjectedFactoryProvider()
        {
            var runtime = new TestGameFlowRuntimeServices();
            var provider = new TestMobaFeatureFactoryProvider();
            var flow = new GameFlowDomain(runtime, provider, new TestLogSink());

            var installed = flow.AttachBattleFeatures(new[] { "context", "entity" });

            Assert.AreEqual(2, installed);
            Assert.AreEqual(2, runtime.FeatureBinder.AttachCount);
            CollectionAssert.AreEqual(new[] { "context", "entity" }, provider.CreatedFeatureIds);
        }

        [Test]
        public void GameFlowDomain_CreatesSessionFeatureThroughInjectedFactory()
        {
            var runtime = new TestGameFlowRuntimeServices();
            var provider = new TestMobaFeatureFactoryProvider();
            var sessionFactory = new TestBattleSessionFeatureFactory();
            var flow = new GameFlowDomain(runtime, provider, sessionFactory, new TestLogSink());

            var installed = flow.AttachBattleFeatures(new[] { "session" });

            Assert.AreEqual(1, installed);
            Assert.AreEqual(1, sessionFactory.CreateCount);
            Assert.AreSame(sessionFactory.LastCreatedFeature, runtime.FeatureBinder.LastAttachedFeature);
            CollectionAssert.AreEqual(new[] { "session" }, provider.CreatedFeatureIds);
        }

        [TestCase(BattleStartConfig.BattleHostMode.Local)]
        [TestCase(BattleStartConfig.BattleHostMode.GatewayRemote)]
        public void BattleSessionFeature_DoesNotTreatFirstFrameAsAssetBarrier(
            BattleStartConfig.BattleHostMode hostMode)
        {
            Assert.IsFalse(BattleSessionFeature.CompletesAssetBarrierOnFirstFrame(hostMode));
        }

        [Test]
        public void BattleSessionFeature_StartsWorldsThroughInjectedInstaller()
        {
            var installer = new TestBattleSessionWorldInstaller();
            var feature = new BattleSessionFeature(null, null, null, installer);

            InvokePrivate(feature, "StartRemoteDrivenLocalWorld");
            InvokePrivate(feature, "StartConfirmedAuthorityWorld");

            Assert.AreEqual(1, installer.RemoteDrivenStartCount);
            Assert.AreEqual(1, installer.ConfirmedAuthorityStartCount);
        }

        [Test]
        public void BattleSessionFeature_UsesInjectedTransportFactoryForGatewayRemoteSession()
        {
            var installer = new TestBattleSessionWorldInstaller();
            var transportFactory = new TestBattleSessionTransportFactory();
            var feature = new BattleSessionFeature(null, null, null, installer, transportFactory);
            var plan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();
            var opts = new BattleLogicSessionOptions
            {
                Mode = BattleLogicMode.Remote,
                WorldId = new WorldId("1001"),
                WorldType = "battle",
                ClientId = "client_1",
                PlayerId = "7",
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };

            SetPrivatePlan(feature, plan);
            InitializeDispatchers(feature);
            BattleLogicSession session = null;

            try
            {
                session = (BattleLogicSession)InvokePrivate(feature, "StartBattleLogicSession", opts);
                Assert.IsNotNull(session);
                Assert.AreEqual(1, transportFactory.CreateCount);
                Assert.AreEqual(7u, transportFactory.LastLocalPlayerId);
                Assert.AreEqual(1001ul, transportFactory.LastRoomId);
                Assert.AreEqual(plan, transportFactory.LastPlan);
                Assert.IsNotNull(transportFactory.LastCallbackDispatcher);
                Assert.IsNotNull(transportFactory.LastIoDispatcher);
            }
            finally
            {
                session?.Dispose();
                InvokePrivate(feature, "DisposeNetworkIoDispatcher");
            }
        }

        [Test]
        public void BattleSessionFeature_UsesInjectedGatewayConnectionFactoryForRoomConnection()
        {
            var installer = new TestBattleSessionWorldInstaller();
            var gatewayConnectionFactory = new TestBattleSessionGatewayConnectionFactory();
            var feature = new BattleSessionFeature(null, null, null, installer, null, gatewayConnectionFactory);
            var plan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: true,
                    autoJoinRoom: false,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            SetPrivatePlan(feature, plan);
            InitializeDispatchers(feature);

            try
            {
                var connection = (IConnection)InvokePrivate(feature, "CreateGatewayRoomConnection", plan);
                Assert.AreSame(gatewayConnectionFactory.Connection, connection);
                Assert.AreEqual(1, gatewayConnectionFactory.CreateCount);
                Assert.AreEqual(plan, gatewayConnectionFactory.LastPlan);
                Assert.IsNotNull(gatewayConnectionFactory.LastCallbackDispatcher);
                Assert.IsNotNull(gatewayConnectionFactory.LastIoDispatcher);
            }
            finally
            {
                InvokePrivate(feature, "DisposeNetworkIoDispatcher");
            }
        }

        [Test]
        public void BattleSessionFeature_UsesInjectedGatewayRoomClientFactoryForRoomClient()
        {
            var installer = new TestBattleSessionWorldInstaller();
            var gatewayConnectionFactory = new TestBattleSessionGatewayConnectionFactory();
            var gatewayRoomClientFactory = new TestBattleSessionGatewayRoomClientFactory();
            var feature = new BattleSessionFeature(null, null, null, installer, null, gatewayConnectionFactory, gatewayRoomClientFactory);
            var plan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            SetPrivatePlan(feature, plan);
            InvokePrivate(feature, "StartGatewayRoomPreparation");

            try
            {
                Assert.AreSame(gatewayConnectionFactory.Connection, gatewayRoomClientFactory.LastConnection);
                Assert.AreEqual(1, gatewayRoomClientFactory.CreateCount);
                Assert.AreEqual(110u, gatewayRoomClientFactory.LastOpCodes.CreateRoom);
                Assert.AreEqual(111u, gatewayRoomClientFactory.LastOpCodes.JoinRoom);
            }
            finally
            {
                InvokePrivate(feature, "StopGatewayRoomPreparation");
            }
        }
 
        [Test]
        public void GatewayRoomPreparationHelper_DetectsGatewayRoomPreparationRequirement()
        {
            var autoJoinPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: string.Empty);
            var noGatewayPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.Local)
                .Build();
            var noRoomPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            Assert.IsTrue(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(autoJoinPlan));
            Assert.IsFalse(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(noGatewayPlan));
            Assert.IsFalse(GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(noRoomPlan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesJoinRoomIdByPriority()
        {
            var explicitPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: "room_explicit");
            var numericPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 2002, joinRoomId: string.Empty);
            var worldPlan = CreateGatewayPlan(worldId: "1001", numericRoomId: 0, joinRoomId: string.Empty);

            Assert.AreEqual("room_explicit", GatewayRoomPreparationHelper.ResolveJoinRoomId(explicitPlan));
            Assert.AreEqual("2002", GatewayRoomPreparationHelper.ResolveJoinRoomId(numericPlan));
            Assert.AreEqual("1001", GatewayRoomPreparationHelper.ResolveJoinRoomId(worldPlan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenJoinRoomIdCannotBeResolved()
        {
            var plan = CreateGatewayPlan(worldId: "placeholder", numericRoomId: 0, joinRoomId: string.Empty)
                .WithGatewayRoom(string.Empty, 0);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveJoinRoomId(plan));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesCreatedRoomWorldId()
        {
            var result = new GatewayCreateRoomResult("room_1001", 1001);

            var worldId = GatewayRoomPreparationHelper.ResolveCreatedRoomWorldId(in result);

            Assert.AreEqual("1001", worldId);
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenCreatedRoomNumericIdIsInvalid()
        {
            var result = new GatewayCreateRoomResult("room_invalid", 0);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveCreatedRoomWorldId(in result));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesCreatedRoomJoinRoomId()
        {
            var explicitResult = new GatewayCreateRoomResult("room_1001", 1001);
            var numericFallbackResult = new GatewayCreateRoomResult(string.Empty, 1001);

            Assert.AreEqual("room_1001", GatewayRoomPreparationHelper.ResolveCreatedRoomJoinRoomId(in explicitResult, 1001));
            Assert.AreEqual("1001", GatewayRoomPreparationHelper.ResolveCreatedRoomJoinRoomId(in numericFallbackResult, 1001));
        }

        [Test]
        public void GatewayRoomPreparationHelper_ResolvesJoinedRoomWorldId()
        {
            var result = new GatewayJoinRoomResult(
                numericRoomId: 1001,
                snapshotJson: string.Empty,
                worldStartAnchor: default);

            var worldId = GatewayRoomPreparationHelper.ResolveJoinedRoomWorldId(in result, "room_1001");

            Assert.AreEqual("1001", worldId);
        }

        [Test]
        public void GatewayRoomPreparationHelper_ThrowsWhenJoinedRoomNumericIdIsInvalid()
        {
            var result = new GatewayJoinRoomResult(
                numericRoomId: 0,
                snapshotJson: string.Empty,
                worldStartAnchor: default);

            Assert.Throws<InvalidOperationException>(() => GatewayRoomPreparationHelper.ResolveJoinedRoomWorldId(in result, "room_invalid"));
        }
 
        [Test]
        public void GatewayRoomPreparationHelper_RecordsValidWorldStartAnchor()
        {
            var anchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();
            var worldId = new WorldId("1001");
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 100,
                serverTickFrequency: 1000,
                startFrame: 12,
                fixedDeltaSeconds: 0.033d);

            var recorded = GatewayRoomPreparationHelper.TryRecordWorldStartAnchor(anchors, worldId, in anchor);

            Assert.IsTrue(recorded);
            Assert.IsTrue(anchors.TryGetValue(worldId, out var stored));
            Assert.AreEqual(1000, stored.ServerTickFrequency);
            Assert.AreEqual(12, stored.StartFrame);
        }

        [Test]
        public void GatewayRoomPreparationHelper_IgnoresInvalidWorldStartAnchor()
        {
            var anchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();
            var worldId = new WorldId("1001");
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 100,
                serverTickFrequency: 0,
                startFrame: 12,
                fixedDeltaSeconds: 0.033d);

            var recorded = GatewayRoomPreparationHelper.TryRecordWorldStartAnchor(anchors, worldId, in anchor);

            Assert.IsFalse(recorded);
            Assert.IsFalse(anchors.ContainsKey(worldId));
        }

        [Test]
        public void GatewayTimeSyncHelper_NormalizesRuntimeOptions()
        {
            var raw = new BattleStartPlanTimeSyncOptions(
                opCode: 120,
                intervalMs: 0,
                alpha: 2d,
                timeoutMs: -1,
                idealFrameSafetyConstMarginFrames: 0,
                idealFrameSafetyRttFactor: 0d,
                idealFrameSafetyMinMarginFrames: 0,
                idealFrameSafetyMaxMarginFrames: 0);

            var options = GatewayTimeSyncHelper.ResolveRuntimeOptions(in raw);

            Assert.AreEqual(120u, options.OpCode);
            Assert.AreEqual(1000, options.IntervalMs);
            Assert.AreEqual(1d, options.Alpha);
            Assert.AreEqual(2000, options.TimeoutMs);
        }

        [Test]
        public void GatewayTimeSyncHelper_CalculatesRttAndClockOffset()
        {
            var sample = GatewayTimeSyncHelper.CalculateSample(
                clientSendTicks: 1000,
                clientReceiveTicks: 1300,
                serverNowTicks: 2000,
                serverTickFrequency: 1000,
                localTickFrequency: 1000d);

            Assert.AreEqual(0.3d, sample.RttSeconds, 0.000001d);
            Assert.AreEqual(-0.85d, sample.OffsetSeconds, 0.000001d);
        }

        [Test]
        public void GatewayTimeSyncHelper_ClampsNegativeRtt()
        {
            var sample = GatewayTimeSyncHelper.CalculateSample(
                clientSendTicks: 1300,
                clientReceiveTicks: 1000,
                serverNowTicks: 2000,
                serverTickFrequency: 1000,
                localTickFrequency: 1000d);

            Assert.AreEqual(0d, sample.RttSeconds, 0.000001d);
        }

        [Test]
        public void GatewayTimeSyncHelper_AppliesFirstAndEwmaSamples()
        {
            var firstSample = new GatewayTimeSyncSample(rttSeconds: 0.3d, offsetSeconds: -0.8d);
            var first = GatewayTimeSyncHelper.ApplySample(
                hasClockSync: false,
                currentClockOffsetSecondsEwma: 0d,
                currentRttSecondsEwma: 0d,
                currentSamples: 0,
                sample: in firstSample,
                alpha: 0.5d);
            var secondSample = new GatewayTimeSyncSample(rttSeconds: 0.5d, offsetSeconds: -0.4d);
            var second = GatewayTimeSyncHelper.ApplySample(
                hasClockSync: first.HasClockSync,
                currentClockOffsetSecondsEwma: first.ClockOffsetSecondsEwma,
                currentRttSecondsEwma: first.RttSecondsEwma,
                currentSamples: first.Samples,
                sample: in secondSample,
                alpha: 0.5d);

            Assert.IsTrue(first.HasClockSync);
            Assert.AreEqual(-0.8d, first.ClockOffsetSecondsEwma, 0.000001d);
            Assert.AreEqual(0.3d, first.RttSecondsEwma, 0.000001d);
            Assert.AreEqual(1, first.Samples);
            Assert.AreEqual(-0.6d, second.ClockOffsetSecondsEwma, 0.000001d);
            Assert.AreEqual(0.4d, second.RttSecondsEwma, 0.000001d);
            Assert.AreEqual(2, second.Samples);
        }

        [Test]
        public void GatewayFrameTimingHelper_ResolvesRawMarginAndLimitedFrame()
        {
            var anchor = new GatewayWorldStartAnchor(
                startServerTicks: 1000,
                serverTickFrequency: 1000,
                startFrame: 10,
                fixedDeltaSeconds: 0.1d);
            var timeSync = new BattleStartPlanTimeSyncOptions(
                opCode: 120,
                intervalMs: 1000,
                alpha: 0.5d,
                timeoutMs: 2000,
                idealFrameSafetyConstMarginFrames: 2,
                idealFrameSafetyRttFactor: 1.5d,
                idealFrameSafetyMinMarginFrames: 1,
                idealFrameSafetyMaxMarginFrames: 4);
            var input = new GatewayFrameTimingInput(
                in anchor,
                hasClockSync: true,
                clockOffsetSecondsEwma: 0.5d,
                rttSecondsEwma: 0.2d,
                timeSync: in timeSync);

            var raw = GatewayFrameTimingHelper.ResolveIdealFrameRaw(in input, localNowSeconds: 2.15d);
            var margin = GatewayFrameTimingHelper.ResolveIdealFrameSafetyMarginFrames(in input);
            var limit = GatewayFrameTimingHelper.ResolveIdealFrameLimit(in input, localNowSeconds: 2.15d);

            Assert.AreEqual(16, raw);
            Assert.AreEqual(3, margin);
            Assert.AreEqual(13, limit);
        }

        [Test]
        public void SessionSimRuntimeTuning_ProtectsUnconsumedInputsFromTickBasedTrimming()
        {
            Assert.AreEqual(37, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                lastTickedFrame: 281,
                lastConsumedFrame: 36));
            Assert.AreEqual(161, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                lastTickedFrame: 281,
                lastConsumedFrame: 200));
            Assert.AreEqual(0, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(
                lastTickedFrame: 281,
                lastConsumedFrame: -1));
        }

        [Test]
        public void FrameTimeRollbackStateProvider_RestoresCapturedClockWhenSnapshotLabelIsAhead()
        {
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(178), time: 5.933333f, fixedDelta: 1f / 30f);
            var provider = new FrameTimeRollbackStateProvider(frameTime);
            var snapshot = provider.Export(new FrameIndex(179));

            frameTime.StepTo(new FrameIndex(209), 1f / 30f);
            provider.Import(new FrameIndex(179), snapshot);

            Assert.AreEqual(178, frameTime.Frame.Value);
            Assert.AreEqual(5.933333f, frameTime.Time, 0.00001f);
            Assert.AreEqual(0f, frameTime.DeltaTime, 0.000001f);
            Assert.AreEqual(1f / 30f, frameTime.FrameToTime(new FrameIndex(1)), 0.000001f);
        }

        [Test]
        public void ServerFrameTimeModule_FallbackTickContinuesFromRestoredWorldFrame()
        {
            const float fixedDelta = 1f / 30f;
            var worldId = new WorldId("rollback-clock");
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(178), time: 5.933333f, fixedDelta);
            var provider = new FrameTimeRollbackStateProvider(frameTime);
            var snapshot = provider.Export(new FrameIndex(179));
            var module = new ServerFrameTimeModule(fixedDelta);
            var timesField = typeof(ServerFrameTimeModule).GetField(
                "_times",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(timesField);
            var times = (Dictionary<WorldId, FrameTime>)timesField.GetValue(module);
            times.Add(worldId, frameTime);

            InvokePrivate(module, "OnPostTick", fixedDelta);
            frameTime.StepTo(new FrameIndex(209), fixedDelta);
            provider.Import(new FrameIndex(179), snapshot);
            InvokePrivate(module, "OnPostTick", fixedDelta);

            Assert.AreEqual(179, frameTime.Frame.Value);
            Assert.AreEqual(5.966666f, frameTime.Time, 0.00001f);
        }

        [Test]
        public void SnapshotBufferEmitter_AllowsSameFrameEmissionAfterEmptyProbe()
        {
            var emitter = new TestSnapshotBufferEmitter();
            var frame = new FrameIndex(248);

            Assert.IsFalse(emitter.TryGetSnapshot(frame, out _));

            emitter.Enqueue(4012);

            Assert.IsTrue(emitter.TryGetSnapshot(frame, out var snapshot));
            Assert.AreEqual(4012, snapshot.OpCode);
            Assert.IsFalse(emitter.TryGetSnapshot(frame, out _));
        }

        [Test]
        public void SnapshotBufferEmitter_AllowsNewEventAfterSameFrameWasAlreadyDrained()
        {
            var emitter = new TestSnapshotBufferEmitter();
            var frame = new FrameIndex(198);

            emitter.Enqueue(4005);
            Assert.IsTrue(emitter.TryGetSnapshot(frame, out var predictedSnapshot));
            Assert.AreEqual(4005, predictedSnapshot.OpCode);
            Assert.IsFalse(emitter.TryGetSnapshot(frame, out _));

            emitter.Enqueue(4012);
            Assert.IsTrue(emitter.TryGetSnapshot(frame, out var authoritativeSnapshot));
            Assert.AreEqual(4012, authoritativeSnapshot.OpCode);
            Assert.IsFalse(emitter.TryGetSnapshot(frame, out _));
        }

        [Test]
        public void ClientPredictionRollbackCapture_ProcessesOnlyPredictionFrameChanges()
        {
            var method = typeof(AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule)
                .GetMethod(
                    "ShouldProcessRollbackFrame",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            Assert.IsFalse((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(182),
                new FrameIndex(182),
            }));
            Assert.IsTrue((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(183),
                new FrameIndex(182),
            }));
            Assert.IsTrue((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(182),
                new FrameIndex(209),
            }));
        }

        [Test]
        public void ClientPredictionReconcileRollback_RejectsPreviouslyRestoredFrame()
        {
            var method = typeof(AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule)
                .GetMethod(
                    "ShouldRequestReconcileRollback",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            Assert.IsFalse((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(179),
                new FrameIndex(180),
            }));
            Assert.IsTrue((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(178),
                new FrameIndex(180),
            }));
        }

        [Test]
        public void ClientPredictionReconcileRollback_AllowsInitialRestoreToFrameZero()
        {
            var method = typeof(AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule)
                .GetMethod(
                    "ShouldRequestReconcileRollback",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            Assert.IsTrue((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(-1),
                new FrameIndex(1),
            }));
            Assert.IsFalse((bool)method.Invoke(null, new object[]
            {
                new FrameIndex(0),
                new FrameIndex(1),
            }));
        }

        [Test]
        public void ClientPredictionFrameTime_AlignsDriftedHostClockToSimulationFrame()
        {
            const float fixedDelta = 1f / 30f;
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(219), 219 * fixedDelta, fixedDelta);
            var method = typeof(AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule)
                .GetMethod(
                    "AlignFrameTime",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            method.Invoke(null, new object[]
            {
                frameTime,
                new FrameIndex(185),
                fixedDelta,
            });

            Assert.AreEqual(185, frameTime.Frame.Value);
            Assert.AreEqual(185 * fixedDelta, frameTime.Time, 0.00001f);
            Assert.AreEqual(fixedDelta, frameTime.DeltaTime, 0.000001f);
        }

        [Test]
        public void ClientPredictionBaseline_AlignsClockToImportedSnapshotFrame()
        {
            const float fixedDelta = 1f / 30f;
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(12), 12 * fixedDelta, fixedDelta);
            var method = typeof(ClientPredictionDriverModule).GetMethod(
                "AlignFrameTimeToBaseline",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            method.Invoke(null, new object[] { frameTime, new FrameIndex(152) });

            Assert.AreEqual(152, frameTime.Frame.Value);
            Assert.AreEqual(152 * fixedDelta, frameTime.Time, 0.00001f);
            Assert.AreEqual(0f, frameTime.DeltaTime, 0.000001f);
        }

        [Test]
        public void ServerFrameTimeModule_CanDisableHostTickFallback()
        {
            var module = new ServerFrameTimeModule(
                1f / 30f,
                advanceOnHostTickFallback: false);
            var field = typeof(ServerFrameTimeModule).GetField(
                "_advanceOnHostTickFallback",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.IsFalse((bool)field.GetValue(module));
        }

        [Test]
        public void InputHistoryRingBuffer_EvictsFrameAtCapacityBoundary()
        {
            var history = new InputHistoryRingBuffer(240);
            var oldFrame = new FrameIndex(139);
            var replacementFrame = new FrameIndex(379);
            var oldInputs = new[]
            {
                new PlayerInputCommand(oldFrame, new PlayerId("1"), 3031, new byte[] { 1 })
            };

            history.Store(oldFrame, oldInputs);
            Assert.IsTrue(history.TryGet(oldFrame, out var stored));
            Assert.AreSame(oldInputs, stored);

            history.Store(replacementFrame, Array.Empty<PlayerInputCommand>());

            Assert.IsFalse(history.TryGet(oldFrame, out _));
            Assert.IsTrue(history.TryGet(replacementFrame, out var replacement));
            Assert.IsEmpty(replacement);
        }

        [Test]
        public void RemoteDrivenRuntimeModuleFactory_RetainsSixHundredPredictionFrames()
        {
            Assert.AreEqual(600, RemoteDrivenRuntimeModuleFactory.PredictionRollbackHistoryFrames);
        }

        [Test]
        public void RemoteDrivenWorldTickDriver_UsesActualPredictionFrameWhenLoopCounterIsAhead()
        {
            Assert.AreEqual(
                171,
                RemoteDrivenWorldTickDriver.ResolveCatchUpFrame(
                    fallbackFrame: 385,
                    hasPredictionFrame: true,
                    predictionFrame: 171));
            Assert.AreEqual(
                385,
                RemoteDrivenWorldTickDriver.ResolveCatchUpFrame(
                    fallbackFrame: 385,
                    hasPredictionFrame: false,
                    predictionFrame: 0));
        }

        [Test]
        public void RemoteDrivenWorldTickDriver_WhenDynamicPredictionTargetIsReached_DoesNotTickRuntime()
        {
            var driveTargetFrame = RemoteDrivenWorldTickDriver.ResolveDriveTargetFrame(
                inputTargetFrame: 325,
                predictionEnabled: true,
                hasPredictionWindow: true,
                predictionWindow: 8);

            Assert.AreEqual(333, driveTargetFrame);
            Assert.IsFalse(RemoteDrivenWorldTickDriver.ShouldDriveRuntime(
                currentFrame: 333,
                driveTargetFrame: driveTargetFrame,
                remainingSteps: 5));
            Assert.IsTrue(RemoteDrivenWorldTickDriver.ShouldDriveRuntime(
                currentFrame: 332,
                driveTargetFrame: driveTargetFrame,
                remainingSteps: 5));
        }

        [Test]
        public void RemoteDrivenWorldTickDriver_WhenPredictionDoesNotAdvance_StopsCatchUpLoop()
        {
            Assert.IsFalse(RemoteDrivenWorldTickDriver.DidAdvancePredictionFrame(333, 333));
            Assert.IsFalse(RemoteDrivenWorldTickDriver.DidAdvancePredictionFrame(333, 329));
            Assert.IsTrue(RemoteDrivenWorldTickDriver.DidAdvancePredictionFrame(333, 334));
        }

        [Test]
        public void SessionSimRuntimeTuning_ResolvesModeAwareInputSubmitFrame()
        {
            var localPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 9)
                .WithHostMode(BattleStartConfig.BattleHostMode.Local)
                .Build();
            var minimumLeadGatewayPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 41101,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: "room",
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();
            var configuredLeadGatewayPlan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 5)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 41101,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: false,
                    autoJoinRoom: false,
                    joinRoomId: "room",
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            Assert.AreEqual(121, SessionSimRuntimeTuning.ResolveInputSubmitFrame(120, in localPlan));
            Assert.AreEqual(123, SessionSimRuntimeTuning.ResolveInputSubmitFrame(120, in minimumLeadGatewayPlan));
            Assert.AreEqual(126, SessionSimRuntimeTuning.ResolveInputSubmitFrame(120, in configuredLeadGatewayPlan));
            Assert.IsTrue(SessionSimRuntimeTuning.ShouldUseFrameSyncInput(BattleSyncMode.Lockstep));
            Assert.IsTrue(SessionSimRuntimeTuning.ShouldUseFrameSyncInput(BattleSyncMode.HybridPredictReconcile));
            Assert.IsFalse(SessionSimRuntimeTuning.ShouldUseFrameSyncInput(BattleSyncMode.SnapshotAuthority));
        }

        [Test]
        public void BattleMoveInputState_SubmitsContinuousMoveAtMostOncePerTargetFrame()
        {
            var state = new BattleMoveInputState();

            Assert.IsTrue(state.TryGetMoveToSubmit(10, 1f, 0.25f, out var dx, out var dz));
            Assert.AreEqual(1f, dx);
            Assert.AreEqual(0.25f, dz);
            Assert.IsFalse(state.TryGetMoveToSubmit(10, 1f, 0.25f, out _, out _));
            Assert.IsTrue(state.TryGetMoveToSubmit(11, 1f, 0.25f, out dx, out dz));
            Assert.AreEqual(1f, dx);
            Assert.AreEqual(0.25f, dz);
        }

        [Test]
        public void BattleMoveInputState_SubmitsInitialNeutralInputOnce()
        {
            var state = new BattleMoveInputState();

            Assert.IsTrue(state.TryGetMoveToSubmit(10, 0f, 0f, out var dx, out var dz));
            Assert.AreEqual(0f, dx);
            Assert.AreEqual(0f, dz);
            Assert.IsFalse(state.TryGetMoveToSubmit(10, 0f, 0f, out _, out _));
            Assert.IsFalse(state.TryGetMoveToSubmit(11, 0f, 0f, out _, out _));
        }

        [Test]
        public void BattleMoveInputState_DefersSameFrameStopAndRepeatsItAcrossFrames()
        {
            var state = new BattleMoveInputState();

            Assert.IsTrue(state.TryGetMoveToSubmit(20, 1f, 0f, out _, out _));
            Assert.IsFalse(state.TryGetMoveToSubmit(20, 0f, 0f, out _, out _));
            Assert.IsTrue(state.TryGetMoveToSubmit(21, 0f, 0f, out var dx, out var dz));
            Assert.AreEqual(0f, dx);
            Assert.AreEqual(0f, dz);
            Assert.IsTrue(state.TryGetMoveToSubmit(22, 0f, 0f, out _, out _));
            Assert.IsTrue(state.TryGetMoveToSubmit(23, 0f, 0f, out _, out _));
            Assert.IsFalse(state.TryGetMoveToSubmit(24, 0f, 0f, out _, out _));
        }

        [Test]
        public void BattleLocalInputQueue_PreservesAuthoritativeTargetFrame()
        {
            var queue = new BattleLocalInputQueue();
            var expectedFrame = new FrameIndex(37);
            queue.Enqueue(new LocalPlayerInputEvent(
                expectedFrame,
                new PlayerId("player-1"),
                MobaOpCodes.Input.Move,
                new byte[] { 1, 2, 3 }));

            queue.Flush();

            Assert.IsTrue(queue.TryDequeue(out var batch));
            Assert.AreEqual(1, batch.Length);
            Assert.AreEqual(expectedFrame.Value, batch[0].Frame.Value);
            Assert.AreEqual("player-1", batch[0].PlayerId.Value);
        }

        [Test]
        public void BattleLocalInputQueue_EmptyRenderFrames_DoNotDelayGameplayInput()
        {
            var queue = new BattleLocalInputQueue();
            for (var i = 0; i < 500; i++)
            {
                queue.Flush();
            }

            var expectedFrame = new FrameIndex(73);
            queue.Enqueue(new LocalPlayerInputEvent(
                expectedFrame,
                new PlayerId("player-1"),
                MobaOpCodes.Input.Move,
                new byte[] { 4, 5, 6 }));
            queue.Flush();

            Assert.AreEqual(501, queue.LocalFrame);
            Assert.IsTrue(queue.TryDequeue(out var batch));
            Assert.AreEqual(1, batch.Length);
            Assert.AreEqual(expectedFrame.Value, batch[0].Frame.Value);
            Assert.IsFalse(queue.TryDequeue(out _));
        }

        [Test]
        public void GatewaySessionFailurePolicy_ControlsPreparationAndTimeSyncFailures()
        {
            var source = new InvalidOperationException("boom");
            var task = Task.FromException(source);

            var wrapped = GatewaySessionFailurePolicy.WrapPreparationFailure(task);

            Assert.AreEqual("Gateway room preparation failed.", wrapped.Message);
            Assert.AreSame(source, wrapped.InnerException);
            Assert.IsFalse(GatewaySessionFailurePolicy.ShouldNotifyTimeSyncFailure(new OperationCanceledException(), 99, 3));
            Assert.IsFalse(GatewaySessionFailurePolicy.ShouldNotifyTimeSyncFailure(source, 2, 3));
            Assert.IsTrue(GatewaySessionFailurePolicy.ShouldNotifyTimeSyncFailure(source, 3, 3));
            Assert.IsTrue(GatewaySessionFailurePolicy.ShouldNotifyTimeSyncFailure(source, 1, 0));
        }

        [Test]
        public void MobaFlowConfiguration_AllowsFailureEndTransitionFromEveryBattleRuntimeState()
        {
            var config = MobaFlowConfiguration.CreateDefault();
            var failedStates = new[]
            {
                MobaBattleState.Prepare,
                MobaBattleState.Connect,
                MobaBattleState.CreateOrJoinWorld,
                MobaBattleState.LoadAssets,
                MobaBattleState.InMatch
            };

            foreach (var state in failedStates)
            {
                var found = false;
                for (var i = 0; i < config.BattleMachine.Transitions.Count; i++)
                {
                    var transition = config.BattleMachine.Transitions[i];
                    if (Equals(transition.Trigger, MobaBattleEvent.Ended) &&
                        Equals(transition.From, state) &&
                        Equals(transition.To, MobaBattleState.End))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, state.ToString());
            }
        }

        [Test]
        public void MobaFlowConfiguration_ProvidesStableDescriptionsForEveryConfiguredState()
        {
            var config = MobaFlowConfiguration.CreateDefault();

            foreach (MobaRootState state in Enum.GetValues(typeof(MobaRootState)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(config.GetRootStateDescription(state)), state.ToString());
            }

            foreach (MobaBattleState state in Enum.GetValues(typeof(MobaBattleState)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(config.GetBattleStateDescription(state)), state.ToString());
            }
        }

        [Test]
        public void MobaFlowConfigurationValidator_AcceptsDefaultConfiguration()
        {
            var result = MobaFlowConfigurationValidator.Validate(MobaFlowConfiguration.CreateDefault());

            Assert.IsTrue(result.IsValid, string.Join(" | ", result.Errors));
        }

        [Test]
        public void MobaFlowConfigurationValidator_ReportsMissingStateDescription()
        {
            var config = MobaFlowConfiguration.CreateDefault();
            var descriptions = (Dictionary<MobaBattleState, string>)config.BattleStateDescriptions;
            descriptions.Remove(MobaBattleState.LoadAssets);

            var result = MobaFlowConfigurationValidator.Validate(config);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(ContainsError(result.Errors, "MOBA battle state has no stable description: LoadAssets"));
        }
 
        [Test]
        public void GatewayRoomCleanupHelper_ClearsAnchorsAndRemovesReliableConnection()
        {
            var anchors = new Dictionary<WorldId, GatewayWorldStartAnchor>
            {
                [new WorldId("1001")] = new GatewayWorldStartAnchor(100, 1000, 12, 0.033d)
            };
            var registry = new TestAbilityKitConnectionRegistry(removeResult: true);

            GatewayRoomCleanupHelper.ClearWorldStartAnchors(anchors);
            var removed = GatewayRoomCleanupHelper.RemoveGatewayReliableConnection(registry);

            Assert.AreEqual(0, anchors.Count);
            Assert.IsTrue(removed);
            Assert.AreEqual(AbilityKitConnectionRole.GatewayReliable, registry.LastRemovedRole);
            Assert.IsTrue(registry.LastRemoveDispose);
        }

        [Test]
        public void BootFeaturePlan_CreatesRegisteredFormalLobbyFeature()
        {
            var registry = new UnityMobaFeatureFactoryProvider()
                .CreateFeatureFactoryRegistry(() => null);
            var plan = new MobaFeaturePlanFactory(registry).CreateBootFeaturePlan();
            var ctx = default(AbilityKit.Game.Flow.GamePhaseContext);

            var created = plan.TryCreate("formal_lobby", in ctx, out var feature);

            Assert.IsTrue(created);
            Assert.IsInstanceOf<FormalLobbyFeature>(feature);
        }

        [Test]
        public void LobbyBattleEntrySelection_RemotePresetActivatesFormalLobbyUntilCleared()
        {
            var config = ScriptableObject.CreateInstance<BattleStartConfig>();
            var preset = ScriptableObject.CreateInstance<BattleStartPresetSO>();
            preset.HostMode = BattleStartConfig.BattleHostMode.GatewayRemote;
            var selection = new LobbyBattleEntrySelection();

            try
            {
                Assert.IsFalse(FormalLobbyFeature.ShouldShowFlowWindow(selection));
                Assert.IsTrue(DemoLobbyOnGUIFeature.IsRemotePreset(preset));

                selection.SelectRemote(config, preset);

                Assert.IsTrue(FormalLobbyFeature.ShouldShowFlowWindow(selection));
                Assert.AreSame(config, selection.Config);
                Assert.AreSame(preset, selection.Preset);

                selection.Clear();
                Assert.IsFalse(FormalLobbyFeature.ShouldShowFlowWindow(selection));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void BattleScopeManager_RejectsQueuedSessionCallbackFromPreviousScope()
        {
            using var host = new BattleWorldScopeHost();
            var triggered = new List<MobaBattleEvent>();
            var sessions = new List<TestBattleSessionFeature>();
            var manager = new BattleScopeManager(
                new BattleScopeManager.Callbacks
                {
                    SetBattleRequested = _ => { },
                    EnqueueRootEvent = _ => { },
                    TriggerBattleFsm = triggered.Add,
                    GetActiveBattle = () => MobaBattleState.Prepare,
                    ClearGatewayConnectionFactory = () => { },
                    GetGatewayConnectionFactory = () => null,
                    CreateBattleSessionFeature = (_, __) =>
                    {
                        var session = new TestBattleSessionFeature();
                        sessions.Add(session);
                        return session;
                    },
                },
                host,
                new MobaBattleAdvanceDecider(),
                new TestLogSink());

            manager.EnterBattle(null);
            manager.CreateBattleSessionFeature();
            var queuedFromFirstBattle = sessions[0].CaptureSessionStarted();

            manager.EnterBattle(null);
            manager.CreateBattleSessionFeature();
            var currentState = host.Resolve<IBattleRuntimeState>();

            queuedFromFirstBattle?.Invoke();

            Assert.IsFalse(currentState.SessionStarted);
            Assert.IsEmpty(triggered);

            sessions[1].RaiseSessionStarted();

            Assert.IsTrue(currentState.SessionStarted);
            CollectionAssert.AreEqual(new[] { MobaBattleEvent.PrepareDone }, triggered);
        }

        [Test]
        public void BattleScopeManager_ClearSessionEvents_UnsubscribesCapturedHandlers()
        {
            using var host = new BattleWorldScopeHost();
            var triggered = new List<MobaBattleEvent>();
            TestBattleSessionFeature session = null;
            var manager = new BattleScopeManager(
                new BattleScopeManager.Callbacks
                {
                    SetBattleRequested = _ => { },
                    EnqueueRootEvent = _ => { },
                    TriggerBattleFsm = triggered.Add,
                    GetActiveBattle = () => MobaBattleState.Prepare,
                    ClearGatewayConnectionFactory = () => { },
                    GetGatewayConnectionFactory = () => null,
                    CreateBattleSessionFeature = (_, __) =>
                        session = new TestBattleSessionFeature(),
                },
                host,
                new MobaBattleAdvanceDecider(),
                new TestLogSink());

            manager.EnterBattle(null);
            manager.CreateBattleSessionFeature();
            Assert.AreEqual(4, session.SubscriberCount);

            manager.ClearBattleSessionEvents();
            session.RaiseAll();

            Assert.AreEqual(0, session.SubscriberCount);
            Assert.IsEmpty(triggered);
            Assert.IsFalse(host.Resolve<IBattleRuntimeState>().SessionStarted);
        }

        [UnityTest]
        public IEnumerator FormalLobbyFeature_DetachReattach_RejectsPreviousOperationCompletion()
        {
            var feature = new FormalLobbyFeature();
            var firstCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ctx = default(AbilityKit.Game.Flow.GamePhaseContext);

            feature.OnAttach(in ctx);
            feature.StartControlledOperationForTesting("first", async () =>
            {
                try
                {
                    await firstCompletion.Task;
                }
                finally
                {
                    firstExited.TrySetResult(true);
                }
            });
            Assert.IsTrue(feature.OperationBusyForTesting);
            Assert.AreEqual("first", feature.OperationLabelForTesting);

            feature.OnDetach(in ctx);
            feature.OnAttach(in ctx);
            feature.StartControlledOperationForTesting("second", () => secondCompletion.Task);

            firstCompletion.SetException(new InvalidOperationException("stale failure"));
            for (var i = 0; i < 20 && !firstExited.Task.IsCompleted; i++)
            {
                yield return null;
            }
            Assert.IsTrue(firstExited.Task.IsCompleted, "The detached operation did not exit in time.");
            yield return null;

            Assert.IsTrue(feature.OperationBusyForTesting);
            Assert.AreEqual("second", feature.OperationLabelForTesting);
            Assert.IsEmpty(feature.OperationErrorForTesting);

            secondCompletion.SetResult(true);
            for (var i = 0; i < 20 && feature.OperationBusyForTesting; i++)
            {
                yield return null;
            }

            Assert.IsFalse(feature.OperationBusyForTesting);
            Assert.IsEmpty(feature.OperationLabelForTesting);
            feature.OnDetach(in ctx);
        }

        [Test]
        public void ExistingGatewayRoomBattleBootstrapper_UsesAuthoritativeRoomIdentifiersWithoutPreparingRoomAgain()
        {
            var sourcePlan = BattleStartPlanBuilder
                .ForWorld("preset-world", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.Local)
                .WithGateway(
                    useGatewayTransport: false,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 11UL,
                    sessionToken: "preset-token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: true,
                    autoJoinRoom: true,
                    joinRoomId: "preset-room",
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();
            var bootstrapper = new ExistingGatewayRoomBattleBootstrapper(
                new FixedBattleBootstrapper(sourcePlan),
                "authenticated-token",
                "room-public-id",
                "battle-authoritative-id",
                9001UL,
                7001UL,
                42u);

            var plan = bootstrapper.Build();

            Assert.IsTrue(bootstrapper.IsAuthenticated);
            Assert.IsTrue(bootstrapper.IsRoomReady);
            Assert.IsTrue(
                bootstrapper.IsConnectivityReady,
                "Connectivity gate must use the configured plan endpoint when no launch request override exists.");
            Assert.IsTrue(bootstrapper.IsAssetsReady);
            Assert.AreEqual(BattleStartConfig.BattleHostMode.GatewayRemote, plan.HostMode);
            Assert.AreEqual("7001", plan.World.WorldId);
            Assert.AreEqual("42", plan.World.PlayerId);
            Assert.AreEqual(9001UL, plan.Gateway.NumericRoomId);
            Assert.AreEqual("room-public-id", plan.Gateway.JoinRoomId);
            Assert.AreEqual("battle-authoritative-id", plan.Gateway.BattleId);
            Assert.AreEqual("authenticated-token", plan.Gateway.SessionToken);
            Assert.IsTrue(plan.Gateway.UseGatewayTransport);
            Assert.IsFalse(plan.Gateway.AutoCreateRoom);
            Assert.IsFalse(plan.Gateway.AutoJoinRoom);
        }

        [Test]
        public void ExistingGatewayRoomBattleBootstrapper_RebindsPresetLoadoutsToAuthoritativePlayerIds()
        {
            var configuredPlayers = new[]
            {
                new MobaPlayerLoadout(
                    new PlayerId("p1"), 1, 1001, 2001, 3, 3001, new[] { 4001 }, 0,
                    hasSpawnPosition: 1, spawnX: 10f, spawnZ: 20f),
                new MobaPlayerLoadout(
                    new PlayerId("p2"), 2, 1002, 2002, 4, 3002, new[] { 4002 }, 1,
                    hasSpawnPosition: 1, spawnX: -10f, spawnZ: -20f)
            };
            var sourceLaunchSpec = new MobaBattleLaunchSpec(
                battleId: "preset-battle",
                matchId: "preset-match",
                worldId: "preset-world",
                worldType: "battle",
                clientId: "client_1",
                localPlayerId: new PlayerId("p1"),
                mapId: 1,
                gameplayId: 101,
                ruleSetId: 201,
                configVersion: 301,
                protocolVersion: 401,
                randomSeed: 501,
                tickRate: 30,
                inputDelayFrames: 2,
                launchMode: MobaBattleLaunchMode.RoomFlow,
                syncMode: MobaBattleLaunchSyncMode.StateSync,
                authorityMode: MobaBattleLaunchAuthorityMode.ServerAuthority,
                players: configuredPlayers);
            var sourcePlan = BattleStartPlanBuilder
                .ForWorld("preset-world", "battle", "client_1", "p1", 30, 2)
                .WithLaunchSpec(in sourceLaunchSpec)
                .Build();
            var roomPlayers = new[]
            {
                new MultiplayerRoomPlayerSnapshot
                {
                    PlayerId = 41u,
                    TeamId = 2,
                    HeroId = 1101,
                    AttributeTemplateId = 2101,
                    Level = 5,
                    BasicAttackSkillId = 3101,
                    SkillIds = new[] { 4101, 4102 }
                },
                new MultiplayerRoomPlayerSnapshot
                {
                    PlayerId = 42u,
                    TeamId = 1,
                    HeroId = 1102,
                    AttributeTemplateId = 2102,
                    Level = 6,
                    BasicAttackSkillId = 3102,
                    SkillIds = new[] { 4201 }
                }
            };
            var bootstrapper = new ExistingGatewayRoomBattleBootstrapper(
                new FixedBattleBootstrapper(sourcePlan),
                "token",
                "room-id",
                "battle-id",
                9001UL,
                7001UL,
                42u,
                players: roomPlayers);

            var plan = bootstrapper.Build();

            Assert.AreEqual("battle-id", plan.LaunchSpec.BattleId);
            Assert.AreEqual("battle-id", plan.LaunchSpec.MatchId);
            Assert.AreEqual("7001", plan.LaunchSpec.WorldId);
            Assert.AreEqual("42", plan.LaunchSpec.LocalPlayerId.Value);
            Assert.AreEqual("41", plan.LaunchSpec.Players[0].PlayerId.Value);
            Assert.AreEqual("42", plan.LaunchSpec.Players[1].PlayerId.Value);
            Assert.AreEqual(1101, plan.LaunchSpec.Players[0].HeroId);
            Assert.AreEqual(2102, plan.LaunchSpec.Players[1].AttributeTemplateId);
            Assert.AreEqual(0, plan.LaunchSpec.Players[0].HasSpawnPosition);
            Assert.AreEqual(0, plan.LaunchSpec.Players[1].HasSpawnPosition);
            Assert.AreEqual(roomPlayers[0].SpawnPointId, plan.LaunchSpec.Players[0].SpawnIndex);
            Assert.AreEqual(roomPlayers[1].SpawnPointId, plan.LaunchSpec.Players[1].SpawnIndex);
            CollectionAssert.AreEqual(new[] { 4101, 4102 }, plan.LaunchSpec.Players[0].SkillIds);
            Assert.IsTrue(MobaCreateWorldInitCodec.TryDeserialize(
                plan.CreateWorld.Payload,
                out var createWorldInit,
                out var createWorldError), createWorldError);
            Assert.AreEqual("42", createWorldInit.LocalPlayerId.Value);
            Assert.AreEqual("41", createWorldInit.Spec.Players[0].PlayerId.Value);
            Assert.AreEqual("42", createWorldInit.Spec.Players[1].PlayerId.Value);
        }

        [Test]
        public void GatewayRoomPreparationController_RunsCreateBeforeJoinBranch()
        {
            var calls = new List<string>();
            var plan = BattleStartPlanBuilder
                .ForWorld("1001", "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: 1001,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: true,
                    autoJoinRoom: true,
                    joinRoomId: string.Empty,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();

            GatewayRoomPreparationController.RunAsync(
                    getPlan: () => plan,
                    waitForConnectionAsync: () => { calls.Add("wait"); return Task.CompletedTask; },
                    ensureSessionTokenAsync: () => { calls.Add("token"); return Task.CompletedTask; },
                    createAndJoinRoomAsync: () => { calls.Add("create"); return Task.CompletedTask; },
                    joinRoomAsync: () => { calls.Add("join"); return Task.CompletedTask; })
                .GetAwaiter()
                .GetResult();

            CollectionAssert.AreEqual(new[] { "wait", "token", "create" }, calls);
        }

        [Test]
        public void BattleWorldFloatingTextFactory_ReusesReleasedInstance()
        {
            var factory = new BattleWorldFloatingTextFactory();
            BattleWorldFloatingText first = null;
            BattleWorldFloatingText second = null;

            try
            {
                first = factory.Create("100", Vector3.zero, Color.red);
                var firstGameObject = first.GameObject;

                factory.Release(first);
                second = factory.Create("200", Vector3.one, Color.green);

                Assert.AreSame(first, second);
                Assert.AreSame(firstGameObject, second.GameObject);
                Assert.IsTrue(second.GameObject.activeSelf);
                Assert.AreEqual("200", second.Text.text);
                Assert.AreEqual(Vector3.one, second.GameObject.transform.position);
                Assert.AreEqual(Color.green, second.BaseColor);
                Assert.AreEqual(0f, second.Age);
            }
            finally
            {
                DestroyIfAlive(second);
                if (!ReferenceEquals(first, second)) DestroyIfAlive(first);
                factory.ClearPool();
            }
        }

        [Test]
        public void BattleFloatingTextStore_ReleasesExpiredTextInsteadOfDestroying()
        {
            var releaseCount = 0;
            BattleWorldFloatingText released = null;
            var store = new BattleFloatingTextStore(text =>
            {
                releaseCount++;
                released = text;
                text.Deactivate();
            });

            var floatingText = CreateFloatingText(lifetime: 0.1f);

            try
            {
                store.Add(floatingText);
                store.Tick(0.2f);

                Assert.AreEqual(1, releaseCount);
                Assert.AreSame(floatingText, released);
                Assert.IsNotNull(floatingText.GameObject);
                Assert.IsFalse(floatingText.GameObject.activeSelf);
            }
            finally
            {
                DestroyIfAlive(floatingText);
            }
        }

        [Test]
        public void ViewTimelineRuntimeOperation_SkipsWhenFrameAlreadyAligned()
        {
            var decision = ViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 12, lastAlignedFrame: 12, tickRate: 30);

            Assert.IsFalse(decision.ShouldSeek);
            Assert.AreEqual(12, decision.Frame);
            Assert.AreEqual(0f, decision.SecondsPerFrame);
        }

        [Test]
        public void ViewTimelineRuntimeOperation_UsesZeroSecondsPerFrameForInvalidTickRate()
        {
            var decision = ViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 13, lastAlignedFrame: 12, tickRate: 0);

            Assert.IsTrue(decision.ShouldSeek);
            Assert.AreEqual(13, decision.Frame);
            Assert.AreEqual(0f, decision.SecondsPerFrame);
        }

        [Test]
        public void ViewTimelineRuntimeOperation_ResolvesSecondsPerFrameFromTickRate()
        {
            var decision = ViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 13, lastAlignedFrame: 12, tickRate: 25);

            Assert.IsTrue(decision.ShouldSeek);
            Assert.AreEqual(13, decision.Frame);
            Assert.AreEqual(0.04f, decision.SecondsPerFrame, 0.0001f);
        }

        private static bool ContainsError(IReadOnlyList<string> errors, string expected)
        {
            for (var i = 0; i < errors.Count; i++)
            {
                if (string.Equals(errors[i], expected, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static EC.IEntityId GetActiveCueEntityId(
            BattlePresentationCueViewEventHandler handler,
            BattlePresentationCueRequestKey requestKey)
        {
            var field = typeof(BattlePresentationCueViewEventHandler).GetField(
                "_activeByRequestKey",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Cue handler active-request map was not found.");

            var active = field.GetValue(handler) as Dictionary<BattlePresentationCueRequestKey, EC.IEntityId>;
            Assert.IsNotNull(active, "Cue handler active-request map has an unexpected type.");
            if (active.TryGetValue(requestKey, out var entityId)) return entityId;

            throw new System.InvalidOperationException("Cue handler has no active entity for request key.");
        }

        private static void InvokePrivate(object target, string methodName)
        {
            InvokePrivate(target, methodName, Array.Empty<object>());
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return method.Invoke(target, args);
        }

        private static void SetPrivatePlan(BattleSessionFeature feature, BattleStartPlan plan)
        {
            var backingProperty = typeof(BattleSessionFeature).GetProperty(
                "_plan",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(backingProperty, "_plan");
            backingProperty.SetValue(feature, plan);
            Assert.AreEqual(plan, feature.Plan);
        }

        private static void InitializeDispatchers(BattleSessionFeature feature)
        {
            var featureType = typeof(BattleSessionFeature);
            var dispatchersField = featureType.GetField(
                "_dispatchers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var handlesField = featureType.GetField(
                "_handles",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(dispatchersField, "_dispatchers");
            Assert.IsNotNull(handlesField, "_handles");

            var dispatchers = dispatchersField.GetValue(feature);
            var handles = handlesField.GetValue(feature);
            var onAttach = dispatchers.GetType().GetMethod("OnAttach");
            Assert.IsNotNull(onAttach, "SessionDispatchersController.OnAttach");
            onAttach.Invoke(dispatchers, new[] { handles });
        }

        private static BattleStartPlan CreateGatewayPlan(string worldId, ulong numericRoomId, string joinRoomId)
        {
            return BattleStartPlanBuilder
                .ForWorld(worldId, "battle", "client_1", "7", tickRate: 30, inputDelayFrames: 0)
                .WithHostMode(BattleStartConfig.BattleHostMode.GatewayRemote)
                .WithGateway(
                    useGatewayTransport: true,
                    host: "127.0.0.1",
                    port: 4000,
                    numericRoomId: numericRoomId,
                    sessionToken: "token",
                    region: "dev",
                    serverId: "local",
                    autoCreateRoom: numericRoomId == 0 && string.IsNullOrEmpty(joinRoomId),
                    autoJoinRoom: true,
                    joinRoomId: joinRoomId,
                    createRoomOpCode: 110,
                    joinRoomOpCode: 111)
                .Build();
        }

        private static PresentationCueData CreatePresentationCue(
            PresentationCueStage stage,
            string requestKey,
            int vfxId,
            int templateId,
            int sourceActorId = 0,
            int targetActorId = 0,
            int[] targets = null,
            SnapshotVec3[] positions = null,
            float offsetX = 0f,
            float offsetY = 0f,
            float offsetZ = 0f,
            float scale = 0f,
            int durationMsOverride = 0,
            IReadOnlyList<int> numericParamKeys = null,
            IReadOnlyList<float> numericParamValues = null)
        {
            return new PresentationCueData(
                stage,
                cueKind: null,
                cueVfxId: null,
                cueSfxId: null,
                templateId,
                vfxId,
                sfxId: 0,
                requestKey,
                sourceActorId,
                targetActorId,
                triggerEventId: 0,
                triggerEventName: null,
                triggerId: 0,
                phase: 0,
                priority: 0,
                order: 0,
                actionIndex: 0,
                interruptReason: 0,
                interruptSourceName: null,
                interruptTriggerId: 0,
                interruptConditionPassed: false,
                targets ?? Array.Empty<int>(),
                positions ?? Array.Empty<SnapshotVec3>(),
                offsetX,
                offsetY,
                offsetZ,
                durationMsOverride: durationMsOverride,
                scale: scale,
                colorR: 0f,
                colorG: 0f,
                colorB: 0f,
                colorA: 0f,
                ownerKind: null,
                instanceId: 0,
                instanceKey: null,
                stackCount: 0,
                maxStackCount: 0,
                elapsedSeconds: 0f,
                remainingSeconds: 0f,
                lifecycleReason: 0,
                numericParamKeys: numericParamKeys,
                numericParamValues: numericParamValues);
        }

        private static BattleWorldFloatingText CreateFloatingText(float lifetime)
        {
            var go = new GameObject("FloatingTextTest");
            var textMesh = go.AddComponent<TextMesh>();
            return new BattleWorldFloatingText
            {
                GameObject = go,
                Text = textMesh,
                Lifetime = lifetime,
                Velocity = Vector3.zero,
                BaseColor = Color.white,
            };
        }

        private static void DestroyIfAlive(BattleWorldFloatingText floatingText)
        {
            if (floatingText?.GameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(floatingText.GameObject);
                floatingText.GameObject = null;
                floatingText.Text = null;
            }
        }
    
        private sealed class TestSnapshotBufferEmitter :
            LogicWorldSnapshotBufferEmitterBase<TestSnapshotBufferEmitter, int>
        {
            public TestSnapshotBufferEmitter() : base(1, 4)
            {
            }

            public void Enqueue(int opCode)
            {
                Add(opCode);
            }

            protected override WorldStateSnapshot CreateSnapshot(int[] entries)
            {
                return new WorldStateSnapshot(entries[0], Array.Empty<byte>());
            }
        }

        private sealed class FixedBattleBootstrapper : IBattleBootstrapper
        {
            private readonly BattleStartPlan _plan;

            public FixedBattleBootstrapper(BattleStartPlan plan)
            {
                _plan = plan;
            }

            public BattleStartPlan Build()
            {
                return _plan;
            }
        }

        private sealed class TestMobaFeatureFactoryProvider : IMobaFeatureFactoryProvider
        {
            public readonly List<string> CreatedFeatureIds = new List<string>();

            public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(Func<IBattleSessionFeature> createBattleSessionFeature)
            {
                var registry = new MobaFeatureFactoryRegistry();
                Register(registry, "boot_menu");
                Register(registry, "demo_lobby");
                Register(registry, "formal_lobby");
                Register(registry, "root_debug");
                Register(registry, "debug_ongui");
                Register(registry, "context");
                Register(registry, "entity");
                Register(registry, "sync");
                Register(registry, "input");
                Register(registry, "view");
                Register(registry, "hud");
                registry.Register("session", (in AbilityKit.Game.Flow.GamePhaseContext ctx) =>
                {
                    CreatedFeatureIds.Add("session");
                    return createBattleSessionFeature();
                });
                return registry;
            }

            private void Register(MobaFeatureFactoryRegistry registry, string id)
            {
                registry.Register(id, (in AbilityKit.Game.Flow.GamePhaseContext ctx) =>
                {
                    CreatedFeatureIds.Add(id);
                    return new TestGamePhaseFeature(id);
                });
            }
        }

        private sealed class TestBattleSessionFeatureFactory : IBattleSessionFeatureFactory
        {
            public int CreateCount { get; private set; }
            public IBattleSessionFeature LastCreatedFeature { get; private set; }

            public IBattleSessionFeature Create(IBattleBootstrapper bootstrapper, Func<BattleStartPlan, IConnection> gatewayConnectionFactory)
            {
                CreateCount++;
                LastCreatedFeature = new TestBattleSessionFeature();
                return LastCreatedFeature;
            }
        }

        private sealed class TestBattleSessionWorldInstaller : IBattleSessionWorldInstaller
        {
            public int RemoteDrivenStartCount { get; private set; }
            public int ConfirmedAuthorityStartCount { get; private set; }

            public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
            {
                RemoteDrivenStartCount++;
            }

            public void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options)
            {
                ConfirmedAuthorityStartCount++;
            }
        }

        private sealed class TestBattleSessionTransportFactory : IBattleSessionTransportFactory
        {
            private readonly TestBattleLogicTransport _transport = new TestBattleLogicTransport();

            public int CreateCount { get; private set; }
            public BattleStartPlan LastPlan { get; private set; }
            public uint LastLocalPlayerId { get; private set; }
            public ulong LastRoomId { get; private set; }
            public IDispatcher LastCallbackDispatcher { get; private set; }
            public IDispatcher LastIoDispatcher { get; private set; }

            public IBattleLogicTransport CreateGatewayRemoteTransport(
                BattleStartPlan plan,
                uint localPlayerId,
                ulong roomId,
                IDispatcher callbackDispatcher,
                IDispatcher ioDispatcher)
            {
                CreateCount++;
                LastPlan = plan;
                LastLocalPlayerId = localPlayerId;
                LastRoomId = roomId;
                LastCallbackDispatcher = callbackDispatcher;
                LastIoDispatcher = ioDispatcher;
                return _transport;
            }
        }

        private sealed class TestBattleLogicTransport : IBattleLogicTransport
        {
            public event Action<FramePacket> FramePushed;

            public void Connect() { }
            public void Disconnect() { }
            public void SendCreateWorld(CreateWorldRequest request) { }
            public void SendJoin(JoinWorldRequest request) { }
            public void SendLeave(LeaveWorldRequest request) { }
            public void SendInput(SubmitInputRequest request) { }
        }

        private sealed class TestBattleSessionGatewayConnectionFactory : IBattleSessionGatewayConnectionFactory
        {
            public TestConnection Connection { get; } = new TestConnection();
            public int CreateCount { get; private set; }
            public BattleStartPlan LastPlan { get; private set; }
            public IDispatcher LastCallbackDispatcher { get; private set; }
            public IDispatcher LastIoDispatcher { get; private set; }

            public IConnection CreateGatewayRoomConnection(
                BattleStartPlan plan,
                IDispatcher callbackDispatcher,
                IDispatcher ioDispatcher)
            {
                CreateCount++;
                LastPlan = plan;
                LastCallbackDispatcher = callbackDispatcher;
                LastIoDispatcher = ioDispatcher;
                return Connection;
            }
        }

        private sealed class TestBattleSessionGatewayRoomClientFactory : IBattleSessionGatewayRoomClientFactory
        {
            public int CreateCount { get; private set; }
            public IConnection LastConnection { get; private set; }
            public GatewayRoomOpCodes LastOpCodes { get; private set; }

            public IGatewayRoomClient CreateGatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
            {
                CreateCount++;
                LastConnection = connection;
                LastOpCodes = opCodes;
                return new TestGatewayRoomClient();
            }
        }

        private sealed class TestGatewayRoomClient : IGatewayRoomClient
        {
            public Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<string> GuestLoginAsync(uint guestLoginOpCode, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayCreateRoomResult> CreateRoomAsync(
                string sessionToken,
                string region,
                string serverId,
                string roomType,
                string title,
                bool isPublic,
                int maxPlayers,
                IReadOnlyDictionary<string, string> tags,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayJoinRoomResult> JoinRoomAsync(
                string sessionToken,
                string region,
                string serverId,
                string roomId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomSnapshotResult> SetReadyAsync(
                string sessionToken,
                string roomId,
                bool ready,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomSnapshotResult> PickHeroAsync(
                string sessionToken,
                string roomId,
                int heroId,
                int teamId,
                int spawnPointId,
                int level,
                int attributeTemplateId,
                int basicAttackSkillId,
                IReadOnlyList<int> skillIds,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> BeginLoadingAsync(
                string sessionToken,
                string roomId,
                long? expectedRevision,
                string commandId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
                string sessionToken,
                string roomId,
                long launchGeneration,
                int manifestVersion,
                string manifestHash,
                string commandId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(
                string sessionToken,
                string roomId,
                long launchGeneration,
                int manifestVersion,
                string manifestHash,
                int progress,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> LeaveRoomAsync(
                string sessionToken,
                string roomId,
                long? expectedRevision,
                string commandId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> CancelLoadingAsync(
                string sessionToken,
                string roomId,
                long? expectedRevision,
                string commandId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayGetSnapshotResult> GetSnapshotAsync(
                string sessionToken,
                string roomId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<GatewayRestoreRoomResult> RestoreRoomAsync(
                string sessionToken,
                string region,
                string serverId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload)
                => throw new NotSupportedException();

            public bool IsRoomStateChangedPush(uint opCode)
                => false;
        }
 
        private sealed class TestAbilityKitConnectionRegistry : IAbilityKitConnectionRegistry
        {
            private readonly bool _removeResult;

            public TestAbilityKitConnectionRegistry(bool removeResult)
            {
                _removeResult = removeResult;
            }

            public AbilityKitConnectionRole LastRemovedRole { get; private set; }
            public bool LastRemoveDispose { get; private set; }

            public bool TryGet(AbilityKitConnectionRole role, out IConnection connection)
            {
                connection = null;
                return false;
            }

            public IConnection GetRequired(AbilityKitConnectionRole role) => throw new InvalidOperationException();

            public void Register(AbilityKitConnectionRole role, IConnection connection, bool disposeOnReplace = true) { }

            public IConnection GetOrCreate(AbilityKitConnectionDescriptor descriptor, Func<AbilityKitConnectionDescriptor, IConnection> factory)
            {
                return factory(descriptor);
            }

            public bool Remove(AbilityKitConnectionRole role, bool dispose = true)
            {
                LastRemovedRole = role;
                LastRemoveDispose = dispose;
                return _removeResult;
            }

            public void Dispose() { }
        }

        private sealed class TestConnection : IConnection
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

            public void Tick(float deltaTime) { }
            public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) { }
            public void Dispose() => Close();
        }

        private sealed class TestGamePhaseFeature : AbilityKit.Game.Flow.IGamePhaseFeature
        {
            public TestGamePhaseFeature(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public string Name => Id;
            public int Priority => 0;
            public bool IsEnabled { get; set; } = true;
            public void OnAttach(in AbilityKit.Game.Flow.GamePhaseContext ctx) { }
            public void OnDetach(in AbilityKit.Game.Flow.GamePhaseContext ctx) { }
            public void Tick(in AbilityKit.Game.Flow.GamePhaseContext ctx, float deltaTime) { }
        }

        private sealed class TestBattleSessionFeature : IBattleSessionFeature
        {
            public event Action SessionStarted;
            public event Action FirstFrameReceived;
            public event Action<Exception> SessionFailed;
            public event Action AssetsLoadCompleted;

            public int SubscriberCount =>
                (SessionStarted?.GetInvocationList().Length ?? 0) +
                (FirstFrameReceived?.GetInvocationList().Length ?? 0) +
                (SessionFailed?.GetInvocationList().Length ?? 0) +
                (AssetsLoadCompleted?.GetInvocationList().Length ?? 0);

            public string Name => "test_session";
            public int Priority => 0;
            public bool IsEnabled { get; set; } = true;

            public Action CaptureSessionStarted() => SessionStarted;

            public void RaiseSessionStarted() => SessionStarted?.Invoke();

            public void RaiseAll()
            {
                SessionStarted?.Invoke();
                FirstFrameReceived?.Invoke();
                SessionFailed?.Invoke(new InvalidOperationException("test"));
                AssetsLoadCompleted?.Invoke();
            }

            public void OnAttach(in AbilityKit.Game.Flow.GamePhaseContext ctx) { }
            public void OnDetach(in AbilityKit.Game.Flow.GamePhaseContext ctx) { }
            public void Tick(in AbilityKit.Game.Flow.GamePhaseContext ctx, float deltaTime) { }
        }
    
            private sealed class TestGameFlowRuntimeServices : IGameFlowRuntimeServices
            {
                public TestGameFlowRuntimeServices()
                {
                    FeatureBinder = new TestFeatureBinder();
                    Features = new TestGameFeatureStore();
                    BattleEntities = new TestBattleEntityRuntime();
                }
    
                public IGameHost Host => null;
                public TestFeatureBinder FeatureBinder { get; }
                IFeatureBinder IGameFlowRuntimeServices.FeatureBinder => FeatureBinder;
                public IGameFeatureStore Features { get; }
                public IBattleEntityRuntime BattleEntities { get; }
                public int LoadPersistentSettingsCount { get; private set; }
                public int LoadPersistentSettingsSyncCount { get; private set; }
                public LayeredJsonSettingsStore LastAsyncSettings { get; private set; }
                public LayeredJsonSettingsStore LastSyncSettings { get; private set; }
    
                public void LoadPersistentSettings(LayeredJsonSettingsStore settings)
                {
                    LoadPersistentSettingsCount++;
                    LastAsyncSettings = settings;
                }
    
                public void LoadPersistentSettingsSync(LayeredJsonSettingsStore settings)
                {
                    LoadPersistentSettingsSyncCount++;
                    LastSyncSettings = settings;
                }
    
                public bool TrySaveSettingsOverridesToPersistent(LayeredJsonSettingsStore settings)
                {
                    return true;
                }
            }
    
            private sealed class TestFeatureBinder : IFeatureBinder
            {
                public int AttachCount { get; private set; }
                public int DetachCount { get; private set; }
                public object LastAttachedFeature { get; private set; }
                public void AttachFeature(object feature)
                {
                    AttachCount++;
                    LastAttachedFeature = feature;
                }
                public void DetachFeature(object feature) => DetachCount++;
            }
    
            private sealed class TestGameFeatureStore : IGameFeatureStore
            {
                private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();
    
                public bool TryGet<T>(out T component) where T : class
                {
                    if (_components.TryGetValue(typeof(T), out var value))
                    {
                        component = (T)value;
                        return true;
                    }
    
                    component = null;
                    return false;
                }
    
                public void Set<T>(T component) where T : class => _components[typeof(T)] = component;
                public void Remove<T>() where T : class => _components.Remove(typeof(T));
                public void Remove(Type componentType) => _components.Remove(componentType);
            }
    
            private sealed class TestBattleEntityRuntime : IBattleEntityRuntime
            {
                public bool TryGetWorld<TWorld>(out TWorld world)
                {
                    world = default;
                    return false;
                }
    
                public bool TryCreateNode<TNode>(string debugName, out TNode node)
                {
                    node = default;
                    return false;
                }
    
                public void DestroyTree<TNode>(TNode root) { }
            }
    
            private sealed class TestLogSink : ILogSink
            {
                public void Info(string message) { }
                public void Warning(string message) { }
                public void Error(string message) { }
                public void Exception(Exception exception, string message = null) { }
            }
    }
}
