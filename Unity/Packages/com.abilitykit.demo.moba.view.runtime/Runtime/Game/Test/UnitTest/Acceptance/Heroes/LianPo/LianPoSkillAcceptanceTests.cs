using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.GameplayTags;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class LianPoSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const string Skill10010101ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010101.expected.json";
        private const string Skill10010101ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010101_scenario.expected.json";
        private const string Skill10010201ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010201_scenario.expected.json";
        private const string Skill10010301ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010301_scenario.expected.json";
        private const string Skill10010301InsertSkill1ScenarioExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010301_insert_skill_1_scenario.expected.json";
        private const string Skill10010401ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010401.expected.json";

        [Test]
        public void Skill10010101_ExportsTraceAndMatchesGoldenExpectation()
        {
            var summary = RunExpectationFile(Skill10010101ExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.skillCastTraceFound);
            Assert.IsTrue(summary.result.effectExecutionTraceFound);
            Assert.IsTrue(summary.result.allExpectedActionsExecuted);
            Assert.Greater(summary.result.effectRootId, 0);
            AssertArtifactsExist(summary);
        }

        [Test]
        public void Skill10010101_ScenarioExportsTraceAndConfirmsDashHitDamageAndKnockup()
        {
            var summary = RunExpectationFile(Skill10010101ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.buffApplied);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10010101ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var effectRootId), "Missing effect root trace for Lian Po skill 1 scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, 0, 10010101, expectedHitCount: 1, "Lian Po skill 1 dash collision should damage exactly one target once during motion hit detection.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, 0, "EffectAction", -13684592, "Lian Po skill 1 hit should execute the configured knock-up pull action with a valid target.");
        }

        [Test]
        public void Skill10010101_ShouldMoveCasterForwardDuringDash()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10010101, 10010201, 10010301 }, heroId: 1, attributeTemplateId: 1001))
            {
                harness.EnterGameAndWarmup(reason: "lian po skill 1 dash movement contract");

                var actorId = harness.AssertPlayerActorBound();
                var entity = harness.AssertActorEntity(actorId);
                Assert.IsTrue(entity.hasTransform, "Lian Po actor must have a transform before casting skill 1.");
                Assert.IsTrue(entity.hasMotion, "Lian Po actor must have motion before casting skill 1.");
                Assert.IsTrue(entity.motion.Initialized, "Lian Po motion must be initialized before casting skill 1.");
                Assert.IsTrue(HasMotionPipeline(entity), "Lian Po motion pipeline must exist before casting skill 1.");

                var start = entity.transform.Value.Position;
                var sourceCountBeforeCast = GetMotionSourceCount(entity);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var castResult = skills.TryCastBySlot(actorId, slot: 1, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(castResult.Success, $"Lian Po skill 1 cast should succeed before asserting dash movement. failReason={castResult.FailReason}");

                harness.Tick(1);

                harness.AssertSkillCastTrace(10010101);
                var effectTrace = harness.AssertEffectExecutionTrace(10010101);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DashId.Value, TriggeringConstants.Actions.Dash);

                entity = harness.AssertActorEntity(actorId);
                Assert.IsTrue(entity.hasMotion, "Lian Po actor must still have motion after the skill action frame.");
                Assert.IsTrue(HasMotionPipeline(entity), "Lian Po motion pipeline must still exist after the skill action frame.");
                var sourceCountAfterActionFrame = GetMotionSourceCount(entity);
                Assert.Greater(sourceCountAfterActionFrame, sourceCountBeforeCast, $"Lian Po skill 1 dash action should add a motion source. before={sourceCountBeforeCast}, afterActionFrame={sourceCountAfterActionFrame}, state={harness.DescribeSkillRuntimeState(actorId, 1)}");

                harness.Tick(1);

                entity = harness.AssertActorEntity(actorId);
                Assert.IsTrue(entity.hasTransform, "Lian Po actor must still have a transform after the first motion tick.");
                Assert.IsTrue(entity.hasMotion, "Lian Po actor must still have motion after the first motion tick.");
                var firstMotionEnd = entity.transform.Value.Position;
                var firstMotionDelta = new Vec3(firstMotionEnd.X - start.X, 0f, firstMotionEnd.Z - start.Z);
                Assert.Greater(firstMotionDelta.Magnitude, 0.01f, $"Lian Po skill 1 dash motion source should be consumed by MotionTick on the frame after it is added. start={start}, firstMotionEnd={firstMotionEnd}, delta={firstMotionDelta}, motionOutput={DescribeMotionOutput(entity)}, sourceCount={GetMotionSourceCount(entity)}");

                harness.Tick(23);

                entity = harness.AssertActorEntity(actorId);
                Assert.IsTrue(entity.hasTransform, "Lian Po actor must still have a transform after casting skill 1.");
                var end = entity.transform.Value.Position;
                var planarDelta = new Vec3(end.X - start.X, 0f, end.Z - start.Z);

                Assert.Greater(planarDelta.Magnitude, 1f, $"Lian Po skill 1 dash should move the caster on the XZ plane. start={start}, end={end}, delta={planarDelta}");
                Assert.Greater(end.X - start.X, 1f, $"Lian Po skill 1 dash should follow the supplied +X aim direction. start={start}, end={end}");
            }
        }

        [Test]
        public void Skill10010201_ScenarioExportsTraceAndConfirmsShieldAreaDamageAndSlow()
        {
            var summary = RunExpectationFile(Skill10010201ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.areaSpawned);
            Assert.IsTrue(summary.result.buffApplied);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10010201ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var effectRootId), "Missing effect root trace for Lian Po skill 2 scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "EffectAction", 951534590, "Lian Po skill 2 should execute the configured shield action under its effect root.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "AreaSpawn", 40010201, "Lian Po skill 2 should spawn the delayed impact area under its effect root.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, effectRootId, "AreaEnter", 40010201, "Lian Po skill 2 area should emit an enter trace when the target is inside the impact area.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, 0, 10010201, expectedHitCount: 1, "Lian Po skill 2 delayed area should damage exactly one target once.");
        }

        [Test]
        public void Skill10010301_ScenarioExportsTraceAndConfirmsThirdStageKnockup()
        {
            var summary = RunExpectationFile(Skill10010301ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.areaSpawned);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10010301ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var thirdStageRootId), "Missing effect root trace for Lian Po skill 3 third stage scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, thirdStageRootId, "AreaSpawn", 40010321, "Lian Po skill 3 third stage should spawn the final slam area under its own effect root.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, thirdStageRootId, "AreaEnter", 40010321, "Lian Po skill 3 third stage area should emit an enter trace before knock-up.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, 0, "EffectAction", -13684592, "Lian Po skill 3 third stage area enter should execute the knock-up pull action with a valid target.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, 0, 10010301, expectedHitCount: 3, "Lian Po skill 3 should apply exactly three stage damage hits to the same target.");
        }

        [Test]
        public void Skill10010301_CanInsertSkill1BetweenStagesAndResumeUltimate()
        {
            var summary = RunExpectationFile(Skill10010301InsertSkill1ScenarioExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.areaSpawned);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);

            var records = LoadTraceRecords(summary);
            var expectation = LoadExpectation(Skill10010301InsertSkill1ScenarioExpectationPath);
            Assert.IsTrue(MobaAcceptanceExpectationAssert.TryGetEffectRootId(records, expectation.config.effectId, out var thirdStageRootId), "Missing effect root trace for resumed Lian Po skill 3 third stage scenario.");
            MobaAcceptanceExpectationAssert.AssertMatches(expectation, records);

            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, thirdStageRootId, "AreaSpawn", 40010321, "Lian Po skill 3 third stage should resume and spawn the final slam area after inserted skill 1 completes.");
            MobaAcceptanceTraceAssert.AssertTraceNodeKindInRoot(records, 0, "EffectExecution", 10010101, "Inserted Lian Po skill 1 should execute while skill 3 is active.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, 0, 10010301, expectedHitCount: 3, "Resumed Lian Po skill 3 should still apply exactly three stage damage hits.");
            MobaAcceptanceTraceAssert.AssertSingleTargetDamageInRoot(records, 0, 10010101, expectedHitCount: 1, "Inserted Lian Po skill 1 should apply exactly one damage hit.");
            MobaAcceptanceTraceAssert.AssertEffectOccursAfter(records, 10010311, 10010101, "Lian Po skill 3 second stage should occur after inserted skill 1 starts.");
            MobaAcceptanceTraceAssert.AssertEffectOccursAfter(records, 10010321, 10010101, "Lian Po skill 3 third stage should occur after inserted skill 1 starts.");
        }

        [TestCase(1, 10010101)]
        [TestCase(2, 10010201)]
        [TestCase(3, 10010301)]
        public void LianPoActiveSkills_ShouldCarrySuperArmorTagDuringCastPipeline(int slot, int expectedSkillId)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10010101, 10010201, 10010301 }, heroId: 1, attributeTemplateId: 1001))
            {
                harness.EnterGameAndWarmup(reason: "lian po skill pipeline super armor tag contract");

                var actorId = harness.AssertPlayerActorBound();
                var superArmorTag = GameplayTag.FromId(10010001);

                Assert.IsFalse(HasEffectiveTag(harness, actorId, superArmorTag), "Lian Po should not carry super armor before a tagged cast pipeline starts.");

                harness.SubmitSkillPress(slot);
                harness.Tick(1);

                Assert.IsTrue(harness.TryGetRunningSkillSnapshot(actorId, slot, out var snapshot), harness.DescribeSkillRuntimeState(actorId, slot));
                Assert.AreEqual(expectedSkillId, snapshot.SkillId, "The running skill should match the configured Lian Po slot.");
                Assert.IsTrue(HasEffectiveTag(harness, actorId, superArmorTag), "Lian Po should carry super armor while the cast pipeline is active.");

                harness.TickUntilSkillStops(actorId, slot, maxTicks: 180);

                Assert.IsFalse(HasEffectiveTag(harness, actorId, superArmorTag), "Pipeline super armor should be removed when the cast pipeline ends.");
            }
        }

        [Test]
        public void Skill10010000_PassiveRage_ShouldScaleStatsAndHealOutOfCombat()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10010101, 10010201, 10010301 }, heroId: 1, attributeTemplateId: 1001))
            {
                harness.EnterGameAndWarmup(reason: "lian po passive rage contract");

                var actorId = harness.AssertPlayerActorBound();
                const float baseAttackSpeed = 0f;
                const float basePhysicsDefense = 12f;
                const float baseMagicDefense = 8f;

                Assert.AreEqual(0f, harness.GetActorRage(actorId), 0.01f, "Lian Po should start with empty rage.");
                Assert.AreEqual(baseAttackSpeed, harness.GetActorAttribute(actorId, BattleAttributeType.ATTACK_SPEED_R), 0.01f, "Base attack speed ratio should come from template before combat.");
                Assert.AreEqual(basePhysicsDefense, harness.GetActorAttribute(actorId, BattleAttributeType.PHYSICS_DEFENSE), 0.01f, "Base physical defense should come from template before combat.");
                Assert.AreEqual(baseMagicDefense, harness.GetActorAttribute(actorId, BattleAttributeType.MAGIC_DEFENSE), 0.01f, "Base magic defense should come from template before combat.");

                var result = ExecutePipelineDamage(harness, attackerActorId: 0, targetActorId: actorId, baseDamage: 30f);
                Assert.IsNotNull(result, "Damage pipeline should produce a result for passive rage test setup.");
                Assert.AreEqual(30f, result.Value, 0.01f, "Pipeline damage should be applied for passive rage test setup.");

                harness.Tick(1);

                Assert.AreEqual(5f, harness.GetActorRage(actorId), 0.01f, "Taking damage should add rage through Lian Po passive.");
                Assert.AreEqual(0.015f, harness.GetActorAttribute(actorId, BattleAttributeType.ATTACK_SPEED_R), 0.001f, "Rage should add attack speed ratio proportionally.");
                Assert.AreEqual(13f, harness.GetActorAttribute(actorId, BattleAttributeType.PHYSICS_DEFENSE), 0.01f, "Rage should add physical defense proportionally.");
                Assert.AreEqual(9f, harness.GetActorAttribute(actorId, BattleAttributeType.MAGIC_DEFENSE), 0.01f, "Rage should add magic defense proportionally.");

                var hpAfterDamage = harness.GetActorHp(actorId);
                harness.TickSeconds(3.1f);
                harness.TickSeconds(0.5f);

                Assert.Less(harness.GetActorRage(actorId), 5f, "Out-of-combat conversion should consume rage.");
                Assert.Greater(harness.GetActorHp(actorId), hpAfterDamage, "Out-of-combat conversion should heal Lian Po from consumed rage.");
            }
        }

        [Test]
        public void Skill10010000_PassiveRage_ShouldGainOnDamageDealtAndClampAtMax()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10010101, 10010201, 10010301 }, heroId: 1, attributeTemplateId: 1001))
            {
                harness.EnterGameAndWarmup(reason: "lian po passive rage damage dealt contract");

                var actorId = harness.AssertPlayerActorBound();
                Assert.AreEqual(0f, harness.GetActorRage(actorId), 0.01f, "Lian Po should start with empty rage before dealing damage.");

                for (var i = 0; i < 12; i++)
                {
                    var result = ExecutePipelineDamage(harness, attackerActorId: actorId, targetActorId: actorId, baseDamage: 1f);
                    Assert.IsNotNull(result, "Damage pipeline should produce a result for repeated rage gain setup.");
                    Assert.Greater(result.Value, 0f, "Damage dealt setup should apply positive damage so rage can be gained.");
                }

                harness.Tick(1);

                Assert.AreEqual(100f, harness.GetActorRage(actorId), 0.01f, "Lian Po rage should clamp at the configured maximum when damage dealt would overfill it.");
                Assert.AreEqual(0.3f, harness.GetActorAttribute(actorId, BattleAttributeType.ATTACK_SPEED_R), 0.001f, "Full rage should grant the full attack speed bonus.");
                Assert.AreEqual(32f, harness.GetActorAttribute(actorId, BattleAttributeType.PHYSICS_DEFENSE), 0.01f, "Full rage should grant the full physical defense bonus.");
                Assert.AreEqual(28f, harness.GetActorAttribute(actorId, BattleAttributeType.MAGIC_DEFENSE), 0.01f, "Full rage should grant the full magic defense bonus.");
            }
        }

        [Test]
        public void Skill10010000_PassiveRage_ShouldIgnoreActorsWithoutLianPoPassive()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 10020101, 10020201, 10020301 }, heroId: 1002, attributeTemplateId: 1002))
            {
                harness.EnterGameAndWarmup(reason: "non lian po passive rage exclusion contract");

                var actorId = harness.AssertPlayerActorBound();
                var result = ExecutePipelineDamage(harness, attackerActorId: actorId, targetActorId: actorId, baseDamage: 10f);
                Assert.IsNotNull(result, "Damage pipeline should produce a result for non-Lian-Po exclusion setup.");
                Assert.Greater(result.Value, 0f, "Non-Lian-Po exclusion setup should apply positive damage.");

                harness.Tick(1);

                Assert.AreEqual(0f, harness.GetActorRage(actorId), 0.01f, "Actors without Lian Po passive should not gain rage from dealing or taking damage.");
            }
        }

        [Test]
        public void Skill10010401_ExportsTraceAndMatchesBuffGoldenExpectation()
        {
            var summary = RunExpectationFile(Skill10010401ExpectationPath);

            AssertPassed(summary);
            Assert.IsTrue(summary.result.skillCastTraceFound);
            Assert.IsTrue(summary.result.effectExecutionTraceFound);
            Assert.IsTrue(summary.result.allExpectedActionsExecuted);
            Assert.IsTrue(summary.result.buffApplied);
            Assert.Greater(summary.result.effectRootId, 0);
            AssertNoMissingTraceNodes(summary);
            AssertArtifactsExist(summary);
        }

        private static bool HasEffectiveTag(MobaSkillConfigTestHarness harness, int actorId, GameplayTag tag)
        {
            var tags = harness.World.Services.Resolve<IMobaEffectiveTagQueryService>().GetEffectiveTags(actorId);
            return tags != null && tags.HasTagExact(tag);
        }

        private static bool HasMotionPipeline(ActorEntity entity)
        {
            return GetMotionPipeline(entity) != null;
        }

        private static int GetMotionSourceCount(ActorEntity entity)
        {
            var pipeline = GetMotionPipeline(entity);
            if (pipeline == null) return -1;

            var sourceCountProperty = pipeline.GetType().GetProperty("SourceCount");
            return sourceCountProperty != null ? (int)sourceCountProperty.GetValue(pipeline, null) : -1;
        }

        private static string DescribeMotionOutput(ActorEntity entity)
        {
            if (entity == null || !entity.hasMotion) return "<no motion>";

            var motion = (object)entity.motion;
            var output = motion.GetType().GetField("Output")?.GetValue(motion);
            if (output == null) return "<no output>";

            var outputType = output.GetType();
            var desired = outputType.GetField("DesiredDelta")?.GetValue(output);
            var applied = outputType.GetField("AppliedDelta")?.GetValue(output);
            return $"desiredDelta={desired}, appliedDelta={applied}";
        }

        private static object GetMotionPipeline(ActorEntity entity)
        {
            if (entity == null || !entity.hasMotion) return null;

            var motion = (object)entity.motion;
            return motion.GetType().GetField("Pipeline")?.GetValue(motion);
        }

        private static DamageResult ExecutePipelineDamage(MobaSkillConfigTestHarness harness, int attackerActorId, int targetActorId, float baseDamage)
        {
            var damage = harness.World.Services.Resolve<DamagePipelineService>();
            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = DamageType.Physical,
                ReasonKind = DamageReasonKind.Environment,
                ReasonParam = 0
            };
            attack.BaseDamage.BaseValue = baseDamage;
            return damage.Execute(attack);
        }
    }
}
