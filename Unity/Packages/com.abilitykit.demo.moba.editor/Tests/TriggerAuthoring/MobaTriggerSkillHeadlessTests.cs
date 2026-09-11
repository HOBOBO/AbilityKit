#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Triggering;
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

        private static readonly FixtureCase[] FixtureCases =
        {
            new FixtureCase(
                "moba-target-collection-skill.trigger.json",
                MobaTriggerAuthoringTestIds.TargetCollectionDamage),
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
        public void ReservedTriggerIds_DoNotOverlapFormalAuthoringSources()
        {
            var sourceDirectory = Path.Combine(
                UnityEngine.Application.dataPath,
                "AbilityKit",
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
                "AbilityKit",
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
            var ownerContextId = trace.CreateRootContext(
                MobaTraceKind.SkillCast,
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
                trace.EndContext(ownerContextId, TraceLifecycleReason.Completed);
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
    }
}
#endif
