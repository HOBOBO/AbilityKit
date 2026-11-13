using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using AbilityKit.Combat.Projectile;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// FullSnapshot → 逻辑世界状态导入（MobaLogicWorldStateImporter）冒烟验证。
///
/// 在真实 Console 战斗世界中：
/// 1. 扰动全部 actor 的 transform/HP（模拟断线期间的状态漂移）
/// 2. 用扰动前捕获的快照导入 —— 验证状态恢复
/// 3. 导入含新 actor 的快照 —— 验证缺失 actor 生成
/// 4. 导入不含该 actor 的全量快照 —— 验证多余 actor 移除
/// </summary>
[Collection("Moba lifecycle preparation")]
public sealed class MobaLogicWorldStateImportSmokeTests
{
    private readonly ITestOutputHelper _output;
    public MobaLogicWorldStateImportSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Rollback_restore_realigns_console_and_world_frame_clocks_before_next_tick()
    {
        var bootstrapper = new ConsoleBattleBootstrapper(
            BattleStartConfig.CreateDefault(),
            additionalModules: new[] { new MobaTraceAdapterModule() });
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrapper.Tick();

            var runtimeWorld = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(bootstrapper)!;
            var registry = MobaRollbackRegistryBuilder.Create(runtimeWorld);
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(8));
            var checkpoint = new FrameIndex(bootstrapper.Context.LastFrame);

            Assert.True(coordinator.CaptureAndStore(checkpoint));
            bootstrapper.Tick(1f / 30f);
            bootstrapper.Tick(1f / 30f);
            Assert.True(bootstrapper.Context.LastFrame > checkpoint.Value);

            Assert.True(bootstrapper.TryRestoreSimulationFrame(coordinator, checkpoint, out var result), result.ToString());
            Assert.True(result.IsSuccess, result.ToString());
            Assert.Equal(checkpoint.Value, bootstrapper.Context.LastFrame);
            Assert.True(bootstrapper.RuntimeServices!.TryResolve<IFrameTime>(out var restoredClock));
            Assert.Equal(checkpoint, restoredClock!.Frame);

            bootstrapper.Tick(1f / 30f);

