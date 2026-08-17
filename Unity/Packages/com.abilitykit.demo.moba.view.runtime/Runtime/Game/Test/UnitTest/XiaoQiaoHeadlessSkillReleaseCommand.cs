using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    [InitializeOnLoad]
    public static class XiaoQiaoHeadlessSkillReleaseCommand
    {
        private const string RunningKey = "AbilityKit.XiaoQiaoHeadlessSkillRelease.Running";
        private const string ResultPathKey = "AbilityKit.XiaoQiaoHeadlessSkillRelease.ResultPath";
        private const float FixedDeltaTime = 1f / 30f;
        private const int MaxTicks = 1200;
        private const float TestHp = 5000f;
        private const float DamageEpsilon = 0.01f;
        private const float KnockupHeightEpsilon = 0.05f;
        private const float LandingHeightEpsilon = 0.03f;

        private static readonly List<int> s_actorIds = new List<int>(16);
        private static readonly List<string> s_scenarioResults = new List<string>(4);
        private static readonly SkillScenario[] s_scenarios =
        {
            new SkillScenario(
                name: "skill1.projectile.return.aim",
                slot: 1,
                aimDx: 12f,
                aimDz: 0f,
                enemyOffsetX: 4f,
                enemyOffsetZ: 0f,
                minObserveTicks: 12,
                maxObserveTicks: 180,
                requiredTraces: new[]
                {
                    new TraceRequirement(MobaTraceKind.ProjectileLaunch, 30020101),
                    new TraceRequirement(MobaTraceKind.DamageApply, 10020101),
                },
                requireDamage: true,
                requireKnockup: false,
                requiredActorBuffId: 10020001,
                minActorMoveSpeed: 6.9f),
            new SkillScenario(
                name: "skill2.target.area.knockup.aim",
                slot: 2,
                aimDx: 4f,
                aimDz: 0f,
                enemyOffsetX: 4f,
                enemyOffsetZ: 0f,
                minObserveTicks: 24,
                maxObserveTicks: 240,
                requiredTraces: new[]
                {
                    new TraceRequirement(MobaTraceKind.AreaSpawn, 40020201),
                    new TraceRequirement(MobaTraceKind.DamageApply, 10020201),
                },
                requireDamage: true,
                requireKnockup: true),
            new SkillScenario(
                name: "skill3.self.follow.vfx.passive",
                slot: 3,
                aimDx: 0f,
                aimDz: 0f,
                enemyOffsetX: 3f,
                enemyOffsetZ: 0f,
                minObserveTicks: 10,
                maxObserveTicks: 180,
                requiredTraces: new[]
                {
                    new TraceRequirement(MobaTraceKind.BuffApply, 10020301),
                },
                requireDamage: false,
                requireKnockup: false,
                requiredActorBuffId: 10020301,
                requiredPresentationVfxId: 90002003),
        };

        private static bool _battleRequested;
        private static int _ticks;
        private static int _lastPeriodicDiagnosticTick;
        private static string _lastStage;
        private static Vector3 _startPosition;
        private static bool _hasStartPosition;
        private static int _enemyActorId;
        private static int _scenarioIndex;
        private static ScenarioPhase _scenarioPhase;
        private static int _scenarioStartTick;
        private static int _scenarioSubmitTick;
        private static double _scenarioSubmitLogicTime;
        private static float _enemyStartHp;
        private static Vector3 _enemyStartPosition;
        private static float _lastEnemyHp;
        private static Vector3 _lastEnemyPosition;
        private static bool _knockupObserved;
        private static float _observedKnockupHeight;
        private static bool _observedActorBuff;
        private static float _maxActorMoveSpeed;
        private static float _lastObservedDamage;
        private static int _firstDamageTick;
        private static double _firstDamageSeconds;
        private static TraceBaseline _traceBaseline;

        static XiaoQiaoHeadlessSkillReleaseCommand()
        {
            EditorApplication.update -= ContinueInPlayMode;
            EditorApplication.update += ContinueInPlayMode;
        }

        public static void Run()
        {
            var resultPath = GetArgValue("-xiaoQiaoSkillResult");
            if (string.IsNullOrWhiteSpace(resultPath))
            {
                resultPath = Path.GetFullPath("../UnityXiaoQiaoHeadlessSkillReleaseCommand.xml");
            }

            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(ResultPathKey, resultPath);
            ResetState();

            try
            {
                DemoGameplayTestLauncher.OpenMobaLocalForPlay();

                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex)
            {
                Finish(false, "EXCEPTION: " + ex);
            }
        }

        private static void ContinueInPlayMode()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

            try
            {
                var result = TickScenario();
                if (result != null)
                {
                    Finish(true, result);
                }
            }
            catch (Exception ex)
            {
                Finish(false, "EXCEPTION: " + ex);
            }
        }

        private static string TickScenario()
        {
            _ticks++;
            if (_ticks > MaxTicks) throw new TimeoutException("Xiao Qiao headless skill release validation timed out. " + BuildDiagnostic(null, null));

            var entry = Object.FindObjectOfType<GameEntry>();
            if (entry == null)
            {
                Stage("waitingForEntry", null, null);
                return null;
            }

            if (!GameEntry.IsInitialized)
            {
                Stage("waitingForGameEntryInitialized", null, null);
                return null;
            }

            var flow = entry.Get<GameFlowDomain>();
            if (flow == null)
            {
                Stage("waitingForFlow", null, null);
                return null;
            }

            FlowTick(flow, 1);

            if (!_battleRequested)
            {
                Stage("requestingBattle", flow, null);
                flow.EnterBattle(new TestBattleBootstrapper());
                _battleRequested = true;
                Stage("battleRequested", flow, null);
                return null;
            }

            if (flow.CurrentBattlePhase != MobaBattleState.InMatch)
            {
                Stage("waitingForInMatch", flow, null);
                return null;
            }

            if (!entry.TryGet(out BattleContext ctx))
            {
                Stage("waitingForBattleContext", flow, null);
                return null;
            }

            if (!EnsureLocalActorReady(ctx))
            {
                Stage("waitingForLocalActorReady", flow, ctx);
                return null;
            }

            if (!_hasStartPosition)
            {
                if (!TryGetLocalActorPosition(ctx, out _startPosition)) return null;
                _hasStartPosition = true;
                Stage("startPositionCaptured", flow, ctx);
            }

            if (_enemyActorId <= 0)
            {
                if (!TryFindEnemyActor(ctx, out _enemyActorId))
                {
                    throw new InvalidOperationException("No enemy actor with transform and attrs was found for Xiao Qiao headless skill validation.");
                }
            }

            if (_scenarioIndex >= s_scenarios.Length)
            {
                return "PASS: Xiao Qiao headless skill validation completed. " + string.Join(" | ", s_scenarioResults);
            }

            var scenario = s_scenarios[_scenarioIndex];
            switch (_scenarioPhase)
            {
                case ScenarioPhase.Prepare:
                    PrepareScenario(ctx, scenario);
                    _scenarioStartTick = _ticks;
                    _scenarioPhase = ScenarioPhase.Submit;
                    Stage("prepared." + scenario.Name, flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case ScenarioPhase.Submit:
                    Stage("submitting." + scenario.Name, flow, ctx);
                    _traceBaseline = CaptureTraceBaseline(ctx, scenario.RequiredTraces);
                    ctx.SubmitHudSkillAim(scenario.Slot, scenario.AimDx, scenario.AimDz);
                    _scenarioSubmitTick = _ticks;
                    _scenarioSubmitLogicTime = ctx.LogicTimeSeconds;
                    _scenarioPhase = ScenarioPhase.Observe;
                    Stage("submitted." + scenario.Name, flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case ScenarioPhase.Observe:
                    return ObserveScenario(flow, ctx, scenario);

                default:
                    throw new InvalidOperationException($"Unsupported scenario phase: {_scenarioPhase}");
            }
        }

        private static string ObserveScenario(GameFlowDomain flow, BattleContext ctx, SkillScenario scenario)
        {
            if (!TryGetEnemyRuntimeState(ctx, _enemyActorId, out var enemyHp, out var enemyPosition))
            {
                throw new InvalidOperationException($"Prepared enemy actor disappeared. enemyActorId={_enemyActorId}");
            }

            _lastEnemyHp = enemyHp;
            _lastEnemyPosition = enemyPosition;
            var damage = _enemyStartHp - enemyHp;
            var enemyHeightDelta = enemyPosition.y - _enemyStartPosition.y;
            if (enemyHeightDelta > _observedKnockupHeight) _observedKnockupHeight = enemyHeightDelta;
            if (enemyHeightDelta > KnockupHeightEpsilon) _knockupObserved = true;

            var ticksSinceSubmit = _ticks - _scenarioSubmitTick;
            var secondsSinceSubmit = ctx.LogicTimeSeconds - _scenarioSubmitLogicTime;
            if (damage > _lastObservedDamage + DamageEpsilon)
            {
                _lastObservedDamage = damage;
                if (_firstDamageTick <= 0)
                {
                    _firstDamageTick = ticksSinceSubmit;
                    _firstDamageSeconds = secondsSinceSubmit;
                }
            }

            var observedEnoughTicks = ticksSinceSubmit >= scenario.MinObserveTicks;
            var damageOk = !scenario.RequireDamage || damage > DamageEpsilon;
            var landed = Math.Abs(enemyHeightDelta) <= LandingHeightEpsilon;
            var knockupOk = !scenario.RequireKnockup || (_knockupObserved && landed);
            var tracesOk = HasNewRequiredTraces(ctx, scenario.RequiredTraces, _traceBaseline);
            var actorHasBuff = scenario.RequiredActorBuffId <= 0 || HasActorBuff(ctx, ctx.LocalActorId, scenario.RequiredActorBuffId);
            if (actorHasBuff) _observedActorBuff = true;

            var moveSpeed = GetActorMoveSpeed(ctx, ctx.LocalActorId);
            if (moveSpeed > _maxActorMoveSpeed) _maxActorMoveSpeed = moveSpeed;

            var actorBuffOk = scenario.RequiredActorBuffId <= 0 || _observedActorBuff;
            var moveSpeedOk = scenario.MinActorMoveSpeed <= 0f || _maxActorMoveSpeed >= scenario.MinActorMoveSpeed;
            var presentationCueOk = scenario.RequiredPresentationVfxId <= 0 || HasActivePresentationCue(ctx, scenario.RequiredPresentationVfxId, ctx.LocalActorId);

            if (observedEnoughTicks && damageOk && knockupOk && tracesOk && actorBuffOk && moveSpeedOk && presentationCueOk)
            {
                var result = $"{scenario.Name}: damage={damage:F3}, firstDamageTick={_firstDamageTick}, firstDamageTime={_firstDamageSeconds:F3}, maxEnemyHeightDelta={_observedKnockupHeight:F3}, landedHeightDelta={enemyHeightDelta:F3}, moveSpeed={moveSpeed:F3}, maxMoveSpeed={_maxActorMoveSpeed:F3}, buffOk={actorBuffOk}, presentationCueOk={presentationCueOk}, traces={DescribeRequiredTraces(ctx, scenario.RequiredTraces, _traceBaseline)}";
                s_scenarioResults.Add(result);
                _scenarioIndex++;
                _scenarioPhase = ScenarioPhase.Prepare;
                Stage("passed." + scenario.Name, flow, ctx);
                FlowTick(flow, 4);
                return null;
            }

            if (_ticks - _scenarioStartTick > scenario.MaxObserveTicks)
            {
                throw new TimeoutException($"{scenario.Name} validation timed out. damage={damage:F3}, damageOk={damageOk}, knockupOk={knockupOk}, tracesOk={tracesOk}, actorBuffOk={actorBuffOk}, moveSpeed={moveSpeed:F3}, maxMoveSpeed={_maxActorMoveSpeed:F3}, moveSpeedOk={moveSpeedOk}, presentationCueOk={presentationCueOk}, maxEnemyHeightDelta={_observedKnockupHeight:F3}, landed={landed}, traces={DescribeRequiredTraces(ctx, scenario.RequiredTraces, _traceBaseline)}, " + BuildDiagnostic(flow, ctx));
            }

            Stage("observing." + scenario.Name, flow, ctx);
            return null;
        }

        private static void PrepareScenario(BattleContext ctx, SkillScenario scenario)
        {
            var casterPosition = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
            if (!TrySetActorPosition(ctx, ctx.LocalActorId, casterPosition))
            {
                throw new InvalidOperationException($"Failed to reset local actor position. actorId={ctx.LocalActorId}, position={casterPosition}");
            }

            var targetPosition = new Vector3(casterPosition.x + scenario.EnemyOffsetX, casterPosition.y, casterPosition.z + scenario.EnemyOffsetZ);
            if (!TrySetActorPosition(ctx, _enemyActorId, targetPosition))
            {
                throw new InvalidOperationException($"Failed to place enemy actor for {scenario.Name}. enemyActorId={_enemyActorId}, targetPosition={targetPosition}");
            }

            if (!TrySetActorHp(ctx, _enemyActorId, TestHp))
            {
                throw new InvalidOperationException($"Failed to reset enemy hp for {scenario.Name}. enemyActorId={_enemyActorId}");
            }

            ResetActiveSkillCooldowns(ctx, ctx.LocalActorId, cancelRunningSkill: true);

            if (!TryGetEnemyRuntimeState(ctx, _enemyActorId, out _enemyStartHp, out _enemyStartPosition))
            {
                throw new InvalidOperationException($"Failed to capture enemy baseline after placement. enemyActorId={_enemyActorId}");
            }

            _lastEnemyHp = _enemyStartHp;
            _lastEnemyPosition = _enemyStartPosition;
            _knockupObserved = false;
            _observedKnockupHeight = 0f;
            _observedActorBuff = false;
            _maxActorMoveSpeed = GetActorMoveSpeed(ctx, ctx.LocalActorId);
            _lastObservedDamage = 0f;
            _firstDamageTick = 0;
            _firstDamageSeconds = 0d;
            _traceBaseline = default;
        }

        private static TraceBaseline CaptureTraceBaseline(BattleContext ctx, TraceRequirement[] requirements)
        {
            var baseline = new TraceBaseline(requirements != null ? requirements.Length : 0);
            if (!TryGetTraceRegistry(ctx, out var traces) || requirements == null) return baseline;

            for (var i = 0; i < requirements.Length; i++)
            {
                baseline.Counts[i] = CountTraceNodes(traces, requirements[i]);
            }

            return baseline;
        }

        private static bool HasNewRequiredTraces(BattleContext ctx, TraceRequirement[] requirements, TraceBaseline baseline)
        {
            if (requirements == null || requirements.Length == 0) return true;
            if (!TryGetTraceRegistry(ctx, out var traces)) return false;
            if (baseline.Counts == null || baseline.Counts.Length != requirements.Length) return false;

            for (var i = 0; i < requirements.Length; i++)
            {
                if (CountTraceNodes(traces, requirements[i]) <= baseline.Counts[i]) return false;
            }

            return true;
        }

        private static int CountTraceNodes(MobaTraceRegistry traces, TraceRequirement requirement)
        {
            var count = 0;
            foreach (var node in traces.GetNodesByKind((int)requirement.Kind))
            {
                var metadata = node.Metadata;
                if (metadata == null || metadata.ConfigId != requirement.ConfigId) continue;
                count++;
            }

            return count;
        }

        private static string DescribeRequiredTraces(BattleContext ctx, TraceRequirement[] requirements, TraceBaseline baseline)
        {
            if (requirements == null || requirements.Length == 0) return "none";
            if (!TryGetTraceRegistry(ctx, out var traces)) return "traceRegistryUnavailable";

            var parts = new string[requirements.Length];
            for (var i = 0; i < requirements.Length; i++)
            {
                var current = CountTraceNodes(traces, requirements[i]);
                var before = baseline.Counts != null && i < baseline.Counts.Length ? baseline.Counts[i] : -1;
                parts[i] = $"{requirements[i].Kind}:{requirements[i].ConfigId} {before}->{current}";
            }

            return string.Join(", ", parts);
        }

        private static bool TryGetTraceRegistry(BattleContext ctx, out MobaTraceRegistry traces)
        {
            traces = null;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            return world.Services.TryResolve(out traces) && traces != null;
        }

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                flow.Tick(FixedDeltaTime);
            }
        }

        private static bool EnsureLocalActorReady(BattleContext ctx)
        {
            if (ctx == null || ctx.Session == null || ctx.EntityQuery == null) return false;
            if (ctx.LocalActorId <= 0 && TryResolveLocalActorId(ctx, out var actorId))
            {
                ctx.LocalActorId = actorId;
                Stage("localActorResolved", null, ctx);
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

            entities.GetRegisteredActorIds(s_actorIds);
            for (var i = 0; i < s_actorIds.Count; i++)
            {
                var actorId = s_actorIds[i];
                if (actorId <= 0 || actorId == ctx.LocalActorId) continue;
                if (!entities.TryGetActorEntity(actorId, out var candidate) || candidate == null) continue;
                if (!candidate.hasTransform || !candidate.hasAttributeGroup) continue;
                if (local.hasTeam && candidate.hasTeam && local.team.Value.Equals(candidate.team.Value)) continue;

                enemyActorId = actorId;
                return true;
            }

            return false;
        }

        private static bool TryGetEnemyRuntimeState(BattleContext ctx, int actorId, out float hp, out Vector3 position)
        {
            hp = 0f;
            position = default;
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasTransform || !entity.hasAttributeGroup) return false;

            hp = entity.GetMobaAttrs().Hp;
            position = ToUnityVector(entity.transform.Value.Position);
            return true;
        }

        private static bool TrySetActorPosition(BattleContext ctx, int actorId, Vector3 position)
        {
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasTransform) return false;

            var current = entity.transform.Value;
            var newPosition = ToVec3(position);
            entity.ReplaceTransform(new Transform3(in newPosition, in current.Rotation, in current.Scale));
            if (entity.hasMotion)
            {
                var motion = entity.motion;
                var state = new MotionState(in newPosition)
                {
                    Forward = current.Forward,
                    Velocity = Vec3.Zero,
                    Time = motion.State.Time
                };
                entity.ReplaceMotion(
                    motion.Pipeline,
                    state,
                    motion.Output,
                    motion.Solver,
                    motion.Policy,
                    motion.Events,
                    motion.Initialized,
                    motion.HitTriggerRuntime);
            }

            return true;
        }

        private static bool TrySetActorHp(BattleContext ctx, int actorId, float hp)
        {
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasAttributeGroup || !entity.hasResourceContainer) return false;

            var attrs = entity.GetMobaAttrs();
            attrs.SetBase(BattleAttributeType.MAX_HP, hp);
            attrs.Hp = hp;
            return true;
        }

        private static float GetActorMoveSpeed(BattleContext ctx, int actorId)
        {
            if (!TryGetEntityManager(ctx, out var entities)) return 0f;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasAttributeGroup) return 0f;

            return entity.GetMobaAttrs().MoveSpeed;
        }

        private static bool HasActorBuff(BattleContext ctx, int actorId, int buffId)
        {
            if (buffId <= 0) return true;
            if (!TryGetEntityManager(ctx, out var entities)) return false;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasBuffs || entity.buffs.Active == null) return false;

            foreach (var runtime in entity.buffs.Active)
            {
                if (runtime != null && runtime.BuffId == buffId) return true;
            }

            return false;
        }

        private static bool HasActivePresentationCue(BattleContext ctx, int vfxId, int targetActorId)
        {
            if (vfxId <= 0) return true;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            if (!world.Services.TryResolve<MobaPresentationCueSnapshotService>(out var cues) || cues == null) return false;

            foreach (var active in cues.ActiveCues.Values)
            {
                var entry = active.Entry;
                if (entry.VfxId != vfxId) continue;
                if (targetActorId <= 0 || entry.TargetActorId == targetActorId || ContainsTarget(entry.Targets, targetActorId)) return true;
            }

            return false;
        }

        private static bool ContainsTarget(int[] targets, int targetActorId)
        {
            if (targets == null) return false;
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == targetActorId) return true;
            }

            return false;
        }

        private static void ResetActiveSkillCooldowns(BattleContext ctx, int actorId, bool cancelRunningSkill)
        {
            if (!TryGetEntityManager(ctx, out var entities)) return;
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null || !entity.hasSkillLoadout) return;

            var skills = entity.skillLoadout.ActiveSkills;
            if (skills != null)
            {
                for (var i = 0; i < skills.Length; i++)
                {
                    if (skills[i] != null) skills[i].CooldownEndTimeMs = 0L;
                }
            }

            if (cancelRunningSkill && ctx.Session != null && ctx.Session.TryGetWorld(out var world) && world?.Services != null && world.Services.TryResolve<SkillCastCoordinator>(out var coordinator) && coordinator != null)
            {
                coordinator.CancelAll(actorId);
            }
        }

        private static bool TryGetEntityManager(BattleContext ctx, out MobaEntityManager entities)
        {
            entities = null;
            if (ctx?.Session == null) return false;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return false;
            return world.Services.TryResolve(out entities) && entities != null;
        }

        private static Vector3 ToUnityVector(Vec3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Vec3 ToVec3(Vector3 value)
        {
            return new Vec3(value.x, value.y, value.z);
        }

        private static string DescribeActorMotion(BattleContext ctx, int actorId)
        {
            if (actorId <= 0) return "n/a";
            if (!TryGetEntityManager(ctx, out var entities)) return "entitiesUnavailable";
            if (!entities.TryGetActorEntity(actorId, out var entity) || entity == null) return "actorUnavailable";
            if (!entity.hasMotion) return "noMotion";

            var motion = entity.motion;
            return $"initialized={motion.Initialized}, sources={(motion.Pipeline != null ? motion.Pipeline.SourceCount : -1)}, statePos={ToUnityVector(motion.State.Position)}, velocity={ToUnityVector(motion.State.Velocity)}, hitTriggerValid={motion.HitTriggerRuntime.IsValid}, hitTrigger={motion.HitTriggerRuntime.TriggerId}";
        }

        private static void Stage(string stage, GameFlowDomain flow, BattleContext ctx)
        {
            var changed = !string.Equals(_lastStage, stage, StringComparison.Ordinal);
            var periodic = _ticks - _lastPeriodicDiagnosticTick >= 60;
            if (!changed && !periodic) return;

            _lastStage = stage;
            _lastPeriodicDiagnosticTick = _ticks;
            Debug.Log("[XiaoQiaoHeadlessSkillReleaseCommand] " + BuildDiagnostic(flow, ctx));
        }

        private static string BuildDiagnostic(GameFlowDomain flow, BattleContext ctx)
        {
            var currentPosition = "n/a";
            if (ctx != null && TryGetLocalActorPosition(ctx, out var position))
            {
                currentPosition = position.ToString();
            }

            var scenario = _scenarioIndex >= 0 && _scenarioIndex < s_scenarios.Length ? s_scenarios[_scenarioIndex] : default;
            var scenarioName = _scenarioIndex >= 0 && _scenarioIndex < s_scenarios.Length ? scenario.Name : "complete";
            var enemyText = _enemyActorId > 0
                ? $", enemyActorId={_enemyActorId}, enemyStartHp={_enemyStartHp:F3}, lastEnemyHp={_lastEnemyHp:F3}, enemyStart={_enemyStartPosition}, lastEnemy={_lastEnemyPosition}, enemyHeightDelta={(_lastEnemyPosition.y - _enemyStartPosition.y):F3}, observedKnockupHeight={_observedKnockupHeight:F3}, knockupObserved={_knockupObserved}, observedActorBuff={_observedActorBuff}, maxActorMoveSpeed={_maxActorMoveSpeed:F3}, firstDamageTick={_firstDamageTick}, motion={DescribeActorMotion(ctx, ctx != null ? ctx.LocalActorId : 0)}, enemyMotion={DescribeActorMotion(ctx, _enemyActorId)}, traces={DescribeRequiredTraces(ctx, scenario.RequiredTraces, _traceBaseline)}"
                : string.Empty;

            return $"stage={_lastStage ?? "notStarted"}, ticks={_ticks}, battleRequested={_battleRequested}, scenario={scenarioName}, scenarioIndex={_scenarioIndex}, scenarioPhase={_scenarioPhase}, hasStartPosition={_hasStartPosition}, battlePhase={(flow != null ? flow.CurrentBattlePhase.ToString() : "n/a")}, hasContext={ctx != null}, hasSession={(ctx != null && ctx.Session != null)}, localActorId={(ctx != null ? ctx.LocalActorId : 0)}, planPlayerId={(ctx != null ? ctx.Plan.World.PlayerId : "n/a")}, lastFrame={(ctx != null ? ctx.LastFrame : 0)}, logicTime={(ctx != null ? ctx.LogicTimeSeconds : 0f):F3}, start={(_hasStartPosition ? _startPosition.ToString() : "n/a")}, current={currentPosition}{enemyText}";
        }

        private static void Finish(bool success, string message)
        {
            var resultPath = SessionState.GetString(ResultPathKey, Path.GetFullPath("../UnityXiaoQiaoHeadlessSkillReleaseCommand.xml"));
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(ResultPathKey);
            ResetState();

            WriteResult(resultPath, success, message);
            Debug.Log("[XiaoQiaoHeadlessSkillReleaseCommand] " + message);

            EditorApplication.update -= ContinueInPlayMode;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void ResetState()
        {
            _battleRequested = false;
            _ticks = 0;
            _lastPeriodicDiagnosticTick = 0;
            _lastStage = null;
            _startPosition = default;
            _hasStartPosition = false;
            _enemyActorId = 0;
            _scenarioIndex = 0;
            _scenarioPhase = ScenarioPhase.Prepare;
            _scenarioStartTick = 0;
            _scenarioSubmitTick = 0;
            _scenarioSubmitLogicTime = 0d;
            _enemyStartHp = 0f;
            _enemyStartPosition = default;
            _lastEnemyHp = 0f;
            _lastEnemyPosition = default;
            _knockupObserved = false;
            _observedKnockupHeight = 0f;
            _observedActorBuff = false;
            _maxActorMoveSpeed = 0f;
            _lastObservedDamage = 0f;
            _firstDamageTick = 0;
            _firstDamageSeconds = 0d;
            _traceBaseline = default;
            s_scenarioResults.Clear();
            s_actorIds.Clear();
        }

        private static string GetArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }

            return null;
        }

        private static void WriteResult(string path, bool success, string message)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var encodedMessage = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message ?? string.Empty));
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                $"<headlessSkillRelease success=\"{success.ToString().ToLowerInvariant()}\">\n" +
                $"  <message encoding=\"base64\">{encodedMessage}</message>\n" +
                "</headlessSkillRelease>\n");
        }

        private enum ScenarioPhase
        {
            Prepare,
            Submit,
            Observe
        }

        private readonly struct TraceRequirement
        {
            public TraceRequirement(MobaTraceKind kind, int configId)
            {
                Kind = kind;
                ConfigId = configId;
            }

            public MobaTraceKind Kind { get; }
            public int ConfigId { get; }
        }

        private readonly struct TraceBaseline
        {
            public TraceBaseline(int count)
            {
                Counts = new int[count];
            }

            public int[] Counts { get; }
        }

        private readonly struct SkillScenario
        {
            public SkillScenario(string name, int slot, float aimDx, float aimDz, float enemyOffsetX, float enemyOffsetZ, int minObserveTicks, int maxObserveTicks, TraceRequirement[] requiredTraces, bool requireDamage, bool requireKnockup, int requiredActorBuffId = 0, float minActorMoveSpeed = 0f, int requiredPresentationVfxId = 0)
            {
                Name = name;
                Slot = slot;
                AimDx = aimDx;
                AimDz = aimDz;
                EnemyOffsetX = enemyOffsetX;
                EnemyOffsetZ = enemyOffsetZ;
                MinObserveTicks = minObserveTicks;
                MaxObserveTicks = maxObserveTicks;
                RequiredTraces = requiredTraces ?? Array.Empty<TraceRequirement>();
                RequireDamage = requireDamage;
                RequireKnockup = requireKnockup;
                RequiredActorBuffId = requiredActorBuffId;
                MinActorMoveSpeed = minActorMoveSpeed;
                RequiredPresentationVfxId = requiredPresentationVfxId;
            }

            public string Name { get; }
            public int Slot { get; }
            public float AimDx { get; }
            public float AimDz { get; }
            public float EnemyOffsetX { get; }
            public float EnemyOffsetZ { get; }
            public int MinObserveTicks { get; }
            public int MaxObserveTicks { get; }
            public TraceRequirement[] RequiredTraces { get; }
            public bool RequireDamage { get; }
            public bool RequireKnockup { get; }
            public int RequiredActorBuffId { get; }
            public float MinActorMoveSpeed { get; }
            public int RequiredPresentationVfxId { get; }
        }
    }
}
