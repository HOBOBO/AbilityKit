using System;
using System.Linq;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 测试召唤技能 → BT 行为树召唤物（P2）冒烟验证。
///
/// 链路：
/// 1. 英雄 1001 施放测试技能 9900001（skill_flow Timeline → EffectId 9900001
///    → trigger_9900001 spawn_summon summon_id=2）
/// 2. 召唤物"树灵守卫"（summons id 2）生成：
///    - ModelId/属性模板 初始化
///    - InheritAttributeConfigId=1 继承施法者属性占比（物攻×0.8、最大生命×0.6、移速×1.0）
///    - BrainId=3 is resolved by the battle-template brain catalog to summon_warden_bt.
/// 3. BT 的 chase 节点驱动召唤物向最近敌人移动
/// </summary>
public sealed class MobaSummonBTreeSkillSmokeTests
{
    private const int TestSkillId = 9900001;
    private const int WardenSummonId = 2;

    [Fact]
    public void Cast_summon_skill_produces_btree_driven_warden_with_inherited_attributes()
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
            Assert.True(services.TryResolve<MobaActorRegistry>(out var registry) && registry != null);
            Assert.True(services.TryResolve<SkillCastCoordinator>(out var castCoordinator) && castCoordinator != null);
            Assert.True(services.TryResolve<MobaBrainService>(out var brains) && brains != null);

            // 找英雄 1001 的 actor 作为施法者
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

            var casterPhysAttack = caster.attributeGroup.Group.GetValue(MobaAttributeIds.PHYSICS_ATTACK);
            var casterMaxHp = caster.attributeGroup.Group.GetValue(MobaAttributeIds.MAX_HP);
            var enemyPos = enemy.transform.Value.Position;

            // 施放测试技能（任意 slot——释放校验走 release/commit RulePlan）
            var castOk = castCoordinator.CastSkill(casterId, TestSkillId, slot: 4, out var failReason);
            Assert.True(castOk, $"CastSkill failed: {failReason}");

            // 推进到 Timeline 100ms 的召唤事件（30fps，500ms 窗口给足）
            for (var i = 0; i < 30; i++)
            {
                bootstrapper.Tick();
            }

            // 找到树灵守卫（BrainId=3 的召唤物）
            int wardenId = 0;
            foreach (var kv in registry.Entries)
            {
                if (kv.Key == casterId || kv.Key == enemyId) continue;
                var e = kv.Value;
                if (e != null && e.hasActorBrain && e.actorBrain.BrainId == 3)
                {
                    wardenId = kv.Key;
                    break;
                }
            }

            Assert.True(wardenId > 0, "warden summon not found after skill cast");
            Assert.True(registry.TryGet(wardenId, out var warden) && warden != null);

            // 大脑行为实例已创建，并且确实采用配置的 BT 驱动而非 BrainId 的 Code 回退。
            Assert.True(warden.actorBrain.BehaviorInstanceId > 0,
                "warden brain behavior was not created");
            Assert.True(brains.TryGetBehavior(warden.actorBrain.BehaviorInstanceId, out var behavior) && behavior != null,
                "warden brain behavior could not be resolved");
            Assert.Equal("MobaBTree", behavior.Decision.DecisionType);

            // 继承属性：物攻 = 施法者 × 0.8，最大生命 = 施法者 × 0.6
            var wardenGroup = warden.attributeGroup.Group;
            Assert.Equal(casterPhysAttack * 0.8f, wardenGroup.GetValue(MobaAttributeIds.PHYSICS_ATTACK), 2);
            Assert.Equal(casterMaxHp * 0.6f, wardenGroup.GetValue(MobaAttributeIds.MAX_HP), 2);

            // BT chase 驱动：推进 90 帧，召唤物应向最近敌人靠近
            var spawnPos = warden.transform.Value.Position;
            var spawnDist = (spawnPos - enemyPos).Magnitude;

            for (var i = 0; i < 90; i++)
            {
                bootstrapper.Tick();
            }

            var finalPos = warden.transform.Value.Position;
            var finalDist = (finalPos - enemyPos).Magnitude;
            var moved = (finalPos - spawnPos).Magnitude;

            Assert.True(moved > 0.5f,
                $"warden did not move under BT chase. moved={moved:F3} spawnDist={spawnDist:F3} finalDist={finalDist:F3}");
            Assert.True(finalDist < spawnDist,
                $"warden did not approach enemy. spawnDist={spawnDist:F3} finalDist={finalDist:F3}");
        }
        finally
        {
            bootstrapper.Stop();
            bootstrapper.Dispose();
        }
    }
}
