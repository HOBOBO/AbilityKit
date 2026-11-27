using System.Collections;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using AbilityKit.Modifiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BattleEnterGameSnapshotMapper = AbilityKit.Demo.Moba.Share.BattleEnterGameSnapshotMapper;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class LocalDemoHeadlessSkillReleaseTests
    {
        private const float FixedDeltaTime = 1f / 30f;

        /// <summary>
        /// 使用正式本地会话、地图障碍和 HUD 移动输入，验证重叠脱离及圆柱、矩形边缘的连续切向滑动。
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_HudMovementEscapesPublishedPillar()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var maps = world.Services.Resolve<AbilityKit.Demo.Moba.Services.Map.IMobaMapRuntimeService>();
            var pillar = maps.CurrentMap.CollisionObjects.Single(item =>
                item.ShapeType == AbilityKit.Demo.Moba.Config.BattleDemo.MO.MapCollisionShapeType.Capsule);
            Assert.IsTrue(maps.TryGetColliderId(pillar.Id, out _));
            var center = new Vector3(pillar.Position.X, 0f, pillar.Position.Z);
            var actors = world.Services.Resolve<MobaActorRegistry>();
            Assert.IsTrue(actors.TryGet(ctx.LocalActorId, out var actor));
            foreach (var pair in actors.Entries)
                if (pair.Value.hasActorBrain) pair.Value.RemoveActorBrain();

            var directions = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
                new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized,
                new Vector3(1, 0, -1).normalized, new Vector3(-1, 0, -1).normalized };
            ctx.BeginHudMove();
            foreach (var direction in directions)
            {
                Place(center - direction * 3f);
                ctx.SetHudMove(direction.x, direction.z);
                for (var tick = 0; tick < 45; tick++) { FlowTick(flow, 1); yield return null; }
                var contact = Position();
                Assert.Greater(Vector3.Dot(contact - (center - direction * 3f), direction), .5f,
                    $"HUD 输入必须实际推动英雄走向圆柱，dir={direction}, pos={contact}");
                Assert.AreEqual(direction.x, actor.moveInput.Dx, .0001f);
                Assert.AreEqual(direction.z, actor.moveInput.Dz, .0001f);
                Assert.Less(Vector3.Dot(contact - center, direction), -.5f,
                    $"普通移动应被圆柱挡住，dir={direction}, pos={contact}");

                ctx.SetHudMove(-direction.x, -direction.z);
                yield return TickUntil(flow, () => Vector3.Dot(Position() - contact, -direction) > .6f,
                    60, $"撞柱后反向普通移动无法脱离，dir={direction}");

                Place(contact);
                var tangent = new Vector3(-direction.z, 0, direction.x);
                ctx.SetHudMove(tangent.x, tangent.z);
                yield return TickUntil(flow, () => Vector3.Dot(Position() - contact, tangent) > .6f,
                    60, $"贴柱后侧向普通移动无法脱离，dir={direction}");

                foreach (var offset in new[] { 0f, .5f, 1f })
                {
                    Place(center + direction * offset);
                    ctx.SetHudMove(direction.x, direction.z);
                    yield return TickUntil(flow, () => Vector3.Dot(Position() - center, direction) > 2f,
                        90, $"圆柱重叠后普通移动无法脱离，dir={direction}, offset={offset}");
                }
            }
            foreach (var obstacle in maps.CurrentMap.CollisionObjects.Where(item => item.ObjectType == "StaticObstacle"))
            {
                var obstacleCenter = new Vector3(obstacle.Position.X, 0, obstacle.Position.Z);
                var rotation = Quaternion.Euler(0, obstacle.RotationEuler.Y, 0);
                foreach (var side in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                foreach (var sign in new[] { -1f, 1f })
                foreach (var inward in new[] { .4f, 2f, 20f })
                {
                    var outward = rotation * side;
                    Place(obstacleCenter + outward * 5f);
                    ctx.SetHudMove(-outward.x, -outward.z);
                    for (var tick = 0; tick < 65; tick++) { FlowTick(flow, 1); yield return null; }
                    var contact = Position();
                    Assert.Greater(Vector3.Dot(obstacleCenter + outward * 5f - contact, outward), .5f);
                    var tangent = new Vector3(-outward.z, 0, outward.x) * sign;
                    var input = (tangent - outward * inward).normalized;
                    ctx.SetHudMove(input.x, input.z);
                    yield return TickUntil(flow, () => Mathf.Abs(actor.moveInput.Dx - input.x) < .0001f &&
                        Mathf.Abs(actor.moveInput.Dz - input.z) < .0001f, 30, "HUD 斜向移动输入未应用。");
                    var slidingStart = Position();
                    for (var tick = 0; tick < 12; tick++)
                    {
                        var previous = Position();
                        FlowTick(flow, 1);
                        yield return null;
                        var expected = actor.motion.Output.DesiredDelta.Magnitude;
                        var applied = Vector3.Distance(Position(), previous);
                        Assert.IsTrue(expected > .01f && applied > expected * .9f,
                            $"HUD 沿边移动应接近正常速度，obstacle={obstacle.Id}, side={side}, sign={sign}, inward={inward}, tick={tick}, expected={expected}, applied={applied}");
                    }
                    Assert.Greater(Vector3.Dot(Position() - slidingStart, tangent), .6f,
                        $"贴边斜向输入应沿切线持续滑动，obstacle={obstacle.Id}, side={side}, sign={sign}, start={slidingStart}, end={Position()}");
                }
            }
            ctx.SetHudMove(0, 0);
            ctx.EndHudMove();

            Vector3 Position()
            {
                var position = actor.transform.Value.Position;
                return new Vector3(position.X, position.Y, position.Z);
            }

            void Place(Vector3 position)
            {
                ctx.SetHudMove(0, 0);
                FlowTick(flow, 4);
                actor.ReplaceMoveInput(0, 0);
                Assert.IsTrue(TrySetActorPosition(ctx, ctx.LocalActorId, position));
            }
        }

        /// <summary>
        /// 验证左侧按钮的正式回调经过本地会话创建两种阵营的逻辑英雄和场景表现，切主控后相对阵营仍正确。
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_DebugSpawnCreatesVisibleHeroesForCurrentControlTeam()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var players = world.Services.Resolve<MobaPlayerActorMapService>();
            var debug = new BattleLocalDebugController(() => ctx, () => BattleFlowDebugProvider.CurrentHud);
            Assert.IsTrue(debug.TryReplaceHero(3007, out var message), message);
            yield return TickUntil(flow, () => actors.TryGet(ctx.LocalActorId, out var a) &&
                a.hasSkillLoadout && a.skillLoadout.HeroId == 3007, 240, "英雄替换未完成。");

            for (var control = 0; control < 2; control++)
            {
                var sourceId = ctx.LocalActorId;
                Assert.IsTrue(actors.TryGet(sourceId, out var source));
                for (var enemy = 0; enemy < 2; enemy++)
                {
                    var before = actors.Entries.Select(pair => pair.Key).ToArray();
                    var accepted = enemy == 0 ? debug.TrySpawnAlly(out message) : debug.TrySpawnEnemy(out message);
                    Assert.IsTrue(accepted, message);
                    yield return TickUntil(flow, () => actors.Entries.Any(pair => !before.Contains(pair.Key)),
                        120, "按钮命令未创建逻辑实体。");
                    var created = actors.Entries.Single(pair => !before.Contains(pair.Key));
                    Assert.AreEqual(UnitSubType.Hero, created.Value.unitSubType.Value);
                    Assert.AreEqual(enemy == 0, created.Value.team.Value == source.team.Value);
                    if (enemy == 0) Assert.AreEqual(source.skillLoadout.HeroId, created.Value.skillLoadout.HeroId);
                    Assert.Greater(created.Value.GetMobaAttrs().Hp, 0);
                    Assert.IsTrue(created.Value.hasResourceContainer);
                    Assert.IsTrue(created.Value.hasCollisionId);
                    Assert.IsFalse(created.Value.hasActorBrain);
                    Assert.AreEqual(sourceId, ctx.LocalActorId);
                    Assert.IsTrue(players.TryGetActorId(new PlayerId(ctx.ResolveLocalControlPlayerId()), out var mapped));
                    Assert.AreEqual(sourceId, mapped);
                    yield return TickUntil(flow, () => ctx.EntityQuery.TryGetTransform(new BattleNetId(created.Key), out var t) && t != null &&
                        Object.FindObjectsOfType<MonoViewHandle>().Any(view => view.ActorId == created.Key &&
                            view.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0),
                        240, "逻辑出生未创建场景表现。");
                    Assert.IsTrue(world.Services.Resolve<MobaActorIndicatorSnapshotService>().TryGetActiveBaseline(out var baseline));
                    var indicators = AbilityKit.Protocol.Moba.StateSync.MobaActorIndicatorSnapshotCodec.Deserialize(baseline.Payload);
                    Assert.IsTrue(indicators.Any(row => row.ActorId == created.Key && row.IndicatorId == 1 && row.Current > 0));
                    Assert.IsTrue(indicators.Any(row => row.ActorId == created.Key && row.IndicatorId == 2 && row.Max > 0));
                }
                Assert.IsTrue(debug.TrySwitchControl(out message), message);
            }

            ctx.SetGameplayInputBlocked(BattleGameplayInputBlockReason.PauseRecovery, true);
            Assert.IsFalse(debug.TrySpawnAlly(out message));
            StringAssert.Contains("提交失败", message);
        }

        /// <summary>
        /// 验证本地等级调试仅作用于当前主控英雄，且不改变技能 Rank。
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_DebugHeroLevelFollowsCurrentControl()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var debug = new BattleLocalDebugController(() => ctx, () => BattleFlowDebugProvider.CurrentHud);

            Assert.IsTrue(actors.TryGet(ctx.LocalActorId, out var firstHero));
            var firstSkill = firstHero.skillLoadout.ActiveSkills.First(skill => skill != null);
            var firstSkillRank = firstSkill.Level;
            var firstLevel = firstHero.championLevel.Value;
            Assert.AreEqual(firstLevel, debug.CaptureSnapshot().CurrentHeroLevel);
            Assert.IsTrue(debug.TryIncreaseHeroLevel(out var message), message);
            Assert.IsTrue(debug.TryIncreaseHeroLevel(out message), message);
            Assert.AreEqual(firstLevel + 2, firstHero.championLevel.Value);
            Assert.AreEqual(firstLevel + 2, debug.CaptureSnapshot().CurrentHeroLevel);

            Assert.IsTrue(debug.TrySwitchControl(out message), message);
            Assert.IsTrue(actors.TryGet(ctx.LocalActorId, out var secondHero));
            Assert.AreNotSame(firstHero, secondHero);
            var secondLevel = secondHero.championLevel.Value;
            Assert.AreEqual(secondLevel, debug.CaptureSnapshot().CurrentHeroLevel);
            Assert.IsTrue(debug.TryIncreaseHeroLevel(out message), message);
            Assert.AreEqual(secondLevel + 1, secondHero.championLevel.Value);
            Assert.IsTrue(debug.TryResetHeroLevel(out message), message);
            Assert.AreEqual(1, secondHero.championLevel.Value);
            Assert.AreEqual(1, debug.CaptureSnapshot().CurrentHeroLevel);
            Assert.AreEqual(firstLevel + 2, firstHero.championLevel.Value);

            Assert.IsTrue(debug.TrySwitchControl(out message), message);
            Assert.AreEqual(firstLevel + 2, debug.CaptureSnapshot().CurrentHeroLevel);
            Assert.IsTrue(debug.TryResetHeroLevel(out message), message);
            Assert.AreEqual(1, firstHero.championLevel.Value);
            Assert.AreEqual(firstSkillRank, firstHero.skillLoadout.ActiveSkills.First(skill => skill != null).Level);
        }

        /// <summary>
        /// 通过实际本地会话的调试命令验证七名正式英雄的已发布成长数据。
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_DebugHeroGrowthAppliesPublishedTemplates()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var config = world.Services.Resolve<MobaConfigDatabase>();
            var debug = new BattleLocalDebugController(() => ctx, () => BattleFlowDebugProvider.CurrentHud);
            var heroIds = debug.CaptureSnapshot().HeroOptions.Select(option => option.HeroId).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(3001, 7), heroIds);

            foreach (var heroId in heroIds)
            {
                if (debug.CaptureSnapshot().CurrentHeroId != heroId)
                {
                    Assert.IsTrue(debug.TryReplaceHero(heroId, out var replaceMessage), replaceMessage);
                    yield return TickUntil(flow, () => actors.TryGet(ctx.LocalActorId, out var candidate) &&
                        candidate.hasSkillLoadout && candidate.skillLoadout.HeroId == heroId,
                        240, $"英雄 {heroId} 替换未完成。");
                }

                Assert.IsTrue(config.TryGetCharacter(heroId, out var character));
                Assert.IsTrue(config.TryGetAttributeTemplate(character.AttributeTemplateId, out var template));
                Assert.IsTrue(actors.TryGet(ctx.LocalActorId, out var actor));
                Assert.AreEqual(1, actor.championLevel.Value);
                var group = actor.attributeGroup.Group;
                var resources = actor.resourceContainer.Value;
                var attrs = actor.GetMobaAttrs();
                var skill = actor.skillLoadout.ActiveSkills.First(item => item != null);
                var skillRank = skill.Level;
                skill.CooldownEndTimeMs = 123456;
                attrs.Hp = attrs.MaxHp * 0.75f;
                attrs.Mana = attrs.MaxMana * 0.5f;
                group.AddModifier(MobaAttributeIds.PHYSICS_ATTACK, ModifierOp.Add, 7, 9001);

                foreach (var expectedLevel in new[] { 1, 9, 18, 1 })
                {
                    var currentLevel = actor.championLevel.Value;
                    if (expectedLevel < currentLevel)
                    {
                        Assert.IsTrue(debug.TryResetHeroLevel(out var message), message);
                    }
                    else
                    {
                        for (var level = currentLevel; level < expectedLevel; level++)
                            Assert.IsTrue(debug.TryIncreaseHeroLevel(out var message), message);
                    }

                    var offset = expectedLevel - 1;
                    var context = $"英雄 {heroId} / {expectedLevel} 级";
                    Assert.AreEqual(expectedLevel, debug.CaptureSnapshot().CurrentHeroLevel, context);
                    Assert.AreEqual(template.MaxHp + template.MaxHpGrowth * offset, attrs.MaxHp, 0.001f, context);
                    Assert.AreEqual(template.MaxMana + template.MaxManaGrowth * offset, attrs.MaxMana, 0.001f, context);
                    Assert.AreEqual(template.PhysicsAttack + template.PhysicsAttackGrowth * offset + 7, attrs.PhysicsAttack, 0.001f, context);
                    Assert.AreEqual(template.PhysicsDefense + template.PhysicsDefenseGrowth * offset, attrs.PhysicsDefense, 0.001f, context);
                    Assert.AreEqual(template.MagicDefense + template.MagicDefenseGrowth * offset, attrs.MagicDefense, 0.001f, context);
                    Assert.AreEqual(template.ManaRegenPerSecond + template.ManaRegenPerSecondGrowth * offset,
                        group.GetValue(MobaAttributeIds.MANA_REGEN_PER_SECOND), 0.001f, context);
                    Assert.AreEqual(attrs.MaxHp * 0.75f, attrs.Hp, 0.001f, context);
                    Assert.AreEqual(attrs.MaxMana * 0.5f, attrs.Mana, 0.001f, context);
                    Assert.AreSame(group, actor.attributeGroup.Group, context);
                    Assert.AreSame(resources, actor.resourceContainer.Value, context);
                    Assert.AreEqual(skillRank, skill.Level, context);
                    Assert.AreEqual(123456L, skill.CooldownEndTimeMs, context);
                }

                attrs.Hp = 0f;
                Assert.IsTrue(debug.TryIncreaseHeroLevel(out var zeroHpMessage), zeroHpMessage);
                Assert.AreEqual(0f, attrs.Hp, "调试升级不得复活英雄。");
                Assert.IsTrue(debug.TryResetHeroLevel(out zeroHpMessage), zeroHpMessage);
                Assert.AreEqual(0f, attrs.Hp, "重置等级不得复活英雄。");
                attrs.Hp = attrs.MaxHp;
            }
        }

        /// <summary>
        /// 通过面板实际启用敌方大脑；验证无对敌射程的就绪技能不会阻止普攻或造成贴身追击。
        /// </summary>
        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_EnemyAiStopsAtBasicAttackRange()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var players = world.Services.Resolve<MobaPlayerActorMapService>();
            var config = world.Services.Resolve<MobaConfigDatabase>();
            var brains = world.Services.Resolve<MobaBrainService>();
            Assert.IsTrue(actors.TryGet(ctx.LocalActorId, out var player));
            Assert.IsTrue(players.TryGetActorId(new PlayerId("p2"), out var enemyId));
            Assert.IsTrue(actors.TryGet(enemyId, out var enemy));
            Assert.AreEqual(3005, enemy.skillLoadout.HeroId);
            Assert.IsFalse(enemy.hasActorBrain);

            ActiveSkillRuntime basicAttack = null;
            var attackRange = 0f;
            var hasZeroRangeSkill = false;
            foreach (var runtime in enemy.skillLoadout.ActiveSkills.Where(runtime => runtime != null))
            {
                Assert.IsTrue(config.TryGetSkill(runtime.SkillId, out var skill));
                if (skill.Id == 30050101)
                {
                    basicAttack = runtime;
                    attackRange = skill.Range;
                }
                else if (skill.Range > 0f)
                {
                    runtime.CooldownEndTimeMs = long.MaxValue;
                }
                else
                {
                    hasZeroRangeSkill = true;
                }
            }
            Assert.IsNotNull(basicAttack);
            Assert.IsTrue(hasZeroRangeSkill);
            Assert.Greater(Distance(), attackRange);

            var debug = new BattleLocalDebugController(() => ctx, () => BattleFlowDebugProvider.CurrentHud);
            Assert.IsTrue(debug.TryToggleEnemyAi(out var message), message);
            Assert.IsTrue(brains.TryGetBehavior(enemy.actorBrain.BehaviorInstanceId, out var behavior));
            FlowTick(flow, 1);
            Assert.AreEqual(attackRange, MobaBrainIntentReader.Read(behavior).MoveStopRange, 0.001f);
            var hpBefore = player.GetMobaAttrs().Hp;
            yield return TickUntil(flow, () => player.GetMobaAttrs().Hp < hpBefore, 300,
                "敌方 AI 未追到普攻范围内并命中主控英雄。");

            Assert.That(Distance(), Is.InRange(attackRange - 0.5f, attackRange + 0.01f));
            Assert.IsTrue(debug.TryToggleEnemyAi(out message), message);
            Assert.IsFalse(enemy.hasActorBrain);

            float Distance()
            {
                var delta = player.transform.Value.Position - enemy.transform.Value.Position;
                return Mathf.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (GameEntry.IsInitialized && GameEntry.Instance != null)
            {
                Object.DestroyImmediate(GameEntry.Instance.gameObject);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DemoGameplayTestLauncher.Clear();
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_TrainingCooldownRangesAndFixedDummy()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                18000, "本地战斗未进入 InMatch。");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.TryGetRuntimeWorld(out var world));
            var registry = world.Services.Resolve<MobaActorRegistry>();
            var debug = new BattleLocalDebugController(() => ctx, () => BattleFlowDebugProvider.CurrentHud);
            const int veigarHeroId = 3004;
            Assert.IsTrue(registry.TryGet(ctx.LocalActorId, out var actor));
            if (actor.skillLoadout.HeroId != veigarHeroId)
            {
                Assert.IsTrue(debug.TryReplaceHero(veigarHeroId, out var replaceMessage), replaceMessage);
                yield return TickUntil(flow, () => registry.TryGet(ctx.LocalActorId, out var replacement) &&
                    replacement.hasSkillLoadout && replacement.skillLoadout.HeroId == veigarHeroId,
                    240, "维迦替换未完成。");
                Assert.IsTrue(registry.TryGet(ctx.LocalActorId, out actor));
            }
            var before = registry.Entries.Select(pair => pair.Key).ToArray();
            Assert.IsTrue(debug.TrySpawnDummy(3f, 0f, out var message), message);
            var dummy = registry.Entries.Single(pair => !before.Contains(pair.Key));
            Assert.IsFalse(dummy.Value.hasActorBrain);
            Assert.AreNotEqual(actor.team.Value, dummy.Value.team.Value);
            var dummyPosition = dummy.Value.transform.Value.Position;
            Assert.That(dummyPosition.X - actor.transform.Value.Position.X, Is.EqualTo(3f).Within(0.001f));
            Assert.IsTrue(debug.TryReadRanges(1, dummy.Key, out _, out var attackRange,
                out var skillRange, out var selected, out var distance));
            Assert.That(attackRange, Is.Positive);
            Assert.That(skillRange, Is.Positive);
            Assert.That(selected, Is.EqualTo(dummy.Key));
            Assert.That(distance, Is.EqualTo(3f).Within(0.001f));
            yield return TickUntil(flow, () => ctx.EntityQuery.TryGetTransform(new BattleNetId(dummy.Key), out var view) && view != null,
                240, "固定靶未创建场景表现。");
            FlowTick(flow, 12);
            Assert.AreEqual(dummyPosition, dummy.Value.transform.Value.Position);
            var skill = actor.skillLoadout.ActiveSkills[0];
            var heroLevel = actor.championLevel.Value;
            var otherRank = actor.skillLoadout.ActiveSkills[1].Level;
            Assert.IsTrue(debug.TryReadSkillRank(1, out var initialRank, out var maxRank));
            Assert.That(maxRank, Is.GreaterThan(initialRank));
            skill.CooldownDurationMs = 10000;
            skill.CooldownEndTimeMs = 100000;
            Assert.IsFalse(debug.TrySetSkillRank(1, maxRank + 1, out _));
            Assert.That(skill.Level, Is.EqualTo(initialRank));
            Assert.IsTrue(debug.TrySetSkillRank(1, maxRank, out message), message);
            Assert.That(skill.Level, Is.EqualTo(maxRank));
            Assert.That(skill.CooldownEndTimeMs, Is.EqualTo(100000L));
            Assert.That(actor.championLevel.Value, Is.EqualTo(heroLevel));
            Assert.That(actor.skillLoadout.ActiveSkills[1].Level, Is.EqualTo(otherRank));
            using var readout = new BattleTrainingReadout();
            readout.Bind(ctx);
            readout.SetTarget(dummy.Key);
            var mana = actor.resourceContainer.Value.Map[ResourceType.Mana];
            var manaBefore = mana.Current;
            Assert.IsTrue(debug.TrySetTrainingOptions(true, false, out message), message);
            Assert.That(skill.CooldownEndTimeMs, Is.Zero);
            Assert.That(mana.Current, Is.EqualTo(manaBefore));
            var skills = world.Services.Resolve<SkillCastCoordinator>();
            var direction = new Vec3(1f, 0f, 0f);
            var hpBefore = dummy.Value.GetMobaAttrs().Hp;
            var cast = skills.TryCastBySlot(ctx.LocalActorId, 1, in dummyPosition, in direction, dummy.Key);
            Assert.IsTrue(cast.Success, cast.FailReason);
            var runtimes = world.Services.Resolve<MobaSkillCastRuntimeService>();
            Assert.IsTrue(runtimes.TryGet(cast.RuntimeHandle, out var castRuntime));
            Assert.That(castRuntime.SkillLevel, Is.EqualTo(maxRank));
            Assert.IsTrue(debug.TrySetSkillRank(1, 1, out message), message);
            Assert.That(castRuntime.SkillLevel, Is.EqualTo(maxRank));
            yield return TickUntil(flow, () => dummy.Value.GetMobaAttrs().Hp < hpBefore,
                120, "技能没有对固定靶结算伤害。");
            FlowTick(flow, 45);
            Assert.That(readout.LastHit, Is.Positive);
            Assert.That(readout.HitCount, Is.EqualTo(1));
            var firstHit = readout.LastHit;
            Assert.That(skill.CooldownEndTimeMs, Is.Zero);
            Assert.That(mana.Current, Is.LessThan(manaBefore));
            Assert.IsTrue(debug.TrySetTrainingOptions(false, false, out message), message);
            cast = skills.TryCastBySlot(ctx.LocalActorId, 1, in dummyPosition, in direction, dummy.Key);
            Assert.IsTrue(cast.Success, cast.FailReason);
            yield return TickUntil(flow, () => skill.CooldownEndTimeMs > 0L, 120, "关闭开关后技能未恢复冷却。");
            yield return TickUntil(flow, () => readout.HitCount >= 2, 120, "读数没有累计第二次命中。");
            Assert.That(readout.TotalDamage, Is.EqualTo(firstHit + readout.LastHit).Within(0.001d));
            Assert.That(readout.LastHit, Is.LessThan(firstHit), "技能等级表应改变实际伤害。");
            FlowTick(flow, 20);
            cast = skills.TryCastBySlot(ctx.LocalActorId, 1, in dummyPosition, in direction, dummy.Key);
            Assert.IsFalse(cast.Success, "第二次施法仍处于冷却，应拒绝。");
            readout.RefreshFailure();
            Assert.That(readout.FailureText, Does.Contain("冷却"));
            readout.ResetDamage();
            Assert.That(readout.TotalDamage, Is.Zero);
            Assert.That(readout.LastHit, Is.Zero);
            Assert.That(readout.HitCount, Is.Zero);
            Assert.IsTrue(debug.TryClearDummies(out message), message);
            yield return TickUntil(flow, () => !registry.TryGet(dummy.Key, out _), 120, "固定靶未清除。");
            Assert.IsTrue(registry.TryGet(ctx.LocalActorId, out _));
            Assert.IsTrue(debug.TrySetTrainingOptions(true, true, out message), message);
            Assert.IsTrue(debug.TrySwitchControl(out message), message);
            readout.Bind(ctx);
            Assert.IsNull(readout.FailureText);
            debug.ReadTrainingOptions(out var noCooldown, out var infiniteMana);
            Assert.IsFalse(noCooldown || infiniteMana);
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_SwitchControlKeepsSkillHudOnSelectedPlayer()
        {
            DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry);
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            flow.StartWithPersistentSettingsSync();

            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000, "Battle flow did not reach InMatch.");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));

            var hud = BattleFlowDebugProvider.CurrentHud;
            Assert.IsNotNull(hud);
            var debug = new BattleLocalDebugController(() => ctx, () => hud);
            var loadouts = BattleEnterGameSnapshotMapper.MapLoadouts(ctx.BuildEffectivePlayerLoadouts());
            Assert.That(loadouts, Has.Length.EqualTo(2));
            var first = loadouts[0];
            var second = loadouts[1];
            Assert.AreEqual(first.PlayerId, ctx.ResolveLocalControlPlayerId());
            Assert.AreNotEqual(first.HeroId, second.HeroId);
            var templates = new BattleHudSkillButtonTemplateResolver();
            Assert.IsTrue(templates.TryResolveSkill(first, 1, out _, out _, out var firstSkill));
            Assert.IsTrue(templates.TryResolveSkill(second, 1, out _, out _, out var secondSkill));
            Assert.AreNotEqual(firstSkill.Name, secondSkill.Name);

            var firstActorId = ctx.LocalActorId;
            var button = System.Array.Find(Object.FindObjectsOfType<SkillButtonView>(), view => view.name == "Skill1");
            Assert.IsNotNull(button);
            Assert.AreEqual(firstSkill.Name, button.PresentationName);
            var cooldown = button.transform.Find("Skill1CooldownLayer/Skill1CooldownOverlay");
            Assert.IsNotNull(cooldown);
            Assert.IsFalse(cooldown.gameObject.activeSelf);

            Assert.IsTrue(debug.TrySwitchControl(out var message), message);
            Assert.AreEqual(second.PlayerId, ctx.ResolveLocalControlPlayerId());
            var secondActorId = ctx.LocalActorId;
            Assert.AreNotEqual(firstActorId, secondActorId);
            Assert.IsTrue(TryGetActiveSkillRuntime(ctx, secondActorId, 1, secondSkill.SkillId, out var runtime));
            runtime.CooldownDurationMs = 10000;
            runtime.CooldownEndTimeMs = (long)(ctx.LogicTimeSeconds * 1000) + runtime.CooldownDurationMs;

            FlowTick(flow, 10);
            Assert.AreEqual(secondSkill.Name, button.PresentationName, "Live snapshots must keep the selected hero's skills.");
            Assert.AreEqual(secondSkill.IconId, button.PresentationIconId);
            Assert.IsTrue(cooldown.gameObject.activeSelf, "Cooldown must come from the selected actor.");
            Assert.AreEqual(secondActorId, ctx.LocalActorId);

            Assert.IsTrue(debug.TrySwitchControl(out message), message);
            FlowTick(flow, 10);
            Assert.AreEqual(first.PlayerId, ctx.ResolveLocalControlPlayerId());
            Assert.AreEqual(firstActorId, ctx.LocalActorId);
            Assert.AreEqual(firstSkill.Name, button.PresentationName);
            Assert.AreEqual(firstSkill.IconId, button.PresentationIconId);
            Assert.IsFalse(cooldown.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_ReleasesLianPoSkill1AndMovesLocalActor()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");

            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);

            flow.EnterBattle(new TestBattleBootstrapper());
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 2400, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var start), DescribeContextWaitFailure(ctx));

            ctx.SubmitHudSkillAim(slot: 1, aimDx: 1f, aimDz: 0f);
            FlowTick(flow, 2);

            yield return TickUntil(
                flow,
                () => TryGetLocalActorPosition(ctx, out var current) && PlanarDistance(start, current) > 0.1f,
                120,
                DescribeMovementFailure(ctx, start));

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var end), "Local actor position should still be readable after skill release.");
            var moved = PlanarDistance(start, end);
            Assert.Greater(moved, 0.1f, $"Lian Po skill 1 should move the local actor. start={start}, end={end}, moved={moved:F3}");
            Assert.Greater(end.x - start.x, 0.1f, $"Lian Po skill 1 should follow +X aim direction. start={start}, end={end}");
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_LianPoSkill3AimMovesAndJumpsToSelectedPoint()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");

            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);

            flow.EnterBattle(new TestBattleBootstrapper());
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 2400, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var skill1Start), DescribeContextWaitFailure(ctx));
            var skill1SubmitTime = ctx.LogicTimeSeconds;
            ctx.SubmitHudSkillAim(slot: 1, aimDx: 1f, aimDz: 0f);
            FlowTick(flow, 2);
            yield return TickUntil(
                flow,
                () => TryGetLocalActorPosition(ctx, out var current) && PlanarDistance(skill1Start, current) > 0.1f,
                120,
                DescribeMovementFailure(ctx, skill1Start));
            yield return TickUntil(
                flow,
                () => ctx.LogicTimeSeconds >= skill1SubmitTime + 0.8f,
                60,
                $"Lian Po skill 1 did not finish before skill 3 validation. lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}");

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var skill3Start), DescribeContextWaitFailure(ctx));
            var maxPlanarMove = 0f;
            var maxHeightDelta = 0f;

            ctx.SubmitHudSkillAim(slot: 3, aimDx: 1f, aimDz: 0f);
            FlowTick(flow, 2);

            yield return TickUntil(
                flow,
                () =>
                {
                    if (!TryGetLocalActorPosition(ctx, out var current)) return false;
                    maxPlanarMove = Mathf.Max(maxPlanarMove, PlanarDistance(skill3Start, current));
                    maxHeightDelta = Mathf.Max(maxHeightDelta, current.y - skill3Start.y);
                    return maxPlanarMove > 0.1f && maxHeightDelta > 0.08f;
                },
                180,
                DescribeSkill3MovementFailure(ctx, skill3Start, maxPlanarMove, maxHeightDelta));

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var skill3End), "Local actor position should still be readable after skill 3 release.");
            Assert.Greater(maxPlanarMove, 0.1f, $"Lian Po skill 3 should move the caster toward the selected point. start={skill3Start}, end={skill3End}, maxMove={maxPlanarMove:F3}");
            Assert.Greater(maxHeightDelta, 0.08f, $"Lian Po skill 3 should keep jump as a vertical motion. start={skill3Start}, end={skill3End}, maxHeightDelta={maxHeightDelta:F3}");
            Assert.Greater(skill3End.x - skill3Start.x, 0.1f, $"Lian Po skill 3 should follow +X aim position. start={skill3Start}, end={skill3End}");
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_LianPoSkill3DoesNotPullBackAfterInsertedSkill1Dash()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");

            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);

            flow.EnterBattle(new TestBattleBootstrapper());
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 2400, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var start), DescribeContextWaitFailure(ctx));

            ctx.SubmitHudSkillAim(slot: 3, aimDx: 1f, aimDz: 0f);
            FlowTick(flow, 2);
            yield return TickUntil(
                flow,
                () => TryGetLocalActorPosition(ctx, out var current) && current.x > start.x + 0.1f && current.y > start.y + 0.08f,
                180,
                DescribeSkill3MovementFailure(ctx, start, 0f, 0f));

            yield return TickUntil(
                flow,
                () => TryGetLocalActorPosition(ctx, out var current) && current.y <= start.y + 0.05f && current.x > start.x + 0.1f,
                120,
                $"Lian Po skill 3 first stage did not land before inserted skill 1. start={start}, lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}");

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var beforeSkill1), "Local actor position should be readable before inserted skill 1.");
            ctx.SubmitHudSkillAim(slot: 1, aimDx: 1f, aimDz: 0f);
            FlowTick(flow, 2);

            yield return TickUntil(
                flow,
                () => TryGetLocalActorPosition(ctx, out var current) && current.x > beforeSkill1.x + 0.1f,
                120,
                DescribeMovementFailure(ctx, beforeSkill1));

            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var afterSkill1), "Local actor position should be readable after inserted skill 1.");
            var minXAfterSkill1 = afterSkill1.x;
            var minObservedX = afterSkill1.x;
            var maxObservedHeight = afterSkill1.y;
            for (var i = 0; i < 90; i++)
            {
                FlowTick(flow, 1);
                Assert.IsTrue(TryGetLocalActorPosition(ctx, out var current), "Local actor position should remain readable while skill 3 resumes.");
                minObservedX = Mathf.Min(minObservedX, current.x);
                maxObservedHeight = Mathf.Max(maxObservedHeight, current.y);
                yield return null;
            }

            Assert.GreaterOrEqual(minObservedX, minXAfterSkill1 - 0.15f, $"Lian Po skill 3 later stages should continue from current position instead of pulling back to the old aim point. start={start}, beforeSkill1={beforeSkill1}, afterSkill1={afterSkill1}, minObservedX={minObservedX:F3}");
            Assert.Greater(maxObservedHeight - start.y, 0.08f, $"Lian Po skill 3 later stages should still jump vertically after inserted skill 1. start={start}, maxObservedHeight={maxObservedHeight:F3}");
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_CameraResolvesAndFollowsLocalActorWithoutSkillInput()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");

            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);
            flow.EnterBattle(new TestBattleBootstrapper());
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 2400, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => ctx.Session != null && ctx.EntityQuery != null, 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(TryResolveLocalActorId(ctx, out var actorId), "Local actor mapping and transform should be ready.");

            ctx.LocalActorId = 0;
            yield return TickUntil(
                flow,
                () => ctx.LocalActorId == actorId,
                30,
                "Camera follow should resolve the local actor after battle entities load without requiring skill input.");

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Demo scene must provide a main camera.");
            FlowTick(flow, 2);
            var cameraStart = camera.transform.position;
            Assert.IsTrue(ctx.EntityQuery.TryGetTransform(new BattleNetId(actorId), out var transform) && transform != null, "Local actor transform should be available before movement.");
            Assert.IsTrue(TrySetActorPosition(ctx, actorId, transform.Position + new Vector3(4f, 0f, 0f)), "Local actor should be movable for camera follow validation.");

            yield return TickUntil(
                flow,
                () => camera.transform.position.x > cameraStart.x + 0.1f,
                30,
                $"Camera should continuously follow normal actor movement. start={cameraStart}, current={camera.transform.position}");
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_Player2RemainsAliveWithoutInputForFifteenSeconds()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");
            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);
            if (!entry.TryGet(out BattleContext activeContext) || activeContext?.Session == null)
            {
                flow.EnterBattle(new TestBattleBootstrapper());
            }
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world), "Battle world must be available.");
            Assert.IsTrue(world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors), "Player actor map must be available.");
            Assert.IsTrue(playerActors.TryGetActorId(new PlayerId("p2"), out var player2ActorId), "Player p2 must have a spawned actor.");
            Assert.IsTrue(world.Services.TryResolve<MobaActorLookupService>(out var actors), "Actor lookup must be available.");
            Assert.IsTrue(actors.TryGetActorEntity(player2ActorId, out var player2) && player2 != null, "Player p2 actor must be registered.");

            var startHp = player2.GetMobaAttrs().Hp;
            var observedDeath = false;
            var observedPositionReset = false;
            var minHp = startHp;
            var lastHp = startHp;
            var lastPosition = player2.transform.Value.Position;

            for (var frame = 0; frame < 450; frame++)
            {
                FlowTick(flow, 1);
                if (!actors.TryGetActorEntity(player2ActorId, out player2) || player2 == null || !player2.hasTransform)
                {
                    Assert.Fail($"Player p2 actor disappeared during no-input simulation. frame={frame}, actorId={player2ActorId}");
                }

                var hp = player2.GetMobaAttrs().Hp;
                var position = player2.transform.Value.Position;
                minHp = Mathf.Min(minHp, hp);
                observedDeath |= hp <= 0f;
                observedPositionReset |= DistanceSquared(position, lastPosition) > 0.25f;
                lastHp = hp;
                lastPosition = position;
                yield return null;
            }

            Assert.IsFalse(observedDeath || observedPositionReset,
                $"Player p2 died or respawned without input. actorId={player2ActorId}, startHp={startHp:F3}, minHp={minHp:F3}, endHp={lastHp:F3}, " +
                $"endPosition={lastPosition}, deathObserved={observedDeath}, positionResetObserved={observedPositionReset}, " +
                $"frames={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}");
        }

        [UnityTest]
        public IEnumerator UnifiedGameplayScene_LocalBattle_LianPoSkill2RefreshesSkill1CooldownWhenEnemyInRange()
        {
            var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
            Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

            var entry = Object.FindObjectOfType<GameEntry>();
            Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");

            InvokePrivate(entry, "Awake");
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

            flow.StartWithPersistentSettingsSync();
            FlowTick(flow, 2);

            flow.EnterBattle(new TestBattleBootstrapper());
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 2400, "Battle flow did not reach InMatch.");

            Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
            yield return TickUntil(flow, () => IsLocalActorReady(ctx), 240, DescribeContextWaitFailure(ctx));
            Assert.IsTrue(TryGetLocalActorPosition(ctx, out var casterPosition), DescribeContextWaitFailure(ctx));
            Assert.IsTrue(TryFindEnemyActor(ctx, out var enemyActorId), "Demo battle should contain an enemy actor.");
            Assert.IsTrue(TrySetActorPosition(ctx, enemyActorId, casterPosition + new Vector3(1.5f, 0f, 0f)), "Enemy should be movable into Lian Po skill 2 range.");
            Assert.IsTrue(TryGetActiveSkillRuntime(ctx, ctx.LocalActorId, skillSlot: 1, skillId: 10010101, out var skill1Runtime), "Lian Po skill 1 runtime should be available.");

            skill1Runtime.CooldownDurationMs = 5000;
            skill1Runtime.CooldownEndTimeMs = 5000;
            ctx.SubmitHudSkillClick(slot: 2);
            FlowTick(flow, 2);

            yield return TickUntil(
                flow,
                () => TryFindActiveAreaRange(40010201, out _),
                15,
                "Lian Po skill 2 should display its warning damage area while charging.");
            Assert.IsTrue(TryFindActiveAreaRange(40010201, out var range), "Lian Po skill 2 warning range should remain active during validation.");
            Assert.AreEqual(7f, range.transform.localScale.x, 0.05f, "Lian Po skill 2 warning range diameter should match its configured 3.5 radius.");
            Assert.AreEqual(7f, range.transform.localScale.z, 0.05f, "Lian Po skill 2 warning range diameter should match its configured 3.5 radius.");

            yield return TickUntil(
                flow,
                () => TryGetActiveSkillRuntime(ctx, ctx.LocalActorId, skillSlot: 1, skillId: 10010101, out var runtime) && runtime.CooldownEndTimeMs <= 0L && runtime.CooldownDurationMs <= 0,
                90,
                $"Lian Po skill 2 should refresh skill 1 cooldown when an enemy is in range. lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}, cooldownEnd={skill1Runtime.CooldownEndTimeMs}, cooldownDuration={skill1Runtime.CooldownDurationMs}");
        }

        private static IEnumerator TickUntil(GameFlowDomain flow, System.Func<bool> predicate, int maxTicks, string failureMessage)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                FlowTick(flow, 1);
                if (predicate()) yield break;
                yield return null;
            }

            if (GameEntry.IsInitialized && GameEntry.Instance != null &&
                GameEntry.Instance.TryGet(out BattleLoadingScreenFeature loading))
            {
                var snapshot = loading.CurrentSnapshot;
                failureMessage += $" Loading: completed={snapshot.Completed}, success={snapshot.Success}, progress={snapshot.LoadedCount}/{snapshot.TotalCount}, current={snapshot.CurrentAssetKey}, error={snapshot.ErrorMessage}.";
            }

            Assert.Fail(failureMessage);
        }

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                flow.Tick(FixedDeltaTime);
            }
        }

        private static bool IsLocalActorReady(BattleContext ctx)
        {
            if (ctx == null || ctx.Session == null || ctx.EntityQuery == null) return false;
            if (ctx.LocalActorId <= 0 && TryResolveLocalActorId(ctx, out var actorId))
            {
                ctx.LocalActorId = actorId;
            }

            return ctx.LocalActorId > 0 && TryGetLocalActorPosition(ctx, out _);
        }

        private static bool TryResolveLocalActorId(BattleContext ctx, out int actorId)
        {
            actorId = 0;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            if (!world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActorMap) || playerActorMap == null) return false;

            var playerIdValue = ctx.Plan.World.PlayerId;
            var playerId = new PlayerId(string.IsNullOrEmpty(playerIdValue) ? "p1" : playerIdValue);
            if (!playerActorMap.TryGetActorId(playerId, out actorId) || actorId <= 0) return false;
            return ctx.EntityQuery.TryGetTransform(new BattleNetId(actorId), out var transform) && transform != null;
        }

        private static bool TryGetLocalActorPosition(BattleContext ctx, out Vector3 position)
        {
            position = default;
            if (ctx == null || ctx.EntityQuery == null || ctx.LocalActorId <= 0) return false;
            if (!ctx.EntityQuery.TryGetTransform(new BattleNetId(ctx.LocalActorId), out var transform) || transform == null) return false;

            position = transform.Position;
            return true;
        }

        private static bool TryFindEnemyActor(BattleContext ctx, out int enemyActorId)
        {
            enemyActorId = 0;
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(ctx.LocalActorId, out var local) || local == null) return false;

            var actorIds = new System.Collections.Generic.List<int>(16);
            entities.GetRegisteredActorIds(actorIds);
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (actorId <= 0 || actorId == ctx.LocalActorId) continue;
                if (!entities.TryGetActorEntity(actorId, out var candidate) || candidate == null) continue;
                if (!candidate.hasTransform || !candidate.hasAttributeGroup) continue;
                if (local.hasTeam && candidate.hasTeam && local.team.Value.Equals(candidate.team.Value)) continue;

                enemyActorId = actorId;
                return true;
            }

            return false;
        }

        private static bool TrySetActorPosition(BattleContext ctx, int actorId, Vector3 position)
        {
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasTransform) return false;

            var current = entity.transform.Value;
            var newPosition = ToVec3(position);
            entity.ReplaceTransform(new Transform3(in newPosition, in current.Rotation, in current.Scale));
            return true;
        }

        private static bool TryFindActiveAreaRange(int templateId, out GameObject range)
        {
            range = null;
            var expectedName = $"AreaRange_{templateId}";
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i]?.gameObject;
                if (candidate == null || !candidate.activeInHierarchy || candidate.name != expectedName) continue;
                if (!candidate.scene.IsValid()) continue;

                range = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetActiveSkillRuntime(BattleContext ctx, int actorId, int skillSlot, int skillId, out ActiveSkillRuntime runtime)
        {
            runtime = null;
            if (!TryGetActorLookup(ctx, out var actors)) return false;
            return MobaSkillRuntimeAccess.TryGetActiveSkill(actors, actorId, skillSlot, skillId, out runtime);
        }

        private static bool TryGetActorLookup(BattleContext ctx, out MobaActorLookupService actors)
        {
            actors = null;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            return world.Services.TryResolve(out actors) && actors != null;
        }

        private static bool TryGetEntityManager(BattleContext ctx, out MobaEntityManager entities)
        {
            entities = null;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            return world.Services.TryResolve(out entities) && entities != null;
        }

        private static Vec3 ToVec3(Vector3 value)
        {
            return new Vec3(value.x, value.y, value.z);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var dx = b.x - a.x;
            var dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceSquared(Vec3 a, Vec3 b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;
            var z = a.Z - b.Z;
            return x * x + y * y + z * z;
        }

        private static string DescribeContextWaitFailure(BattleContext ctx)
        {
            if (ctx == null) return "BattleContext was not available.";
            return $"Local actor was not ready. hasSession={ctx.Session != null}, localActorId={ctx.LocalActorId}, planPlayerId={ctx.Plan.World.PlayerId}, hasEntityQuery={ctx.EntityQuery != null}, lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}";
        }

        private static string DescribeMovementFailure(BattleContext ctx, Vector3 start)
        {
            if (!TryGetLocalActorPosition(ctx, out var current))
            {
                return DescribeContextWaitFailure(ctx);
            }

            var moved = PlanarDistance(start, current);
            return $"Lian Po skill 1 did not move the local actor after aim release. start={start}, current={current}, moved={moved:F3}, localActorId={ctx.LocalActorId}, lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}";
        }

        private static string DescribeSkill3MovementFailure(BattleContext ctx, Vector3 start, float maxPlanarMove, float maxHeightDelta)
        {
            if (!TryGetLocalActorPosition(ctx, out var current))
            {
                return DescribeContextWaitFailure(ctx);
            }

            return $"Lian Po skill 3 did not combine selected-point horizontal movement and vertical jump. start={start}, current={current}, maxMove={maxPlanarMove:F3}, maxHeightDelta={maxHeightDelta:F3}, localActorId={ctx.LocalActorId}, lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}";
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method: {methodName}");
            method.Invoke(target, null);
        }
    }
}
