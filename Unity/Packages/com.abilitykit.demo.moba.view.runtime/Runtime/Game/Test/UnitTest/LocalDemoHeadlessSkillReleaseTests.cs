using System.Collections;
using System.Reflection;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class LocalDemoHeadlessSkillReleaseTests
    {
        private const float FixedDeltaTime = 1f / 30f;

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
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 240, "Battle flow did not reach InMatch.");

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
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 240, "Battle flow did not reach InMatch.");

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
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 240, "Battle flow did not reach InMatch.");

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
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 240, "Battle flow did not reach InMatch.");

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
            yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 240, "Battle flow did not reach InMatch.");

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
