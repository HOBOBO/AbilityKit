using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Trace;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public readonly struct HeroSkillContract
    {
        public readonly string Name;
        public readonly int HeroId;
        public readonly int AttributeTemplateId;
        public readonly int[] SkillIds;

        public HeroSkillContract(string name, int heroId, int attributeTemplateId, int[] skillIds)
        {
            Name = name;
            HeroId = heroId;
            AttributeTemplateId = attributeTemplateId;
            SkillIds = skillIds;
        }
    }

    public readonly struct HeroSkillSlotContract
    {
        public readonly int Slot;
        public readonly int SkillId;
        public readonly int CastFlowId;
        public readonly int EffectId;

        public HeroSkillSlotContract(int slot, int skillId, int castFlowId, int effectId)
        {
            Slot = slot;
            SkillId = skillId;
            CastFlowId = castFlowId;
            EffectId = effectId;
        }
    }

    public static class HeroSkillHeadlessContract
    {
        public static MobaSkillConfigTestHarness CreateHarness(in HeroSkillContract hero, string worldId)
        {
            return MobaSkillConfigTestHarness.CreateForSinglePlayer(
                hero.SkillIds,
                worldId: worldId,
                heroId: hero.HeroId,
                attributeTemplateId: hero.AttributeTemplateId);
        }

        public static TraceSnapshot<MobaTraceMetadata> CastSlotAndAssertEffect(
            MobaSkillConfigTestHarness harness,
            in HeroSkillSlotContract skill,
            string reason)
        {
            harness.EnterGameAndWarmup(reason: reason);
            harness.AssertSlotSkill(skill.Slot, skill.SkillId);
            harness.AssertSkillUsesCastFlow(skill.SkillId, skill.CastFlowId);
            harness.AssertCastFlowContainsTimelineEffect(skill.CastFlowId, skill.EffectId);

            var actorId = harness.AssertPlayerActorBound();
            var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
            var cast = skills.TryCastBySlot(actorId, skill.Slot, aimPos: default, aimDir: new Vec3(1f, 0f, 0f), targetActorId: 0);
            Assert.IsTrue(
                cast.Success,
                $"{reason} should cast skill {skill.SkillId} from slot {skill.Slot}. " +
                $"failReason={cast.FailReason}, failure.source={cast.Failure.Source}, " +
                $"failure.stage={cast.Failure.Stage}, failure.code={cast.Failure.Code}, " +
                $"failure.message={cast.Failure.Message}");

            harness.AssertSkillCastTrace(skill.SkillId);
            return harness.TickUntilTraceNode(
                MobaTraceKind.EffectExecution,
                skill.EffectId,
                maxTicks: harness.CalculateWaitTicksForSkillEffect(skill.SkillId, skill.EffectId, safetyFrames: 5) + 30,
                message: $"EffectExecution trace missing for effect {skill.EffectId} after direct casting skill {skill.SkillId} slot {skill.Slot}.");
        }

        public static int SpawnEnemyHero(
            MobaSkillConfigTestHarness harness,
            string alias = "enemy",
            int actorId = 9001,
            int heroId = 1002,
            int attributeTemplateId = 1002,
            float x = 4f,
            float z = 0f)
        {
            return harness.SpawnScenarioActor(
                alias,
                actorId,
                kind: "Hero",
                teamId: 2,
                heroId: heroId,
                attributeTemplateId: attributeTemplateId,
                level: 1,
                unitSubType: (int)UnitSubType.Hero,
                mainType: (int)EntityMainType.Unit,
                ownerPlayerId: "enemy_player",
                ownerActorId: 0,
                sourceKind: "Hero",
                sourceId: heroId,
                position: new MobaAcceptanceVector3Expectation { x = x, y = 0f, z = z });
        }

        public static DamageResult ExecuteDamage(
            MobaSkillConfigTestHarness harness,
            int attackerActorId,
            int targetActorId,
            float baseDamage,
            DamageReasonKind reasonKind,
            int reasonParam = 0,
            DamageType damageType = DamageType.Physical)
        {
            var damage = harness.World.Services.Resolve<DamagePipelineService>();
            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = damageType,
                ReasonKind = reasonKind,
                ReasonParam = reasonParam
            };
            attack.BaseDamage.BaseValue = baseDamage;
            return damage.Execute(attack);
        }

        public static DamageResult ExecuteBasicAttackDamage(
            MobaSkillConfigTestHarness harness,
            int attackerActorId,
            int targetActorId,
            float baseDamage = 1f)
        {
            return ExecuteDamage(
                harness,
                attackerActorId,
                targetActorId,
                baseDamage,
                DamageReasonKind.BasicAttack,
                reasonParam: 0,
                damageType: DamageType.Physical);
        }

        public static void AssertTriggerActions(MobaSkillConfigTestHarness harness, int triggerId, params int[] actionIds)
        {
            harness.AssertTriggerPlanContainsActions(triggerId, actionIds);
        }

        public static void AssertFreshBuff(MobaSkillConfigTestHarness harness, int actorId, int buffId, float minRemainingSeconds, string message)
        {
            for (var i = 0; i < 30; i++)
            {
                if (harness.TryGetActorBuffRemainingSeconds(actorId, buffId, out var remaining))
                {
                    Assert.GreaterOrEqual(remaining, minRemainingSeconds, message + $" remaining={remaining:F3}");
                    return;
                }

                harness.Tick(1);
            }

            Assert.Fail(message);
        }
    }
}
