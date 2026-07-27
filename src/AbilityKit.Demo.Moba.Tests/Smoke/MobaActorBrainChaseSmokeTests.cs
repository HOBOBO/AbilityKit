using System;
using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Brain → MoveInput → Motion 链路（P0/P1）冒烟验证。
///
/// 在真实 Console 战斗世界中：
/// 1. 通过 MobaSummonService 召唤一只带 BrainId=1（chase 驱动）的小兵
/// 2. MobaBrainSystem 创建 BehaviorRuntime（chase 决策）
/// 3. 追击决策产出 Movement → MobaBrainOutputApplySystem 写 MoveInput
/// 4. Motion 消费 MoveInput 推动小兵向最近敌人移动
///
/// 断言：若干帧后小兵位置明显变化，且与最近敌人的距离缩小。
/// </summary>
public sealed class MobaActorBrainChaseSmokeTests
{
    [Fact]
    public void Summon_with_chase_brain_moves_toward_nearest_enemy()
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
                "MobaActorRegistry not resolved.");
            Assert.True(services.TryResolve<MobaSummonService>(out var summons) && summons != null,
                "MobaSummonService not resolved.");
            Assert.True(services.TryResolve<IMobaActorBrainCatalog>(out var catalog) && catalog != null,
                "IMobaActorBrainCatalog not resolved.");

            // 目录中应有 BrainId=1 的 chase 定义
            Assert.True(catalog.TryGet(1, out var definition));
            Assert.Equal(MobaBrainDriverKind.Code, definition.DriverKind);
            Assert.Equal("chase", definition.DecisionName);

            // 找两个不同队伍的 actor 作为 caster 和参照敌人
            int casterId = 0;
            int casterTeam = 0;
            int enemyId = 0;
            foreach (var kv in registry.Entries)
            {
                var e = kv.Value;
                if (e == null || !e.hasTeam || !e.hasTransform) continue;
                var team = (int)e.team.Value;
                if (team == 0) continue;

                if (casterId == 0)
                {
                    casterId = kv.Key;
                    casterTeam = team;
                    continue;
                }

                if (team != casterTeam)
                {
                    enemyId = kv.Key;
                    break;
                }
            }

            Assert.True(casterId > 0, "no caster hero found");
            Assert.True(enemyId > 0, "no enemy hero found");

            Assert.True(registry.TryGet(casterId, out var caster) && caster != null);
            Assert.True(registry.TryGet(enemyId, out var enemy) && enemy != null);

            // 在 caster 位置附近、远离敌人的一侧召唤（小兵队伍=caster 队伍，chase 应朝敌人移动）
            var casterPos = caster.transform.Value.Position;
            var enemyPos = enemy.transform.Value.Position;
            var away = casterPos - enemyPos;
            var awayLen = away.Magnitude;
            var spawnDir = awayLen > 0.001f ? new Vec3(away.X / awayLen, away.Y / awayLen, away.Z / awayLen) : Vec3.Forward;
            var spawnPos = new Vec3(
                casterPos.X + spawnDir.X * 5f,
                casterPos.Y,
                casterPos.Z + spawnDir.Z * 5f);

            Assert.True(summons.TrySummon(casterId, summonId: 1, in spawnPos),
                "TrySummon failed");

            // 找到刚召唤的 actor（带 ActorBrain 且不是 caster/enemy）
            int summonActorId = 0;
            foreach (var kv in registry.Entries)
            {
                if (kv.Key == casterId || kv.Key == enemyId) continue;
                var e = kv.Value;
                if (e != null && e.hasActorBrain && e.actorBrain.BrainId == 1)
                {
                    summonActorId = kv.Key;
                    break;
                }
            }

            Assert.True(summonActorId > 0, "summon with ActorBrain(BrainId=1) not found");
            Assert.True(registry.TryGet(summonActorId, out var summon) && summon != null);

            var spawnDistanceToEnemy = (summon.transform.Value.Position - enemyPos).Magnitude;

            // 推进战斗：BrainTick → 输出应用 → Motion
            for (var i = 0; i < 90; i++)
            {
                bootstrapper.Tick();
            }

            // 大脑行为实例应已创建
            Assert.True(summon.hasActorBrain);
            Assert.True(summon.actorBrain.BehaviorInstanceId > 0,
                "BehaviorRuntime was not created for summon brain");

            var finalPos = summon.transform.Value.Position;
            var finalDistanceToEnemy = (finalPos - enemyPos).Magnitude;
            var moved = (finalPos - spawnPos).Magnitude;

            Assert.True(moved > 0.5f,
                $"summon did not move. moved={moved:F3} spawnDist={spawnDistanceToEnemy:F3} finalDist={finalDistanceToEnemy:F3}");
            Assert.True(finalDistanceToEnemy < spawnDistanceToEnemy,
                $"summon did not approach enemy. spawnDist={spawnDistanceToEnemy:F3} finalDist={finalDistanceToEnemy:F3}");
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }
}
