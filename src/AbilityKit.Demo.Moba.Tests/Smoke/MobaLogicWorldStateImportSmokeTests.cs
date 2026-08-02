using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateImport;
using Xunit;

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
public sealed class MobaLogicWorldStateImportSmokeTests
{
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
            Assert.True(services.TryResolve<MobaActorRegistry>(out var registry) && registry != null,
                "MobaActorRegistry not resolved from world services.");
            Assert.True(services.TryResolve<MobaLogicWorldStateImporter>(out var importer) && importer != null,
                "MobaLogicWorldStateImporter not resolved from world services.");

            var originals = CaptureStates(registry);
            Assert.NotEmpty(originals);

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
