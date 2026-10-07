using System;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleTestScriptRunnerEditModeTests
    {
        [Test]
        public void SharedRunner_CanDriveUnityHeadlessViewRuntimeDriver()
        {
            var script = BattleTestScenarioLibrary.CreateViewPresentationRisk(cycles: 1);
            var ctx = BattleContext.Rent();

            try
            {
                ctx.Plan = BattleStartPlanBuilder
                    .ForWorld("world", "type", "client", "player", tickRate: 30, inputDelayFrames: 0)
                    .Build();

                var driver = new ViewRuntimeBattleTestDriver(ctx);

                var result = new BattleTestScriptRunner().Run(script, driver);

                Assert.IsTrue(result.Completed, result.ErrorMessage);
                Assert.AreEqual(script.Steps.Count, result.StepCount);
                Assert.AreEqual(script.TotalDurationTicks, result.TickCount);
                Assert.AreEqual(script.TotalDurationTicks, driver.TickCount);
                Assert.AreEqual(script.TotalDurationTicks, ctx.LastFrame);
                Assert.GreaterOrEqual(driver.SkillApplyCount, 1);
                Assert.Greater(driver.SampleTime, 0d);
            }
            finally
            {
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void ViewRuntimeBattleTestDriver_TracksStepKindsAndStoresLastResult()
        {
            var script = new BattleTestScriptBuilder()
                .Move(1f, 0f, durationTicks: 2)
                .Skill(1)
                .Wait(3)
                .Idle(2)
                .Build("view-runtime-driver-coverage");
            var ctx = BattleContext.Rent();

            try
            {
                var driver = new ViewRuntimeBattleTestDriver(ctx);

                var result = new BattleTestScriptRunner().Run(script, driver);

                Assert.IsTrue(result.Completed, result.ErrorMessage);
                Assert.AreSame(result, driver.LastResult);
                Assert.AreEqual(script.TotalDurationTicks, driver.TickCount);
                Assert.AreEqual(2, driver.MoveApplyCount);
                Assert.AreEqual(1, driver.SkillApplyCount);
                Assert.AreEqual(3, driver.WaitApplyCount);
                Assert.AreEqual(2, driver.IdleApplyCount);
                Assert.AreEqual(script.TotalDurationTicks, ctx.LastFrame);
                Assert.AreEqual(ctx.LastFrame / (double)driver.TickRate, ctx.LogicTimeSeconds);
            }
            finally
            {
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void ViewRuntimeBattleTestHarness_CanRunScenarioWithOwnedContext()
        {
            using var harness = new ViewRuntimeBattleTestHarness();
            harness.ConfigureDefaultPlan();

            var script = BattleTestScenarioLibrary.CreateViewPresentationRisk(cycles: 1);
            var result = harness.Run(script);

            Assert.IsTrue(result.Completed, result.ErrorMessage);
            Assert.AreSame(result, harness.LastResult);
            Assert.IsNotNull(harness.LastDriver);
            Assert.AreEqual(script.TotalDurationTicks, harness.LastDriver.TickCount);
            Assert.AreEqual(script.TotalDurationTicks, harness.Context.LastFrame);
            Assert.GreaterOrEqual(harness.LastDriver.SkillApplyCount, 1);
        }

        [Test]
        public void ViewRuntimeBattleTestHarness_CanRunFullFlowAndAutoReleaseFirstSkill()
        {
            using var harness = new ViewRuntimeBattleTestHarness();
            harness.ConfigureDefaultPlan();

            var script = new BattleTestScriptBuilder()
                .Move(1f, 0f, durationTicks: 5)
                .Wait(2)
                .Skill(1)
                .Wait(10)
                .Idle(3)
                .Build("full-flow-auto-release-first-skill");

            var result = harness.Run(script);

            Assert.IsTrue(result.Completed, result.ErrorMessage);
            Assert.AreSame(result, harness.LastResult);
            Assert.IsNotNull(harness.LastDriver);
            Assert.AreEqual(script.TotalDurationTicks, result.TickCount);
            Assert.AreEqual(script.TotalDurationTicks, harness.LastDriver.TickCount);
            Assert.AreEqual(script.TotalDurationTicks, harness.Context.LastFrame);
            Assert.AreEqual(1, harness.LastDriver.SkillApplyCount);
            Assert.AreEqual(1, harness.LastDriver.LastReleasedSkillSlot);
            CollectionAssert.AreEqual(new[] { 1 }, harness.LastDriver.ReleasedSkillSlots);
            Assert.Greater(harness.LastDriver.SampleTime, 0d);
        }

        [Test]
        public void SkillSlotOne_CastExecutesConfiguredTimelineEffect()
        {
            const int expectedSkillId = 10010101;
            const int expectedCastFlowId = 10010101;
            const int expectedEffectId = 10010101;

            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { expectedSkillId },
                worldId: "skill_effect_assertion_world",
                heroId: 1001,
                attributeTemplateId: 1001);

            harness.AssertSkillUsesCastFlow(expectedSkillId, expectedCastFlowId);
            harness.AssertCastFlowContainsTimelineEffect(expectedCastFlowId, expectedEffectId);
            harness.AssertTriggerPlanContainsActions(
                expectedEffectId,
                (int)TriggeringConstants.AddBuffId.Value,
                (int)TriggeringConstants.DashId.Value,
                (int)TriggeringConstants.DebugLogId.Value);

            harness.EnterGameAndWarmup(reason: "editmode skill effect assertion");
            harness.AssertSlotSkill(slot: 1, expectedSkillId: expectedSkillId);

            var effectTrace = harness.CastSkillSlotAndTickUntilEffect(
                slot: 1,
                skillId: expectedSkillId,
                effectId: expectedEffectId);
            harness.AssertSkillCastTrace(expectedSkillId);
            harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.AddBuffId.Value, TriggeringConstants.Actions.AddBuff);
            harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DashId.Value, TriggeringConstants.Actions.Dash);
            harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DebugLogId.Value, TriggeringConstants.Actions.DebugLog);
        }

        [TestCase(1001, 1001, 10010101, 10010001)]
        [TestCase(1003, 1003, 10030101, 10030001)]
        public void MeleeBasicAttack_LoadoutAndConfigUseDirectDamage(
            int heroId,
            int attributeTemplateId,
            int activeSkillId,
            int basicAttackSkillId)
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { activeSkillId },
                worldId: "melee_basic_attack_" + heroId + "_world",
                heroId: heroId,
                attributeTemplateId: attributeTemplateId);

            harness.AssertSkillUsesCastFlow(basicAttackSkillId, basicAttackSkillId);
            harness.AssertCastFlowContainsTimelineEffect(basicAttackSkillId, basicAttackSkillId);
            harness.AssertTriggerPlanContainsActions(basicAttackSkillId, (int)TriggeringConstants.GiveDamageId.Value);

            harness.EnterGameAndWarmup(reason: "melee basic attack loadout assertion");
            harness.AssertSlotSkill(slot: 2, expectedSkillId: basicAttackSkillId);
        }

        [TestCase(1002, 1002, 10020101, 10020001, 10020011, 31020001, 30020001)]
        [TestCase(1004, 1004, 10040101, 10040011, 10040012, 31040011, 30040011)]
        [TestCase(1005, 1005, 10050101, 10050001, 10050011, 31050001, 30050001)]
        [TestCase(1006, 1006, 10060101, 10060001, 10060011, 31060001, 30060001)]
        public void RangedBasicAttack_LoadoutAndConfigUseProjectileHitDamage(
            int heroId,
            int attributeTemplateId,
            int activeSkillId,
            int basicAttackSkillId,
            int hitTriggerId,
            int launcherId,
            int projectileId)
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { activeSkillId },
                worldId: "ranged_basic_attack_" + heroId + "_world",
                heroId: heroId,
                attributeTemplateId: attributeTemplateId);

            harness.AssertSkillUsesCastFlow(basicAttackSkillId, basicAttackSkillId);
            harness.AssertCastFlowContainsTimelineEffect(basicAttackSkillId, basicAttackSkillId);
            harness.AssertProjectileConfigExists(launcherId, projectileId);
            harness.AssertTriggerPlanContainsActions(basicAttackSkillId, (int)TriggeringConstants.ShootProjectileId.Value);
            harness.AssertTriggerPlanContainsActions(hitTriggerId, (int)TriggeringConstants.GiveDamageId.Value);

            harness.EnterGameAndWarmup(reason: "ranged basic attack loadout assertion");
            harness.AssertSlotSkill(slot: 2, expectedSkillId: basicAttackSkillId);
        }

        [Test]
        public void MoziPassive_UsesGenericCounterTriggerPlanAndFourthHitMeleeAttack()
        {
            const int passiveCounterTriggerId = 10040001;
            const int passiveMeleeTriggerId = 10040002;

            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { 10040101, 10040201, 10040301 },
                worldId: "mozi_passive_trigger_plan_assertion_world");

            harness.AssertTriggerPlanContainsActions(passiveCounterTriggerId, (int)TriggeringConstants.AdvanceGameplayCounterId.Value);
            harness.AssertTriggerPlanContainsActions(
                passiveMeleeTriggerId,
                (int)TriggeringConstants.GiveDamageId.Value,
                (int)TriggeringConstants.PullId.Value,
                (int)TriggeringConstants.AddBuffId.Value,
                (int)TriggeringConstants.DebugLogId.Value);
            AssertPackageResourceExists("ability/triggers/passives/trigger_10040000.json");
        }

        [Test]
        public void ZhaoYunPassive_IsBoundToCharacterAndCompilesTriggerPlans()
        {
            const int heroId = 1003;
            const int passiveSkillId = 10030000;

            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { 10030101, 10030201, 10030301 },
                heroId: heroId,
                attributeTemplateId: 1003,
                worldId: "zhaoyun_passive_trigger_plan_assertion_world");

            Assert.IsTrue(harness.Config.TryGetCharacter(heroId, out var character), $"Character config missing: {heroId}");
            Assert.IsTrue(ContainsId(character.PassiveSkillIds, passiveSkillId), $"ZhaoYun character must bind passive skill {passiveSkillId}.");
            Assert.IsTrue(harness.Config.TryGetPassiveSkill(passiveSkillId, out var passive), $"Passive skill config missing: {passiveSkillId}");
            Assert.IsTrue(ContainsId(passive.TriggerIds, 10030000), "ZhaoYun passive must include low-health trigger 10030000.");

            for (int i = 0; i < passive.TriggerIds.Count; i++)
            {
                harness.AssertTriggerPlanContainsActions(passive.TriggerIds[i]);
            }
        }

        [Test]
        public void MobaTriggerResources_AreOwnedByViewRuntimePackage()
        {
            Assert.IsFalse(Directory.Exists(Path.Combine("Assets", "Resources")), "MOBA resources must not be duplicated under Assets/Resources.");
            AssertPackageResourceExists("moba/characters.json");
            AssertPackageResourceExists("moba/battle_start.json");
            AssertPackageResourceExists("ability/triggers/passives/trigger_10030000.json");
            AssertPackageResourceExists("ability/triggers/passives/trigger_10040000.json");
            AssertPackageResourceExists("ability/triggers/skills/trigger_10030101.json");
            AssertPackageResourceExists("ability/triggers/skills/trigger_10040101.json");
        }

        [Test]
        public void SharedRunner_PropagatesRiskTagsForUnityPresentationCoverage()
        {
            var script = new ViewPresentationRiskScenario { Cycles = 1 }.CreateScript();
            var driver = new RecordingUnityHeadlessDriver();

            var result = new BattleTestScriptRunner().Run(script, driver);

            Assert.IsTrue(result.Completed, result.ErrorMessage);
            CollectionAssert.Contains(script.RiskTags, BattleTestScenarioLibrary.EntityRiskTag);
            CollectionAssert.Contains(script.RiskTags, BattleTestScenarioLibrary.FloatingTextRiskTag);
            CollectionAssert.Contains(script.RiskTags, BattleTestScenarioLibrary.ProjectileRiskTag);
            CollectionAssert.Contains(script.RiskTags, BattleTestScenarioLibrary.VfxRiskTag);
            CollectionAssert.Contains(script.RiskTags, BattleTestScenarioLibrary.SnapshotEventRiskTag);
            Assert.AreEqual(script.TotalDurationTicks, driver.AppliedTickCount);
        }

        private static bool ContainsId(System.Collections.Generic.IReadOnlyList<int> ids, int expectedId)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == expectedId) return true;
            }

            return false;
        }

        private static void AssertPackageResourceExists(string resourcePath)
        {
            var packagePath = Path.Combine("Packages", "com.abilitykit.demo.moba.view.runtime", "Resources", resourcePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(packagePath), $"Package runtime resource missing: {packagePath}");
        }

        private sealed class RecordingUnityHeadlessDriver : IBattleTestScriptDriver
        {
            public int AppliedTickCount { get; private set; }

            public void Apply(BattleTestStep step)
            {
                AppliedTickCount++;
            }

            public void Tick()
            {
            }
        }
    }
}