            Assert.Equal(checkpoint.Value + 1, bootstrapper.Context.LastFrame);
            Assert.Equal(new FrameIndex(checkpoint.Value + 1), restoredClock.Frame);
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }

    [Fact]
    public void Annie_q_precast_checkpoint_restores_complete_lifecycle_through_formal_registry()
    {
        var bootstrapper = new ConsoleBattleBootstrapper(
            BattleStartConfig.CreateDefault(),
            additionalModules: new[] { new MobaTraceAdapterModule() });
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrapper.Tick();

            var executor = new LiveSimSetupActionExecutor(bootstrapper) { FixedDelta = 1f / 30f };
            executor.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = "annie", playerId = "player_1", teamId = 1,
                heroId = 3003, attributeTemplateId = 3003,
                position = new MobaAcceptanceVector3Expectation { x = 0f, y = 0f, z = 0f },
            });
            executor.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = "target", playerId = "player_2", teamId = 2,
                heroId = 1001, attributeTemplateId = 1001,
                position = new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = 0f },
            });

            Assert.True(executor.TryGetActorId("annie", out var annieId));
            Assert.True(executor.TryGetActorId("target", out var targetId));
            // Complete the normal actor/passive initialization tick before the
            // checkpoint. This scenario requires an unchanged subscription set.
            bootstrapper.Tick(1f / 30f);
            var beforeHp = executor.GetActorHp(targetId);
            var beforeMana = executor.GetActorMana(annieId);
            var runtimeWorld = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(bootstrapper)!;
            var rollbackRegistry = MobaRollbackRegistryBuilder.Create(runtimeWorld);
            Assert.True(rollbackRegistry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var group));
            var lifecycleProvider = Assert.IsType<MobaSkillLifecycleRollbackProvider>(group);
            Assert.IsAssignableFrom<IRollbackStructureRestoreProvider>(group);
            Assert.False(rollbackRegistry.TryGet(MobaBuffLifecycleRollbackProvider.DefaultKey, out _));
            var coordinator = new RollbackCoordinator(
                rollbackRegistry,
                new RollbackSnapshotRingBuffer(16));
            var checkpoint = new FrameIndex(bootstrapper.Context.LastFrame);
            Assert.True(coordinator.CaptureAndStore(checkpoint));
            var blackboards = lifecycleProvider.OwnerBlackboards;
            var lifecycle = new MobaSkillLifecycleRollbackPreparation(runtimeWorld,
                (MobaOwnerBlackboardRollbackProvider)blackboards);
            var lifecycleCheckpoint = lifecycle.Capture(checkpoint);
            var actorRegistry = runtimeWorld.Services.Resolve<MobaActorRegistry>();
            var checkpointActors = new HashSet<int>(actorRegistry.CopyActorIdsInOrder());
            var ownerCheckpoint = blackboards.Export(checkpoint);
            Assert.True(bootstrapper.RuntimeServices!.TryResolve<IOwnerBlackboardStore>(out var ownerStore));
            Assert.False(ownerStore!.TryGet(annieId, out _));
            Assert.True(bootstrapper.RuntimeServices!.TryResolve<MobaContinuousManager>(out var continuous));
            var initialContinuous = continuous!.GetAllContinuous().ToArray();
            var timeline = new LiveSimTimelineRunner(bootstrapper, executor);
            timeline.Run(new[]
            {
                new MobaAcceptanceTimelineStepExpectation
                {
                    atMs = 100, action = "cast_skill", actorAlias = "annie", targetAlias = "target", slot = 1,
                },
            });
            var inFlightCheckpoint = new FrameIndex(bootstrapper.Context.LastFrame);
            var inFlightCapture = coordinator.TryCaptureAndStore(inFlightCheckpoint, out var inFlightCaptureResult);
            Assert.True(inFlightCapture, $"Annie Q in-flight rollback capture failed at frame {inFlightCheckpoint.Value}: {inFlightCaptureResult}.");
            var lifecycleInFlight = lifecycle.Capture(inFlightCheckpoint);
            var temporaryActors = actorRegistry.Entries.Where(pair => !checkpointActors.Contains(pair.Key)).ToArray();
            Assert.NotEmpty(temporaryActors);
            foreach (var actor in temporaryActors)
            {
                Assert.True(actor.Value.hasEntityMainType && actor.Value.entityMainType.Value == EntityMainType.Projectile);
                _output.WriteLine($"In-flight temporary actor: id={actor.Key}, type={actor.Value.entityMainType.Value}");
            }
            var preparedInFlight = lifecycle.Prepare(inFlightCheckpoint, lifecycleInFlight);
            Assert.True(preparedInFlight.SkillRuntimeCount > 0);
            Assert.Equal(lifecycleInFlight, lifecycle.Capture(inFlightCheckpoint));

            timeline.Run(new[]
            {
                new MobaAcceptanceTimelineStepExpectation
                {
                    atMs = 1800, action = "wait", note = "Allow Annie Q cast and projectile hit to resolve.",
                },
            });
            Assert.True(bootstrapper.RuntimeServices!.TryResolve<MobaTraceRegistry>(out var trace) && trace != null,
                "Runtime trace registry must be resolved for the real skill replay test.");
            Assert.Contains(trace!.GetNodesByKind((int)MobaExecutionKind.SkillCast),
                node => node.Metadata != null && node.Metadata.ConfigId == 30030201);
            var firstHp = executor.GetActorHp(targetId);
            var firstMana = executor.GetActorMana(annieId);
            Assert.True(firstHp < beforeHp, $"Annie Q did not damage its target. hp={beforeHp}->{firstHp}");
            Assert.True(firstMana < beforeMana, $"Annie Q did not consume mana. mana={beforeMana}->{firstMana}");

            var changedContinuous = continuous.GetAllContinuous().Except(initialContinuous).ToArray();
            foreach (var item in changedContinuous)
            {
                var projection = item.Config as IMobaContinuousProjectionConfig;
                _output.WriteLine($"New continuous: {item.GetType().Name}, config={item.Config.Id}, owner={projection?.OwnerActorId}, source={projection?.ModifierSourceId}, target={projection?.TargetActorId}");
            }
            Assert.NotEmpty(changedContinuous);
            Assert.Contains(changedContinuous, item => item is BuffContinuousRuntime buff &&
                buff.ModifierSourceId > 0 && item.Config is IMobaContinuousModifierConfig specs && specs.Modifiers.Count > 0);
            var currentFrame = bootstrapper.Context.LastFrame;
            var ownerAfterCast = blackboards.Export(checkpoint);
            var lifecycleCurrentFrame = new FrameIndex(currentFrame);
            var lifecycleBeforePreflight = lifecycle.Capture(lifecycleCurrentFrame);
            var preparedPrecast = lifecycle.Prepare(checkpoint, lifecycleCheckpoint);
            // The in-flight World also owns temporary projectile actors. Their later removal
            // is outside the approved same-roster lifecycle group.
            var rosterError = Assert.Throws<InvalidOperationException>(() => lifecycle.Prepare(inFlightCheckpoint, lifecycleInFlight));
            Assert.Contains("same actor roster", rosterError.Message);
            Assert.True(preparedInFlight.SkillRuntimeCount > preparedPrecast.SkillRuntimeCount);
            Assert.Equal(lifecycleBeforePreflight, lifecycle.Capture(lifecycleCurrentFrame));
            Assert.Equal(lifecycleCheckpoint, preparedPrecast.CopyCheckpoint());
            Assert.True(ownerStore.TryGet(annieId, out _));
            ((IRollbackStatePreflightProvider)blackboards).ValidateImport(checkpoint, ownerCheckpoint);
            Assert.Equal(ownerAfterCast, blackboards.Export(checkpoint));

            var endedDuringRestore = new List<long>();
            using var endObservation = runtimeWorld.Services.Resolve<MobaExecutionContextRegistry>().Observe(
                (in AbilityKit.Context.ContextLifecycleEvent<MobaExecutionContextNode> change) =>
                {
                    if (change.Kind == AbilityKit.Context.ContextLifecycleEventKind.Ended)
                        endedDuringRestore.Add(change.Node.ContextId);
                }, replayExisting: false);
            Assert.True(bootstrapper.TryRestoreSimulationFrame(coordinator, checkpoint, out var restoreResult), restoreResult.ToString());
            Assert.Equal(checkpoint.Value, bootstrapper.Context.LastFrame);
            Assert.Equal(beforeHp, executor.GetActorHp(targetId));
            Assert.Equal(beforeMana, executor.GetActorMana(annieId));
            var restoredLifecycle = lifecycle.Capture(checkpoint);
            var expectedSections = MobaSkillLifecycleCheckpointCodec.ReadSections(MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(lifecycleCheckpoint));
            var actualSections = MobaSkillLifecycleCheckpointCodec.ReadSections(MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(restoredLifecycle));
            foreach (var section in expectedSections)
            {
                Assert.True(section.Value.SequenceEqual(actualSections[section.Key]), $"Restored lifecycle section differs: {section.Key}");
            }
            Assert.Empty(endedDuringRestore);
            Assert.Equal(lifecycleCheckpoint, restoredLifecycle);
            Assert.Equal(checkpointActors.OrderBy(id => id), actorRegistry.CopyActorIdsInOrder());
            Assert.False(ownerStore.TryGet(annieId, out _));
            Assert.Equal(ownerCheckpoint, blackboards.Export(checkpoint));
            Assert.True(bootstrapper.TryRestoreSimulationFrame(coordinator, checkpoint, out restoreResult), restoreResult.ToString());
            Assert.Equal(lifecycleCheckpoint, lifecycle.Capture(checkpoint));
            bootstrapper.Tick(1f / 30f);
            Assert.Equal(checkpoint.Value + 1, bootstrapper.Context.LastFrame);
            Assert.Equal(beforeHp, executor.GetActorHp(targetId));
            Assert.Equal(beforeMana, executor.GetActorMana(annieId));
            Assert.DoesNotContain(continuous.GetAllContinuous(), item => item is SkillPipelineContinuousRuntime pipeline && pipeline.SkillId == 30030201);
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }

    [Fact]
    public void StateImport_restores_perturbed_state_and_handles_spawn_despawn()
    {
        var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        try
        {
            bootstrapper.Initialize();
            bootstrapper.Start();
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
            {
                bootstrapper.Tick();
            }
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++)
            {
                bootstrapper.Tick();
            }

            var services = bootstrapper.RuntimeServices;
            Assert.NotNull(services);
            var runtimeWorld = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(bootstrapper)!;
            var rollbackRegistry = MobaRollbackRegistryBuilder.Create(runtimeWorld);
            Assert.NotEmpty(rollbackRegistry.Providers);
            Assert.True(rollbackRegistry.TryGet(ProjectileRollbackProvider.DefaultKey, out _),
                "Projectile simulation state must participate in the battle rollback registry.");
            Assert.True(services.TryResolve<MobaActorRegistry>(out var registry) && registry != null,
                "MobaActorRegistry not resolved from world services.");
            Assert.True(services.TryResolve<MobaLogicWorldStateImporter>(out var importer) && importer != null,
                "MobaLogicWorldStateImporter not resolved from world services.");

            var originals = CaptureStates(registry);
            Assert.NotEmpty(originals);
            var firstImport = originals.Values.First();
            Assert.True(registry.TryGet(firstImport.ActorId, out var firstActor));
            var positionBeforeInvalidImport = firstActor.transform.Value.Position;
            var invalidImport = importer.Import(
                new[] { firstImport, firstImport },
                bootstrapper.Context.LastFrame,
                isFullSnapshot: true);
            Assert.Equal(1, invalidImport.Failed);
            Assert.Equal(0, invalidImport.Updated);
            Assert.Equal(positionBeforeInvalidImport, firstActor.transform.Value.Position);
            var legacyExistingActor = new MobaActorStateImport(
                firstImport.ActorId,
                firstImport.PosX, firstImport.PosY, firstImport.PosZ,
                firstImport.Yaw, firstImport.Hp, firstImport.HpMax,
                firstImport.TeamId, kind: 0, firstImport.Code, firstImport.OwnerNetId);
            var legacyImport = importer.Import(
                new[] { legacyExistingActor },
                bootstrapper.Context.LastFrame,
                isFullSnapshot: false);
            Assert.Equal(1, legacyImport.Updated);
            Assert.Equal(0, legacyImport.Failed);

            var moveActor = registry.Entries
                .Select(pair => pair.Value)
                .First(e => e != null && e.hasTransform && e.hasMoveInput);
            var originalMoveDx = moveActor.moveInput.Dx;
            var originalMoveDz = moveActor.moveInput.Dz;
            moveActor.ReplaceMoveInput(0.75f, -0.25f);
            var rollbackProvider = new MobaActorTransformRollbackProvider(registry);
            var rollbackFrame = new FrameIndex(bootstrapper.Context.LastFrame);
            var rollbackPayload = rollbackProvider.Export(rollbackFrame);
            moveActor.ReplaceMoveInput(0f, 0f);

            rollbackProvider.Import(rollbackFrame, rollbackPayload);

            Assert.Equal(0.75f, moveActor.moveInput.Dx, 3);
            Assert.Equal(-0.25f, moveActor.moveInput.Dz, 3);
            moveActor.ReplaceMoveInput(originalMoveDx, originalMoveDz);

            // 扰动：所有 actor 平移 + HP 打到 1（模拟断线期间的状态漂移）
            foreach (var kv in originals)
            {
                Assert.True(registry.TryGet(kv.Key, out var e) && e != null);
                var t = e.transform.Value;
                e.ReplaceTransform(new Transform3(
                    new Vec3(t.Position.X + 50f, t.Position.Y, t.Position.Z + 50f),
                    t.Rotation,
                    t.Scale));
                if (e.hasAttributeGroup && e.attributeGroup.Group != null)
                {
                    e.attributeGroup.Group.SetBase(MobaAttributeIds.HP, 1f);
                }
            }

            // 导入扰动前快照 → 状态恢复
            var imports = originals.Values.ToArray();
            var frame = bootstrapper.Context.LastFrame;
            var result = importer.Import(imports, frame: frame, isFullSnapshot: true);

            Assert.Equal(originals.Count, result.Updated);
            Assert.Equal(0, result.Failed);

            foreach (var kv in originals)
            {
                Assert.True(registry.TryGet(kv.Key, out var e) && e != null);
                var s = kv.Value;
                var pos = e.transform.Value.Position;
                Assert.True(
                    Math.Abs(pos.X - s.PosX) < 0.001f && Math.Abs(pos.Z - s.PosZ) < 0.001f,
                    $"actor {kv.Key} position not restored: expected ({s.PosX},{s.PosZ}) actual ({pos.X},{pos.Z})");
                if (e.hasMotion)
                {
                    var motionPos = e.motion.State.Position;
                    Assert.True(
                        Math.Abs(motionPos.X - s.PosX) < 0.001f && Math.Abs(motionPos.Z - s.PosZ) < 0.001f,
                        $"actor {kv.Key} motion state not rebased: expected ({s.PosX},{s.PosZ}) actual ({motionPos.X},{motionPos.Z})");
                }
                if (e.hasAttributeGroup && e.attributeGroup.Group != null && s.HpMax > 0f)
                {
                    Assert.Equal(s.Hp, e.attributeGroup.Group.GetValue(MobaAttributeIds.HP), 3);
                }
            }

            // 导入含新 actor 的快照 → 缺失 actor 生成
            const int spawnId = 990001;
            var withNew = imports.Concat(new[]
            {
                new MobaActorStateImport(
                    spawnId, 1f, 0f, 1f, yaw: 0f, hp: 100f, hpMax: 100f,
                    teamId: 1, kind: 1, code: 1001, ownerNetId: 0)
            }).ToArray();
            var spawnResult = importer.Import(withNew, frame: frame + 1, isFullSnapshot: true);
            Assert.True(spawnResult.Spawned >= 1, $"expected spawn, got {spawnResult}");
            Assert.True(registry.TryGet(spawnId, out var spawned) && spawned != null,
                "newly imported actor not found in registry");

            // 导入不含该 actor 的全量快照 → 多余 actor 移除（despawn 在下一 tick 由清理系统执行）
            var despawnResult = importer.Import(imports, frame: frame + 2, isFullSnapshot: true);
            Assert.True(despawnResult.Despawned >= 1, $"expected despawn, got {despawnResult}");
            Assert.True(spawned.hasActorDespawnRequest,
                $"despawn request not queued. lastFrame={bootstrapper.Context.LastFrame} importFrame={frame + 2}");
            for (var i = 0; i < 8; i++)
            {
                bootstrapper.Tick();
            }

            var confirmedInfo = "unresolved";
            if (services.TryResolve<AbilityKit.Demo.Moba.Services.MobaAuthorityFrameService>(out var authority) && authority != null)
            {
                confirmedInfo = $"confirmed={authority.ConfirmedFrame.Value} predicted={authority.PredictedFrame.Value}";
            }

            Assert.False(registry.TryGet(spawnId, out _),
                $"despawned actor still in registry. lastFrame={bootstrapper.Context.LastFrame} importFrame={frame + 2} hasRequest={spawned.hasActorDespawnRequest} {confirmedInfo}");
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }

    private static Dictionary<int, MobaActorStateImport> CaptureStates(MobaActorRegistry registry)
    {
        var states = new Dictionary<int, MobaActorStateImport>();
        foreach (var kv in registry.Entries)
        {
            var e = kv.Value;
            if (e == null || !e.hasTransform) continue;

            var t = e.transform.Value;
            var yaw = QuatToYaw(t.Rotation);

            float hp = 0f, hpMax = 0f;
            if (e.hasAttributeGroup && e.attributeGroup.Group != null)
            {
                hp = e.attributeGroup.Group.GetValue(MobaAttributeIds.HP);
                hpMax = e.attributeGroup.Group.GetValue(MobaAttributeIds.MAX_HP);
            }

            var teamId = e.hasTeam ? (int)e.team.Value : 0;
            var code = e.hasModelId ? e.modelId.Value : 0;
            var kind = e.hasEntityMainType && e.entityMainType.Value == EntityMainType.Projectile ? 2 : 1;
            var ownerNetId = e.hasOwnerLink ? e.ownerLink.OwnerActorId : 0;

            states[kv.Key] = new MobaActorStateImport(
                kv.Key,
                t.Position.X, t.Position.Y, t.Position.Z,
                yaw,
                hp, hpMax,
                teamId,
                kind, code, ownerNetId);
        }

        return states;
    }

    private static float QuatToYaw(in Quat q)
    {
        return MathF.Atan2(2f * (q.X * q.Z + q.W * q.Y), 1f - 2f * (q.X * q.X + q.Y * q.Y));
    }
}
