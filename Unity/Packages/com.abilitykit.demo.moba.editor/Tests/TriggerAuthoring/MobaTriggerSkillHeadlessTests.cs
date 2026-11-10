#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Eventing;
using AbilityKit.Combat.Collision;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Predicates;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Continuous;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests.TriggerAuthoring
{
    public sealed class MobaTriggerSkillHeadlessTests
    {
        private const int ExistingSkillId = 10010101;
        private const int BuffId = 10020001;
        private const int ProjectileLauncherId = 31020101;
        private const int ProjectileTemplateId = 30020101;
        private const int AreaTemplateId = 40060101;

        [TestCase(0d, "TRG2026")]
        [TestCase(-1d, "TRG2026")]
        [TestCase(1.5d, "TRG1301")]
        [TestCase(double.NaN, "TRG1301")]
        public void QueryExistencePredicate_RejectsInvalidTemplateIdsDuringExport(double queryId, string diagnostic)
        {
            var failure = Assert.Throws<AssertionException>(() =>
                MobaTriggerSkillFixtureCompiler.Compile("moba-self-buff-skill.trigger.json",
                    MobaTriggerAuthoringTestIds.SelfBuff, module => module.Triggers[0].Condition = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Condition, Type = "has_query_target",
                        Arguments = new List<TriggerArgumentData>
                        {
                            new TriggerArgumentData
                            {
                                Name = "query_template_id", Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.Constant,
                                    Type = queryId == Math.Truncate(queryId)
                                        ? TriggerValueType.Integer : TriggerValueType.Number,
                                    IntegerValue = queryId == Math.Truncate(queryId) ? (long)queryId : 0,
                                    NumberValue = queryId
                                }
                            }
                        }
                    }));
            Assert.That(failure.Message, Does.Contain(diagnostic));
        }

        [TestCase(0f, 2, false, false)]
        [TestCase(3.9f, 1, true, false)]
        [TestCase(4.01f, 2, true, false)]
        [TestCase(4f, 2, true, true)]
        [TestCase(3.9f, 2, true, true)]
        public void QueryExistencePredicate_UsesPublishedRadiusTeamAndSelfBuff(
            float distance, int targetTeam, bool spawnTarget, bool expected)
        {
            using var harness = CreateQueryPresenceHarness();
            var casterId = harness.AssertPlayerActorBound();
            var caster = harness.AssertActorEntity(casterId);
            var position = new Vec3(20, 0, 20);
            caster.ReplaceTransform(new Transform3(position, Quat.Identity, Vec3.One));
            var targetId = spawnTarget
                ? SpawnScenarioActor(harness, "query_target", 9_911_001, 20 + distance, 20, targetTeam)
                : 0;
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
            Assert.That(harness.HasActorBuff(casterId, BuffId), Is.EqualTo(expected));
            if (expected)
                Assert.That(caster.buffs.Active.Count(buff => buff.BuffId == BuffId), Is.EqualTo(1));
            if (targetId > 0) Assert.That(harness.HasActorBuff(targetId, BuffId), Is.False);
        }

        [Test]
        public void QueryExistencePredicate_ConsecutiveCastersDoNotShareQueryResults()
        {
            using var harness = CreateQueryPresenceHarness();
            var casterId = harness.AssertPlayerActorBound();
            var caster = harness.AssertActorEntity(casterId);
            caster.ReplaceTransform(new Transform3(new Vec3(20, 0, 20), Quat.Identity, Vec3.One));
            var otherCaster = SpawnScenarioActor(harness, "other_caster", 9_911_002, 40, 20, teamId: 1);
            var enemy = SpawnScenarioActor(harness, "query_enemy", 9_911_003, 23, 20, teamId: 2);
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
            effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(otherCaster));
            Assert.That(harness.HasActorBuff(casterId, BuffId), Is.True);
            Assert.That(harness.HasActorBuff(otherCaster, BuffId), Is.False);
            // Recreate the same query facts after an intervening failed query: no persistent result exists.
            var target = harness.AssertActorEntity(enemy);
            target.ReplaceTransform(new Transform3(new Vec3(43, 0, 20), Quat.Identity, Vec3.One));
            effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(otherCaster));
            Assert.That(harness.HasActorBuff(otherCaster, BuffId), Is.True);
        }

        [Test]
        public void QueryExistencePredicate_NestedEffectsRestoreOuterQueryContext()
        {
            using var harness = CreateQueryPresenceHarness();
            var casterId = harness.AssertPlayerActorBound();
            harness.AssertActorEntity(casterId).ReplaceTransform(
                new Transform3(new Vec3(20, 0, 20), Quat.Identity, Vec3.One));
            var otherCaster = SpawnScenarioActor(harness, "nested_caster", 9_911_004, 40, 20, teamId: 1);
            SpawnScenarioActor(harness, "nested_enemy", 9_911_005, 23, 20, teamId: 2);
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            var functions = harness.World.Services.Resolve<FunctionRegistry>();
            var id = MobaPlanPredicateFunctions.HasQueryTargetFunctionId;
            Assert.That(functions.TryGet<Predicate2<object, IWorldResolver>>(id, out var original,
                out var deterministic), Is.True);
            var nested = false;
            var innerCalls = 0;
            var outerCalls = 0;
            functions.Register<Predicate2<object, IWorldResolver>>(id, (payload, args, ctx) =>
            {
                var before = original(payload, args, ctx);
                if (nested)
                {
                    innerCalls++;
                    Assert.That(before, Is.False);
                    return before;
                }
                outerCalls++;
                nested = true;
                try
                {
                    effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(otherCaster));
                }
                finally { nested = false; }
                Assert.That(original(payload, args, ctx), Is.EqualTo(before),
                    "Nested execution must restore the outer effect's query source.");
                return before;
            }, deterministic);
            try
            {
                effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
                Assert.That(outerCalls, Is.EqualTo(1));
                Assert.That(innerCalls, Is.EqualTo(1));
                Assert.That(harness.HasActorBuff(casterId, BuffId), Is.True);
                Assert.That(harness.HasActorBuff(otherCaster, BuffId), Is.False);
            }
            finally { functions.Register(id, original, deterministic); }
        }

        [Test]
        public void QueryExistencePredicate_RecomputesAfterAuthoritativeTransformRestore()
        {
            using var harness = CreateQueryPresenceHarness();
            var casterId = harness.AssertPlayerActorBound();
            harness.AssertActorEntity(casterId).ReplaceTransform(
                new Transform3(new Vec3(20, 0, 20), Quat.Identity, Vec3.One));
            var enemy = SpawnScenarioActor(harness, "restore_enemy", 9_911_006, 23, 20, teamId: 2);
            var transforms = new MobaActorTransformRollbackProvider(harness.World.Services.Resolve<MobaActorRegistry>());
            var frame = new FrameIndex(100);
            var snapshot = transforms.ExportState(frame);
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            var functions = harness.World.Services.Resolve<FunctionRegistry>();
            var id = MobaPlanPredicateFunctions.HasQueryTargetFunctionId;
            Assert.That(functions.TryGet<Predicate2<object, IWorldResolver>>(id, out var original,
                out var deterministic), Is.True);
            var results = new List<bool>();
            functions.Register<Predicate2<object, IWorldResolver>>(id, (payload, args, ctx) =>
            {
                var result = original(payload, args, ctx);
                results.Add(result);
                return result;
            }, deterministic);
            try
            {
                effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
                harness.AssertActorEntity(enemy).ReplaceTransform(
                    new Transform3(new Vec3(43, 0, 20), Quat.Identity, Vec3.One));
                effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
                transforms.ImportState(frame, snapshot);
                effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.SelfBuff, new ActorPayload(casterId));
                Assert.That(results, Is.EqualTo(new[] { true, false, true }));
            }
            finally { functions.Register(id, original, deterministic); }
        }

        private static MobaSkillConfigTestHarness CreateQueryPresenceHarness()
        {
            var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { ExistingSkillId }, worldId: "query_presence_contract", heroId: 1001,
                attributeTemplateId: 1001);
            var plans = MobaTriggerSkillFixtureCompiler.Compile("moba-self-buff-skill.trigger.json",
                MobaTriggerAuthoringTestIds.SelfBuff, module =>
                {
                    var trigger = module.Triggers[0];
                    trigger.Scope = "global";
                    trigger.Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action, Type = "conditional",
                        Condition = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition, Type = "has_query_target",
                            Arguments = new List<TriggerArgumentData>
                            {
                                new TriggerArgumentData
                                {
                                    Name = "query_template_id", Value = new TriggerValueRefData
                                    {
                                        Source = TriggerValueSource.Constant, Type = TriggerValueType.Integer,
                                        IntegerValue = 50070101
                                    }
                                }
                            }
                        },
                        Children = new List<TriggerNodeData> { trigger.Actions }
                    };
                });
            harness.TriggerPlans.MergeFrom(plans, replaceExisting: true);
            harness.EnterGameAndWarmup(reason: "stateless query predicate contract");
            return harness;
        }

        private static readonly FixtureCase[] FixtureCases =
        {
            new FixtureCase(
                "moba-target-collection-skill.trigger.json",
                MobaTriggerAuthoringTestIds.TargetCollectionDamage),
            new FixtureCase(
                "moba-health-percent-damage-skill.trigger.json",
                MobaTriggerAuthoringTestIds.HealthPercentDamage),
            new FixtureCase(
                "moba-self-buff-skill.trigger.json",
                MobaTriggerAuthoringTestIds.SelfBuff),
            new FixtureCase(
                "moba-tracked-projectile-skill.trigger.json",
                MobaTriggerAuthoringTestIds.TrackedProjectile),
            new FixtureCase(
                "moba-forward-dash-skill.trigger.json",
                MobaTriggerAuthoringTestIds.ForwardDash),
            new FixtureCase(
                "moba-shield-resource-skill.trigger.json",
                MobaTriggerAuthoringTestIds.ShieldAndResource),
            new FixtureCase(
                "moba-bounded-group-pull-skill.trigger.json",
                MobaTriggerAuthoringTestIds.BoundedGroupPull),
            new FixtureCase(
                "moba-persistent-area-skill.trigger.json",
                MobaTriggerAuthoringTestIds.PersistentArea),
            new FixtureCase(
                "moba-overheal-to-shield-skill.trigger.json",
                MobaTriggerAuthoringTestIds.OverhealToShield)
        };

        [Test]
        public void FixtureIds_AreUniqueAndInsideReservedDomains()
        {
            var ids = FixtureCases.Select(testCase => testCase.TriggerId).ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Has.All.InRange(
                MobaTriggerAuthoringTestIds.ReservedStart,
                MobaTriggerAuthoringTestIds.ReservedEnd));
            Assert.That(MobaTriggerAuthoringTestIds.TargetCollectionDamage,
                Is.InRange(MobaTriggerAuthoringTestIds.TargetingStart, MobaTriggerAuthoringTestIds.TargetingEnd));
            Assert.That(MobaTriggerAuthoringTestIds.SelfBuff,
                Is.InRange(MobaTriggerAuthoringTestIds.BuffStart, MobaTriggerAuthoringTestIds.BuffEnd));
            Assert.That(MobaTriggerAuthoringTestIds.TrackedProjectile,
                Is.InRange(MobaTriggerAuthoringTestIds.ProjectileStart, MobaTriggerAuthoringTestIds.ProjectileEnd));
            Assert.That(MobaTriggerAuthoringTestIds.ForwardDash,
                Is.InRange(MobaTriggerAuthoringTestIds.MotionStart, MobaTriggerAuthoringTestIds.MotionEnd));
            Assert.That(MobaTriggerAuthoringTestIds.ShieldAndResource,
                Is.InRange(MobaTriggerAuthoringTestIds.CompositeStart, MobaTriggerAuthoringTestIds.CompositeEnd));
            Assert.That(MobaTriggerAuthoringTestIds.BoundedGroupPull,
                Is.InRange(MobaTriggerAuthoringTestIds.CompositeStart, MobaTriggerAuthoringTestIds.CompositeEnd));
            Assert.That(MobaTriggerAuthoringTestIds.PersistentArea,
                Is.InRange(MobaTriggerAuthoringTestIds.CompositeStart, MobaTriggerAuthoringTestIds.CompositeEnd));
            Assert.That(MobaTriggerAuthoringTestIds.OverhealToShield,
                Is.InRange(MobaTriggerAuthoringTestIds.CompositeStart, MobaTriggerAuthoringTestIds.CompositeEnd));
        }

        [Test]
        public void Source_target_tag_rule_condition_exports_from_authoring_model()
        {
            var database = MobaTriggerSkillFixtureCompiler.Compile(
                "moba-self-buff-skill.trigger.json",
                MobaTriggerAuthoringTestIds.SelfBuff,
                module => module.Triggers[0].Condition = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "source_target_tag_rules",
                    Arguments = new List<TriggerArgumentData>
                    {
                        new TriggerArgumentData
                        {
                            Name = "source_rule_group_id",
                            Value = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.Integer,
                                IntegerValue = 2004
                            }
                        }
                    }
                });
            Assert.That(database, Is.Not.Null);
        }

        [Test]
        public void Owner_team_relation_conditions_export_from_authoring_model()
        {
            var database = MobaTriggerSkillFixtureCompiler.Compile(
                "moba-self-buff-skill.trigger.json",
                MobaTriggerAuthoringTestIds.SelfBuff,
                module => module.Triggers[0].Condition = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "all",
                    Children = new List<TriggerNodeData>
                    {
                        new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            Type = "owner_same_team_as_payload_source"
                        },
                        new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            Type = "owner_enemy_to_payload_target"
                        }
                    }
                });

            Assert.That(database, Is.Not.Null);
            Assert.That(database.TryGetExecutionRootByTriggerId(
                MobaTriggerAuthoringTestIds.SelfBuff, out _), Is.True);
        }

        [Test]
        public void ReservedTriggerIds_DoNotOverlapFormalAuthoringSources()
        {
            var sourceDirectory = Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "com.abilitykit.demo.moba.view.runtime",
                "Configs",
                "MobaTriggerAuthoring",
                "Sources");
            Assert.That(Directory.Exists(sourceDirectory), Is.True,
                $"Formal MOBA Trigger Authoring source directory is missing: {sourceDirectory}");

            var collisions = Directory.GetFiles(sourceDirectory, "*.trigger.json")
                .SelectMany(file => TriggerAuthoringSourceCodec.ReadFile(file).Module.Triggers
                    .Where(trigger => MobaTriggerAuthoringTestIds.IsReserved(trigger.Id))
                    .Select(trigger => $"{trigger.Id}:{Path.GetFileName(file)}"))
                .ToArray();

            Assert.That(collisions, Is.Empty,
                "Formal MOBA triggers must not use the reserved authoring-test ID range.");
        }

        [Test]
        public void FormalAuthoringSources_AddBuffLifetimeComesFromBuffConfig()
        {
            var sourceDirectory = Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "com.abilitykit.demo.moba.view.runtime",
                "Configs",
                "MobaTriggerAuthoring",
                "Sources");
            var violations = new List<string>();

            foreach (var file in Directory.GetFiles(sourceDirectory, "*.trigger.json"))
            {
                var module = TriggerAuthoringSourceCodec.ReadFile(file).Module;
                foreach (var trigger in module.Triggers)
                {
                    if (EnumerateNodes(trigger.Actions).Any(HasAddBuffDuration))
                        violations.Add($"trigger {trigger.Id}:{Path.GetFileName(file)}");
                }
                foreach (var group in module.ActionGroups)
                {
                    if (EnumerateNodes(group.Root).Any(HasAddBuffDuration))
                        violations.Add($"action group {group.Id}:{Path.GetFileName(file)}");
                }
            }

            Assert.That(violations, Is.Empty,
                "add_buff.duration_ms is ignored by runtime; Buff config DurationMs is authoritative.");
        }

        [TestCaseSource(nameof(FixtureCases))]
        public void SourceFixture_ExportsThroughMobaExtension(FixtureCase fixture)
        {
            MobaTriggerSkillFixtureCompiler.Compile(fixture.FileName, fixture.TriggerId);
        }

        [Test]
        public void TargetCollectionSkill_DamagesOnlyBoundedNearestEnemies()
        {
            using var harness = CreateHarness(
                "target_collection_trigger_headless_world",
                "moba-target-collection-skill.trigger.json",
                MobaTriggerAuthoringTestIds.TargetCollectionDamage);
            var casterId = harness.AssertPlayerActorBound();
            var casterHp = harness.GetActorHp(casterId);
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            var payload = new ActorPayload(casterId);

            Assert.That(effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.TargetCollectionDamage, payload), Is.True,
                "An empty enemy query must remain a successful no-op.");
            Assert.That(harness.GetActorHp(casterId), Is.EqualTo(casterHp).Within(0.001f));

            var enemyIds = new[]
            {
                SpawnEnemy(harness, "collection_enemy_near", 9_811_001, 1f, 0f),
                SpawnEnemy(harness, "collection_enemy_mid", 9_811_002, 2f, 0f),
                SpawnEnemy(harness, "collection_enemy_far", 9_811_003, 3f, 0f)
            };
            var hpBefore = enemyIds.Select(actorId => harness.GetActorHp(actorId)).ToArray();

            Assert.That(effects.ExecuteRulePlan(MobaTriggerAuthoringTestIds.TargetCollectionDamage, payload), Is.True);

            var damagedCount = enemyIds.Count(actorId =>
            {
                var index = System.Array.IndexOf(enemyIds, actorId);
                return harness.GetActorHp(actorId) < hpBefore[index] - 0.001f;
            });
            Assert.That(damagedCount, Is.EqualTo(2),
                "The query returns three enemies, but for_each must enforce max_iterations=2.");
            Assert.That(harness.GetActorHp(casterId), Is.EqualTo(casterHp).Within(0.001f));
        }

        [Test]
        public void HealthPercentDamage_UsesCurrentTargetHpBeforeDamageCalculation()
        {
            using var harness = CreateHarness(
                "health_percent_damage_trigger_headless_world",
                "moba-health-percent-damage-skill.trigger.json",
                MobaTriggerAuthoringTestIds.HealthPercentDamage);
            var casterId = harness.AssertPlayerActorBound();
            var targetId = SpawnEnemy(harness, "health_percent_low_target", 9_812_001, 4f, 0f);
            var maxHp = new MobaAttrs(harness.AssertActorEntity(targetId)).MaxHp;
            SetActorHp(harness, targetId, maxHp * 0.4f);

            using var scope = BindOwnerTrigger(harness, MobaTriggerAuthoringTestIds.HealthPercentDamage, casterId);
            var damage = harness.World.Services.Resolve<MobaCombatEffectService>();
            var result = damage.DealDamage(CreateAttack(casterId, targetId));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value, Is.EqualTo(26f).Within(0.001f),
                "40% current HP means 60% missing HP; coefficient 0.5 adds 30% to 20 base damage.");
            Assert.That(harness.GetActorHp(targetId), Is.EqualTo(maxHp * 0.4f - 26f).Within(0.001f));
            Assert.That(harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>()
                .TryGetLastEvaluation(scope.OwnerKey, MobaTriggerAuthoringTestIds.HealthPercentDamage, out var evaluation), Is.True);
            Assert.That(evaluation.PlanPassed, Is.True);
        }

        [Test]
        public void HealthPercentDamage_DoesNotModifyDamageAboveThreshold()
        {
            using var harness = CreateHarness(
                "health_percent_damage_high_target_world",
                "moba-health-percent-damage-skill.trigger.json",
                MobaTriggerAuthoringTestIds.HealthPercentDamage);
            var casterId = harness.AssertPlayerActorBound();
            var targetId = SpawnEnemy(harness, "health_percent_high_target", 9_812_002, 4f, 0f);
            var maxHp = new MobaAttrs(harness.AssertActorEntity(targetId)).MaxHp;
            SetActorHp(harness, targetId, maxHp * 0.75f);

            using var scope = BindOwnerTrigger(harness, MobaTriggerAuthoringTestIds.HealthPercentDamage, casterId);
            var result = harness.World.Services.Resolve<MobaCombatEffectService>()
                .DealDamage(CreateAttack(casterId, targetId));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value, Is.EqualTo(20f).Within(0.001f));
            Assert.That(harness.GetActorHp(targetId), Is.EqualTo(maxHp * 0.75f - 20f).Within(0.001f));
            var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
            Assert.That(subscriptions.TryGetLastEvaluation(
                scope.OwnerKey, MobaTriggerAuthoringTestIds.HealthPercentDamage, out var evaluation), Is.True);
            Assert.That(evaluation.PlanPassed, Is.False);
        }

        [Test]
        public void HealthPercentDamage_LeavesDamageUnchangedWhenTargetIsMissing()
        {
            using var harness = CreateHarness(
                "health_percent_damage_missing_target_world",
                "moba-health-percent-damage-skill.trigger.json",
                MobaTriggerAuthoringTestIds.HealthPercentDamage);
            var casterId = harness.AssertPlayerActorBound();
            using var scope = BindOwnerTrigger(harness, MobaTriggerAuthoringTestIds.HealthPercentDamage, casterId);

            var result = harness.World.Services.Resolve<MobaCombatEffectService>()
                .DealDamage(CreateAttack(casterId, 9_812_099));

            Assert.That(result, Is.Null, "The damage pipeline must reject a missing target before before_calc is dispatched.");
            var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
            Assert.That(subscriptions.TryGetLastEvaluation(
                scope.OwnerKey, MobaTriggerAuthoringTestIds.HealthPercentDamage, out _), Is.False);
        }

        [Test]
        public void SelfBuffSkill_AppliesConfiguredBuffToCaster()
        {
            using var harness = CreateHarness(
                "self_buff_trigger_headless_world",
                "moba-self-buff-skill.trigger.json",
                MobaTriggerAuthoringTestIds.SelfBuff);
            var casterId = harness.AssertPlayerActorBound();
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();

            Assert.That(harness.HasActorBuff(casterId, BuffId), Is.False);
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.SelfBuff,
                new ActorPayload(casterId)), Is.True);
            Assert.That(harness.HasActorBuff(casterId, BuffId), Is.True,
                "The configured MOBA Buff must be present on the caster after execution.");
        }

        [Test]
        public void TrackedProjectileSkill_SearchesEnemyAndLaunchesMovingProjectile()
        {
            using var harness = CreateHarness(
                "tracked_projectile_trigger_headless_world",
                "moba-tracked-projectile-skill.trigger.json",
                MobaTriggerAuthoringTestIds.TrackedProjectile);
            var casterId = harness.AssertPlayerActorBound();
            SpawnEnemy(harness, "projectile_enemy", 9_831_001, 8f, 0f);
            harness.AssertProjectileConfigExists(ProjectileLauncherId, ProjectileTemplateId);

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.TrackedProjectile,
                new ActorPayload(casterId)), Is.True);

            var spawn = TickUntilProjectileSpawn(harness, ProjectileTemplateId, maxTicks: 5);
            Assert.That(spawn.OwnerActorId, Is.EqualTo(casterId));
            Assert.That(spawn.ProjectileActorId, Is.GreaterThan(0));
            Assert.That(spawn.ForwardX, Is.GreaterThan(0.9f),
                "Nearest-enemy target search should aim the projectile along positive X.");

            var projectile = harness.AssertActorEntity(spawn.ProjectileActorId);
            Assert.That(projectile.hasTransform, Is.True);
            var start = projectile.transform.Value.Position;
            var end = TickUntilPositionXGreaterThan(
                harness,
                spawn.ProjectileActorId,
                start.X + 0.05f,
                maxTicks: 10);
            Assert.That(end.X, Is.GreaterThan(start.X + 0.05f));
        }

        [Test]
        public void ForwardDashSkill_MovesCasterForBoundedDuration()
        {
            using var harness = CreateHarness(
                "forward_dash_trigger_headless_world",
                "moba-forward-dash-skill.trigger.json",
                MobaTriggerAuthoringTestIds.ForwardDash);
            var casterId = harness.AssertPlayerActorBound();
            var caster = harness.AssertActorEntity(casterId);
            Assert.That(caster.hasTransform, Is.True);
            var start = caster.transform.Value.Position;

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.ForwardDash,
                new ActorPayload(casterId)), Is.True);
            harness.Tick(12);

            var end = harness.AssertActorEntity(casterId).transform.Value.Position;
            var planarDelta = new Vec3(end.X - start.X, 0f, end.Z - start.Z);
            Assert.That(planarDelta.SqrMagnitude, Is.GreaterThan(0.25f),
                $"Dash should move the caster. start={start}, end={end}");
        }

        [Test]
        public void ShieldAndResourceSkill_ComposesExistingDefensiveSystems()
        {
            using var harness = CreateHarness(
                "shield_resource_trigger_headless_world",
                "moba-shield-resource-skill.trigger.json",
                MobaTriggerAuthoringTestIds.ShieldAndResource);
            var casterId = harness.AssertPlayerActorBound();
            var rageBefore = harness.GetActorRage(casterId);
            var shields = harness.World.Services.Resolve<MobaShieldService>();
            var shieldBefore = shields.GetTotalRemaining(casterId);

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.ShieldAndResource,
                new ActorPayload(casterId)), Is.True);

            Assert.That(shields.GetTotalRemaining(casterId), Is.EqualTo(shieldBefore + 180f).Within(0.001f));
            Assert.That(harness.GetActorRage(casterId), Is.EqualTo(rageBefore + 15f).Within(0.001f));
        }

        [Test]
        public void BoundedGroupPullSkill_MovesOnlyTwoNearestEnemiesTowardCaster()
        {
            using var harness = CreateHarness(
                "bounded_group_pull_trigger_headless_world",
                "moba-bounded-group-pull-skill.trigger.json",
                MobaTriggerAuthoringTestIds.BoundedGroupPull);
            var casterId = harness.AssertPlayerActorBound();
            var enemyIds = new[]
            {
                SpawnEnemy(harness, "pull_enemy_near", 9_891_001, 2f, 0f),
                SpawnEnemy(harness, "pull_enemy_mid", 9_891_002, 3f, 0f),
                SpawnEnemy(harness, "pull_enemy_far", 9_891_003, 4f, 0f)
            };
            harness.Tick(1);
            var starts = enemyIds.Select(actorId =>
                harness.AssertActorEntity(actorId).transform.Value.Position).ToArray();

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            var executed = effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.BoundedGroupPull,
                new ActorPayload(casterId));
            Assert.That(executed, Is.True, BuildPlanActionFailureMessage(harness));
            var continuous = harness.World.Services.Resolve<IContinuousManager>();
            var activeCounts = enemyIds.Select(actorId =>
                continuous.GetOwnerActiveContinuous(actorId).Count).ToArray();
            harness.Tick(12);

            var ends = enemyIds.Select(actorId =>
                harness.AssertActorEntity(actorId).transform.Value.Position).ToArray();
            var positions = $"starts={string.Join(",", starts.Select(value => value.X))}; " +
                $"ends={string.Join(",", ends.Select(value => value.X))}";
            var movedNearestTwo = ends[0].X < starts[0].X - 0.1f &&
                ends[1].X < starts[1].X - 0.1f;
            var leftThirdUntouched = System.Math.Abs(ends[2].X - starts[2].X) <= 0.001f;
            var activatedNearestTwo = activeCounts[0] == 1 && activeCounts[1] == 1 && activeCounts[2] == 0;
            Assert.That(movedNearestTwo && leftThirdUntouched && activatedNearestTwo, Is.True,
                "target_max_count=2 must pull the nearest two targets and leave the third untouched. " +
                positions + $"; active={string.Join(",", activeCounts)}");
        }

        [Test]
        public void PersistentAreaSkill_DelegatesLifetimeToAreaRuntime()
        {
            using var harness = CreateHarness(
                "persistent_area_trigger_headless_world",
                "moba-persistent-area-skill.trigger.json",
                MobaTriggerAuthoringTestIds.PersistentArea);
            var casterId = harness.AssertPlayerActorBound();
            var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
            var activeBefore = areas.ActiveCount;

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.PersistentArea,
                new ActorPayload(casterId)), Is.True);

            var active = new List<MobaAreaRuntimeInfo>();
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(areas.ActiveCount, Is.EqualTo(activeBefore + 1));
            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].Radius, Is.EqualTo(2.75f).Within(0.001f));
            Assert.That(active[0].OwnerActorId, Is.EqualTo(casterId));

            harness.Tick(22);

            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.False,
                "The Area Runtime must expire the eighteen-frame area without trigger-side scheduling.");
            Assert.That(areas.ActiveCount, Is.EqualTo(activeBefore));
        }

        [Test]
        public void OneShotArea_RejectsAllyThenDispatchesOnlyOneEnemyEnterInSameFrame()
        {
            using var harness = CreateHarness(
                "one_shot_area_enter_order_headless_world",
                "moba-persistent-area-skill.trigger.json",
                MobaTriggerAuthoringTestIds.PersistentArea);
            var casterId = harness.AssertPlayerActorBound();

            // The production test Area row is deliberately persistent. Toggle only the
            // runtime MO in this test so the system-level enter gate can be exercised
            // without adding a temporary production row or hero-specific branch.
            Assert.That(harness.Config.TryGetAoe(AreaTemplateId, out var area), Is.True);
            var consumeField = typeof(AbilityKit.Demo.Moba.Config.BattleDemo.MO.AoeMO)
                .GetField("<ConsumeOnFirstEnter>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(consumeField, Is.Not.Null, "AoeMO must retain the generated one-shot field.");
            consumeField.SetValue(area, true);

            var enterEvents = new List<AreaEventArgs>();
            var spawnEvents = new List<AreaEventArgs>();
            var delayEvents = new List<AreaEventArgs>();
            var eventBus = harness.World.Services.Resolve<IEventBus>();
            using var spawnSubscription = eventBus.Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.spawn")),
                payload => spawnEvents.Add(payload));
            using var delaySubscription = eventBus.Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.delay")),
                payload => delayEvents.Add(payload));
            using var subscription = eventBus.Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.enter")),
                payload => enterEvents.Add(payload));

            // An ally is already inside when the delayed Area arms. It must not claim
            // the one-shot slot before an enemy arrives.
            SpawnScenarioActor(harness, "area_ally", 9_901_001, 0f, 0f, teamId: 1);
            // Actor spawning is a world mutation; let the PreExecute collision sync
            // register the ally collider before the Area is created. Otherwise the
            // first Area query can race the actor registration and produce no Enter.
            harness.Tick(2);
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.PersistentArea,
                new ActorPayload(casterId)), Is.True);
            harness.Tick(10);
            Assert.That(enterEvents, Is.Empty, "An allied overlap must not dispatch or consume AreaEnter.");

            // Two enemies enter before the next world tick. The AreaEnter queue may
            // contain both, but the atomic consume gate must dispatch exactly one.
            var firstEnemy = SpawnScenarioActor(harness, "area_enemy_first", 9_901_002, 0f, 0f, teamId: 2);
            var secondEnemy = SpawnScenarioActor(harness, "area_enemy_second", 9_901_003, 0f, 0f, teamId: 2);
            harness.Tick(2);

            Assert.That(enterEvents, Has.Count.EqualTo(1),
                $"A one-shot Area must dispatch exactly one accepted enemy Enter for a same-frame queue. " +
                $"spawnEvents={spawnEvents.Count} delayEvents={delayEvents.Count}");
            Assert.That(new[] { firstEnemy, secondEnemy }, Does.Contain(enterEvents[0].TargetActorId));
            Assert.That(enterEvents[0].OwnerActorId, Is.EqualTo(casterId));
        }

        [Test]
        public void GroupedAreas_ClaimEachEnemyOnceAndRemainAvailableForDistinctTargets()
        {
            using var harness = CreateHarness(
                "grouped_area_target_claim_headless_world",
                "moba-grouped-persistent-area-skill.trigger.json",
                MobaTriggerAuthoringTestIds.GroupedPersistentArea);
            var casterId = harness.AssertPlayerActorBound();
            var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
            var enterEvents = new List<AreaEventArgs>();
            var eventBus = harness.World.Services.Resolve<IEventBus>();
            using var subscription = eventBus.Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.enter")),
                payload => enterEvents.Add(payload));

            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();
            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.GroupedPersistentArea,
                new ActorPayload(casterId)), Is.True,
                BuildPlanActionFailureMessage(harness));
            var active = new List<MobaAreaRuntimeInfo>();
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(active, Has.Count.EqualTo(2));
            Assert.That(active[0].ClaimTargetsWithinGroup, Is.True);
            Assert.That(active[1].ClaimTargetsWithinGroup, Is.True);

            harness.Tick(10);
            Assert.That(enterEvents, Is.Empty,
                "Both Areas should arm before the first enemy enters.");

            var firstEnemy = SpawnScenarioActor(harness, "group_enemy_first", 9_901_004, 0f, 0f, teamId: 2);
            harness.Tick(2);
            Assert.That(enterEvents.Count(entry => entry.TargetActorId == firstEnemy), Is.EqualTo(1),
                "The first enemy may be accepted by only one Area in the group.");
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(active, Has.Count.EqualTo(2),
                "Group target claims must not consume either non-one-shot Area.");

            var secondEnemy = SpawnScenarioActor(harness, "group_enemy_second", 9_901_005, 0f, 0f, teamId: 2);
            harness.Tick(2);
            Assert.That(enterEvents.Count(entry => entry.TargetActorId == secondEnemy), Is.EqualTo(1),
                "A surviving Area in the group must remain available for a different enemy.");
            Assert.That(enterEvents, Has.Count.EqualTo(2));
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(active, Has.Count.EqualTo(2));
        }

        [Test]
        public void JinxEPlaceholderContract_CreatesThreeAreasClaimsTargetsAndExpiresAsAGroup()
        {
            using var harness = CreateHarness(
                "jinx_e_three_area_placeholder_headless_world",
                "moba-jinx-e-three-area-acceptance.trigger.json",
                MobaTriggerAuthoringTestIds.JinxEThreeAreaAcceptance);
            var casterId = harness.AssertPlayerActorBound();
            var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
            var enterEvents = new List<AreaEventArgs>();
            var eventBus = harness.World.Services.Resolve<IEventBus>();
            using var subscription = eventBus.Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.enter")),
                payload => enterEvents.Add(payload));
            var effects = harness.World.Services.Resolve<MobaEffectExecutionService>();

            Assert.That(effects.ExecuteRulePlan(
                MobaTriggerAuthoringTestIds.JinxEThreeAreaAcceptance,
                new ActorPayload(casterId)), Is.True,
                BuildPlanActionFailureMessage(harness));

            var active = new List<MobaAreaRuntimeInfo>();
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(active, Has.Count.EqualTo(3), "E must create three independent Area entities.");
            Assert.That(active.All(area => area.ClaimTargetsWithinGroup), Is.True);
            Assert.That(active.Select(area => area.GroupKey).Distinct().ToArray(), Is.EqualTo(new[] { 1 }));

            harness.Tick(10);
            Assert.That(enterEvents, Is.Empty, "Areas must not enter-trigger before their delay/arming boundary.");

            var firstEnemy = SpawnScenarioActor(harness, "jinx_e_enemy_first", 9_901_006, 0f, 0f, teamId: 2);
            harness.Tick(2);
            Assert.That(enterEvents.Count(entry => entry.TargetActorId == firstEnemy), Is.EqualTo(1),
                "One cast's shared group claim must allow only one trap to affect the same enemy.");
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.True);
            Assert.That(active, Has.Count.EqualTo(3), "Persistent placeholder Areas must remain for later targets.");

            var secondEnemy = SpawnScenarioActor(harness, "jinx_e_enemy_second", 9_901_007, 0f, 0f, teamId: 2);
            harness.Tick(2);
            Assert.That(enterEvents.Count(entry => entry.TargetActorId == secondEnemy), Is.EqualTo(1),
                "A surviving trap must remain available for a distinct enemy.");
            Assert.That(enterEvents, Has.Count.EqualTo(2));

            harness.Tick(100);
            Assert.That(areas.TryGetAreas(active, casterId, AreaTemplateId), Is.False,
                "All three placeholder Areas must expire and release their group state.");
        }

        [Test]
        public void OverhealToShieldSkill_ExecutesThroughOwnerBoundHealEvent()
        {
            using var harness = CreateHarness(
                "overheal_to_shield_trigger_headless_world",
                "moba-overheal-to-shield-skill.trigger.json",
                MobaTriggerAuthoringTestIds.OverhealToShield);
            var casterId = harness.AssertPlayerActorBound();
            var ownerKey = (long)MobaTriggerAuthoringTestIds.OverhealToShield;
            var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
            var gates = harness.World.Services.Resolve<MobaOwnerBoundTriggerGateService>();
            var trace = harness.World.Services.Resolve<MobaTraceRegistry>();
            var ownerContextId = trace.CreateObservationRoot(
                MobaExecutionKind.SkillCast,
                MobaTriggerAuthoringTestIds.OverhealToShield,
                casterId,
                casterId);
            var gate = new TestOwnerBoundGate(
                ownerKey,
                MobaTriggerAuthoringTestIds.OverhealToShield,
                casterId,
                ownerContextId);
            gates.RegisterGate(gate);

            try
            {
                // The fixture is merged after world bootstrap, so refresh the subscription record cache.
                subscriptions.OnInit(harness.World.Services);
                subscriptions.ApplyTriggers(new[] { MobaTriggerAuthoringTestIds.OverhealToShield }, ownerKey);
                Assert.That(subscriptions.ContainsOwnerKey(ownerKey), Is.True);

                var damage = harness.World.Services.Resolve<MobaDamageService>();
                var shields = harness.World.Services.Resolve<MobaShieldService>();
                var fullHp = harness.GetActorHp(casterId);
                var shieldBefore = shields.GetTotalRemaining(casterId);
                var damageResult = damage.CommitDamage(casterId, casterId, damageType: 1, value: 30f);
                Assert.That(damageResult.AppliedValue, Is.EqualTo(30f).Within(0.001f));

                var healResult = damage.CommitHeal(casterId, casterId, healType: 1, value: 50f);

                Assert.That(healResult.AppliedValue, Is.EqualTo(30f).Within(0.001f));
                Assert.That(harness.GetActorHp(casterId), Is.EqualTo(fullHp).Within(0.001f));
                Assert.That(subscriptions.TryGetLastEvaluation(
                    ownerKey,
                    MobaTriggerAuthoringTestIds.OverhealToShield,
                    out var evaluation), Is.True,
                    "The boxed heal.apply.after event must reach the owner-bound subscription.");
                Assert.That(evaluation.GatePassed, Is.True);
                Assert.That(evaluation.SourceResolved, Is.True);
                Assert.That(evaluation.PlanPassed, Is.True,
                    "Owner matching and payload.overheal_value > 0 must pass before actions execute.");
                Assert.That(gate.CompletionCount, Is.EqualTo(1),
                    "The owner-bound trigger must complete its formal effect execution session. " +
                    BuildPlanActionFailureMessage(harness));
                Assert.That(shields.GetTotalRemaining(casterId),
                    Is.EqualTo(shieldBefore + 15f).Within(0.001f),
                    "The trigger formula is max(payload.overheal_value * 0.5 + 5, 1): overheal 20 produces shield 15. " +
                    BuildPlanActionFailureMessage(harness));
            }
            finally
            {
                subscriptions.Stop(ownerKey);
                gates.UnregisterGate(gate);
                trace.EndContext(ownerContextId, MobaExecutionEndReason.Completed);
            }
        }

        private static string BuildPlanActionFailureMessage(MobaSkillConfigTestHarness harness)
        {
            var diagnostics = harness.World.Services.Resolve<IMobaBattleDiagnosticsService>();
            return string.Join("\n", diagnostics.GetWarningsSnapshot()
                .Where(record => record.Key == MobaBattleDiagnosticMetric.PlanActionRejected
                    || record.Key == MobaBattleDiagnosticMetric.TriggerActionFailed)
                .Select(record => record.Message));
        }

        private static MobaSkillConfigTestHarness CreateHarness(
            string worldId,
            string fixtureFileName,
            int triggerId)
        {
            var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { ExistingSkillId },
                worldId: worldId,
                heroId: 1001,
                attributeTemplateId: 1001);
            MobaTriggerSkillFixtureCompiler.CompileAndMerge(harness, fixtureFileName, triggerId);
            harness.EnterGameAndWarmup(reason: "MOBA trigger authoring skill verification");
            return harness;
        }

        private static AttackInfo CreateAttack(int casterId, int targetId)
        {
            var attack = new AttackInfo
            {
                AttackerActorId = casterId,
                TargetActorId = targetId,
                DamageType = DamageType.Physical,
                ReasonKind = DamageReasonKind.Skill,
                ReasonParam = MobaTriggerAuthoringTestIds.HealthPercentDamage
            };
            attack.BaseDamage.BaseValue = 20f;
            return attack;
        }

        private static void SetActorHp(MobaSkillConfigTestHarness harness, int actorId, float hp)
        {
            var actor = harness.AssertActorEntity(actorId);
            Assert.That(actor.hasResourceContainer && actor.resourceContainer.Value != null, Is.True);
            Assert.That(actor.resourceContainer.Value.Map.TryGetValue(
                AbilityKit.Demo.Moba.Components.ResourceType.Hp, out var state), Is.True);
            state.Current = AbilityKit.Deterministic.Fixed64.FromSingle(hp);
        }

        private static OwnerTriggerScope BindOwnerTrigger(
            MobaSkillConfigTestHarness harness,
            int triggerId,
            int ownerActorId)
        {
            var ownerKey = (long)triggerId + ownerActorId;
            var trace = harness.World.Services.Resolve<MobaTraceRegistry>();
            var gates = harness.World.Services.Resolve<MobaOwnerBoundTriggerGateService>();
            var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
            var ownerContextId = trace.CreateObservationRoot(
                MobaExecutionKind.SkillCast, triggerId, ownerActorId, ownerActorId);
            var gate = new TestOwnerBoundGate(ownerKey, triggerId, ownerActorId, ownerContextId);
            gates.RegisterGate(gate);
            subscriptions.OnInit(harness.World.Services);
            subscriptions.ApplyTriggers(new[] { triggerId }, ownerKey);
            Assert.That(subscriptions.ContainsOwnerKey(ownerKey), Is.True);
            return new OwnerTriggerScope(ownerKey, gate, gates, subscriptions, trace, ownerContextId);
        }

        private static int SpawnEnemy(
            MobaSkillConfigTestHarness harness,
            string alias,
            int actorId,
            float x,
            float z)
        {
            return harness.SpawnScenarioActor(
                alias,
                actorId,
                kind: "Hero",
                teamId: 2,
                heroId: 1002,
                attributeTemplateId: 1002,
                level: 1,
                unitSubType: (int)UnitSubType.Hero,
                mainType: (int)EntityMainType.Unit,
                ownerPlayerId: alias + "_player",
                ownerActorId: 0,
                sourceKind: "Hero",
                sourceId: 1002,
                position: new MobaAcceptanceVector3Expectation { x = x, y = 0f, z = z });
        }

        private static int SpawnScenarioActor(
            MobaSkillConfigTestHarness harness,
            string alias,
            int actorId,
            float x,
            float z,
            int teamId)
        {
            return harness.SpawnScenarioActor(
                alias,
                actorId,
                kind: "Hero",
                teamId: teamId,
                heroId: 1002,
                attributeTemplateId: 1002,
                level: 1,
                unitSubType: (int)UnitSubType.Hero,
                mainType: (int)EntityMainType.Unit,
                ownerPlayerId: alias + "_player",
                ownerActorId: 0,
                sourceKind: "Hero",
                sourceId: 1002,
                position: new MobaAcceptanceVector3Expectation { x = x, y = 0f, z = z });
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSpawn(
            MobaSkillConfigTestHarness harness,
            int templateId,
            int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSpawn(harness, templateId, out var entry)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile spawn snapshot missing for template {templateId} within {maxTicks} ticks.");
            return default;
        }

        private static IEnumerable<TriggerNodeData> EnumerateNodes(TriggerNodeData node)
        {
            if (node == null) yield break;
            yield return node;
            foreach (var child in node.Children)
                foreach (var descendant in EnumerateNodes(child))
                    yield return descendant;
            foreach (var child in node.ElseChildren)
                foreach (var descendant in EnumerateNodes(child))
                    yield return descendant;
        }

        private static bool HasAddBuffDuration(TriggerNodeData node)
        {
            return string.Equals(node.Type, "add_buff", System.StringComparison.OrdinalIgnoreCase) &&
                   node.Arguments.Any(argument =>
                       string.Equals(argument.Name, "duration_ms", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryCollectProjectileSpawn(
            MobaSkillConfigTestHarness harness,
            int templateId,
            out MobaProjectileEventSnapshotEntry entry)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            {
                if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload);
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    if (entries[entryIndex].Kind != (int)ProjectileEventKind.Spawn ||
                        entries[entryIndex].TemplateId != templateId) continue;
                    entry = entries[entryIndex];
                    return true;
                }
            }

            return false;
        }

        private static Vec3 TickUntilPositionXGreaterThan(
            MobaSkillConfigTestHarness harness,
            int actorId,
            float minimumX,
            int maxTicks)
        {
            var position = Vec3.Zero;
            for (var i = 0; i <= maxTicks; i++)
            {
                var actor = harness.AssertActorEntity(actorId);
                if (actor.hasTransform)
                {
                    position = actor.transform.Value.Position;
                    if (position.X > minimumX) return position;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            return position;
        }

        public readonly struct FixtureCase
        {
            public FixtureCase(string fileName, int triggerId)
            {
                FileName = fileName;
                TriggerId = triggerId;
            }

            public string FileName { get; }
            public int TriggerId { get; }

            public override string ToString()
            {
                return $"{TriggerId}:{FileName}";
            }
        }

        private sealed class ActorPayload : IMobaActorContextProvider
        {
            private readonly int _sourceActorId;

            public ActorPayload(int sourceActorId)
            {
                _sourceActorId = sourceActorId;
            }

            public bool TryGetSourceActorId(out int actorId)
            {
                actorId = _sourceActorId;
                return actorId > 0;
            }

            public bool TryGetTargetActorId(out int actorId)
            {
                actorId = 0;
                return false;
            }
        }

        private sealed class TestOwnerBoundGate :
            IMobaOwnerBoundTriggerGate,
            IMobaOwnerBoundTriggerExecutionSourceProvider
        {
            private readonly long _ownerKey;
            private readonly int _triggerId;
            private readonly int _actorId;
            private readonly long _ownerContextId;

            public TestOwnerBoundGate(long ownerKey, int triggerId, int actorId, long ownerContextId)
            {
                _ownerKey = ownerKey;
                _triggerId = triggerId;
                _actorId = actorId;
                _ownerContextId = ownerContextId;
            }

            public int CompletionCount { get; private set; }

            public long OwnerKey => _ownerKey;

            public bool IsMatch(long ownerKey, int triggerId)
            {
                return ownerKey == _ownerKey && triggerId == _triggerId;
            }

            public bool CanExecute(long ownerKey, int triggerId)
            {
                return IsMatch(ownerKey, triggerId);
            }

            public void Complete(long ownerKey, int triggerId)
            {
                if (IsMatch(ownerKey, triggerId)) CompletionCount++;
            }

            public bool TryGetExecutionSource(
                long ownerKey,
                int triggerId,
                out MobaOwnerBoundTriggerExecutionSource source)
            {
                if (!IsMatch(ownerKey, triggerId))
                {
                    source = default;
                    return false;
                }

                source = new MobaOwnerBoundTriggerExecutionSource(
                    _actorId,
                    _actorId,
                    sourceContextId: _ownerContextId,
                    rootContextId: _ownerContextId,
                    ownerContextId: _ownerContextId,
                    sourceConfigId: _triggerId);
                return true;
            }
        }

        private sealed class OwnerTriggerScope : IDisposable
        {
            private readonly TestOwnerBoundGate _gate;
            private readonly MobaOwnerBoundTriggerGateService _gates;
            private readonly MobaTriggerPlanSubscriptionService _subscriptions;
            private readonly MobaTraceRegistry _trace;
            private readonly long _ownerContextId;
            private bool _disposed;

            public OwnerTriggerScope(
                long ownerKey,
                TestOwnerBoundGate gate,
                MobaOwnerBoundTriggerGateService gates,
                MobaTriggerPlanSubscriptionService subscriptions,
                MobaTraceRegistry trace,
                long ownerContextId)
            {
                OwnerKey = ownerKey;
                _gate = gate;
                _gates = gates;
                _subscriptions = subscriptions;
                _trace = trace;
                _ownerContextId = ownerContextId;
            }

            public long OwnerKey { get; }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _subscriptions.Stop(OwnerKey);
                _gates.UnregisterGate(_gate);
                _trace.EndContext(_ownerContextId, MobaExecutionEndReason.Completed);
            }
        }
    }
}
#endif
