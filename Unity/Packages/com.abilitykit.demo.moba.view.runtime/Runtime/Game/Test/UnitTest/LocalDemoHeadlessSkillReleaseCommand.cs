using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    [InitializeOnLoad]
    public static class LocalDemoHeadlessSkillReleaseCommand
    {
        private const string RunningKey = "AbilityKit.LocalDemoHeadlessSkillRelease.Running";
        private const string ResultPathKey = "AbilityKit.LocalDemoHeadlessSkillRelease.ResultPath";
        private const float FixedDeltaTime = 1f / 30f;
        private const int MaxTicks = 1500;
        private const float DamageEpsilon = 0.01f;
        private const float KnockupHeightEpsilon = 0.05f;
        private const float LandingHeightEpsilon = 0.03f;
        private const float CasterJumpHeightEpsilon = 0.08f;
        private const float TestHp = 5000f;

        private static readonly int[] s_lianPoTransientAreaTemplateIds = { 40010201, 40010301, 40010311, 40010321 };
        private static readonly List<int> s_actorIds = new List<int>(16);
        private static readonly List<int> s_searchResults = new List<int>(16);
        private static readonly SkillScenario[] s_scenarios =
        {
            new SkillScenario("skill1.first.aim", slot: 1, inputMode: SkillInputMode.AimRelease, aimDx: 1f, aimDz: 0f, enemyOffsetX: 2.5f, enemyOffsetZ: 0f, requireCasterMove: true, requireDamage: true, requireKnockup: true, minObserveTicks: 8, maxObserveTicks: 180),
            new SkillScenario("skill1.second.afterHit.aim", slot: 1, inputMode: SkillInputMode.AimRelease, aimDx: 1f, aimDz: 0f, enemyOffsetX: 2.5f, enemyOffsetZ: 0f, requireCasterMove: true, requireDamage: true, requireKnockup: true, minObserveTicks: 8, maxObserveTicks: 180, preserveCasterState: true, resetEnemyHp: false),
            new SkillScenario("skill2.afterSkill1.click", slot: 2, inputMode: SkillInputMode.Click, aimDx: 1f, aimDz: 0f, enemyOffsetX: 1.5f, enemyOffsetZ: 0f, requireCasterMove: false, requireDamage: true, requireKnockup: false, minObserveTicks: 20, maxObserveTicks: 210, preserveCasterState: true, resetEnemyHp: false, minDamageSeconds: 0.45f, requiredDamageSteps: 1),
            new SkillScenario("skill3.afterSkill1.click", slot: 3, inputMode: SkillInputMode.Click, aimDx: 1f, aimDz: 0f, enemyOffsetX: 1.5f, enemyOffsetZ: 0f, requireCasterMove: false, requireDamage: true, requireKnockup: true, minObserveTicks: 20, maxObserveTicks: 300, preserveCasterState: true, resetEnemyHp: false, minDamageSeconds: 0.32f, requiredDamageSteps: 3, requireCasterJump: true, requireDamageAfterCasterLanding: true),
            new SkillScenario("skill3.afterSkill1.aim", slot: 3, inputMode: SkillInputMode.AimRelease, aimDx: 1f, aimDz: 0f, enemyOffsetX: 1.5f, enemyOffsetZ: 0f, requireCasterMove: true, requireDamage: true, requireKnockup: true, minObserveTicks: 20, maxObserveTicks: 300, preserveCasterState: true, resetEnemyHp: false, minDamageSeconds: 0.32f, requiredDamageSteps: 3, requireCasterJump: true, requireDamageAfterCasterLanding: true),
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
        private static float _scenarioStartCasterX;
        private static float _scenarioStartCasterY;
        private static bool _movementObserved;
        private static float _observedMoveDistance;
        private static bool _casterJumpObserved;
        private static bool _casterLandedAfterJump;
        private static float _observedCasterJumpHeight;
        private static bool _knockupObserved;
        private static float _observedKnockupHeight;
        private static int _damageStepCount;
        private static float _lastObservedDamage;
        private static int _firstDamageTick;
        private static double _firstDamageSeconds;
        private static bool _firstDamageAfterCasterLanding;
        private static AdditionalValidationPhase _additionalPhase;
        private static int _additionalStartTick;
        private static int _additionalObservedTicks;
        private static Vector3 _additionalStartPosition;
        private static Vector3 _additionalBeforeSkill1Position;
        private static Vector3 _additionalAfterSkill1Position;
        private static float _additionalMaxPlanarMove;
        private static float _additionalMaxHeightDelta;
        private static float _additionalMinObservedX;
        private static float _additionalMaxObservedHeight;
        private static bool _additionalSkill3JumpObserved;
        private static readonly List<string> s_scenarioResults = new List<string>(8);

        static LocalDemoHeadlessSkillReleaseCommand()
        {
            EditorApplication.update -= ContinueInPlayMode;
            EditorApplication.update += ContinueInPlayMode;
        }

        public static void Run()
        {
            var resultPath = GetArgValue("-headlessSkillResult");
            if (string.IsNullOrWhiteSpace(resultPath))
            {
                resultPath = Path.GetFullPath("../UnityHeadlessSkillReleaseCommand.xml");
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
            if (_ticks > MaxTicks) throw new TimeoutException("Headless local demo skill release validation timed out. " + BuildDiagnostic(null, null));

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
                    throw new InvalidOperationException("No enemy actor with transform and attrs was found for headless Lian Po skill validation.");
                }
            }

            if (_scenarioIndex >= s_scenarios.Length)
            {
                return TickAdditionalValidations(flow, ctx);
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
                    SubmitScenarioInput(ctx, scenario);
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

        private static string TickAdditionalValidations(GameFlowDomain flow, BattleContext ctx)
        {
            switch (_additionalPhase)
            {
                case AdditionalValidationPhase.Skill3InsertedSkill1Prepare:
                    ResetActiveSkillCooldowns(ctx, ctx.LocalActorId, cancelRunningSkill: true);
                    ClearLianPoTransientAreas(ctx);
                    _additionalStartPosition = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
                    if (!TrySetActorPosition(ctx, ctx.LocalActorId, _additionalStartPosition))
                    {
                        throw new InvalidOperationException($"additional.skill3InsertedSkill1 failed to reset local actor position. actorId={ctx.LocalActorId}, position={_additionalStartPosition}");
                    }

                    if (!TrySetActorPosition(ctx, _enemyActorId, _additionalStartPosition + new Vector3(1.5f, 0f, 0f)))
                    {
                        throw new InvalidOperationException($"additional.skill3InsertedSkill1 failed to place enemy. enemyActorId={_enemyActorId}");
                    }

                    if (!TrySetActorHp(ctx, _enemyActorId, TestHp))
                    {
                        throw new InvalidOperationException($"additional.skill3InsertedSkill1 failed to reset enemy hp. enemyActorId={_enemyActorId}");
                    }

                    _additionalMaxPlanarMove = 0f;
                    _additionalMaxHeightDelta = 0f;
                    _additionalSkill3JumpObserved = false;
                    _additionalStartTick = _ticks;
                    _additionalPhase = AdditionalValidationPhase.Skill3InsertedSkill1SubmitSkill3;
                    Stage("additional.skill3InsertedSkill1.prepared", flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case AdditionalValidationPhase.Skill3InsertedSkill1SubmitSkill3:
                    ctx.SubmitHudSkillAim(slot: 3, aimDx: 1f, aimDz: 0f);
                    _additionalStartTick = _ticks;
                    _additionalPhase = AdditionalValidationPhase.Skill3InsertedSkill1WaitFirstLanding;
                    Stage("additional.skill3InsertedSkill1.submittedSkill3", flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case AdditionalValidationPhase.Skill3InsertedSkill1WaitFirstLanding:
                    if (!TryGetLocalActorPosition(ctx, out var skill3Current))
                    {
                        throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
                    }

                    _additionalMaxPlanarMove = Mathf.Max(_additionalMaxPlanarMove, PlanarDistance(_additionalStartPosition, skill3Current));
                    _additionalMaxHeightDelta = Mathf.Max(_additionalMaxHeightDelta, skill3Current.y - _additionalStartPosition.y);
                    if (skill3Current.x > _additionalStartPosition.x + 0.1f && skill3Current.y > _additionalStartPosition.y + CasterJumpHeightEpsilon)
                    {
                        _additionalSkill3JumpObserved = true;
                    }

                    if (_additionalSkill3JumpObserved && skill3Current.y <= _additionalStartPosition.y + 0.05f && skill3Current.x > _additionalStartPosition.x + 0.1f)
                    {
                        _additionalBeforeSkill1Position = skill3Current;
                        ctx.SubmitHudSkillAim(slot: 1, aimDx: 1f, aimDz: 0f);
                        _additionalStartTick = _ticks;
                        _additionalPhase = AdditionalValidationPhase.Skill3InsertedSkill1WaitSkill1Dash;
                        Stage("additional.skill3InsertedSkill1.submittedSkill1", flow, ctx);
                        FlowTick(flow, 2);
                        return null;
                    }

                    if (_ticks - _additionalStartTick > 180)
                    {
                        throw new TimeoutException($"additional.skill3InsertedSkill1 first stage did not land. start={_additionalStartPosition}, current={skill3Current}, maxMove={_additionalMaxPlanarMove:F3}, maxHeightDelta={_additionalMaxHeightDelta:F3}, " + BuildDiagnostic(flow, ctx));
                    }

                    Stage("additional.skill3InsertedSkill1.waitFirstLanding", flow, ctx);
                    return null;

                case AdditionalValidationPhase.Skill3InsertedSkill1WaitSkill1Dash:
                    if (!TryGetLocalActorPosition(ctx, out var afterSkill1Current))
                    {
                        throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
                    }

                    if (afterSkill1Current.x > _additionalBeforeSkill1Position.x + 0.1f)
                    {
                        _additionalAfterSkill1Position = afterSkill1Current;
                        _additionalMinObservedX = afterSkill1Current.x;
                        _additionalMaxObservedHeight = afterSkill1Current.y;
                        _additionalObservedTicks = 0;
                        _additionalPhase = AdditionalValidationPhase.Skill3InsertedSkill1ObserveResume;
                        Stage("additional.skill3InsertedSkill1.skill1Dashed", flow, ctx);
                        return null;
                    }

                    if (_ticks - _additionalStartTick > 120)
                    {
                        throw new TimeoutException($"additional.skill3InsertedSkill1 inserted skill 1 did not move caster. beforeSkill1={_additionalBeforeSkill1Position}, current={afterSkill1Current}, " + BuildDiagnostic(flow, ctx));
                    }

                    Stage("additional.skill3InsertedSkill1.waitSkill1Dash", flow, ctx);
                    return null;

                case AdditionalValidationPhase.Skill3InsertedSkill1ObserveResume:
                    if (!TryGetLocalActorPosition(ctx, out var resumeCurrent))
                    {
                        throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
                    }

                    _additionalMinObservedX = Mathf.Min(_additionalMinObservedX, resumeCurrent.x);
                    _additionalMaxObservedHeight = Mathf.Max(_additionalMaxObservedHeight, resumeCurrent.y);
                    _additionalObservedTicks++;
                    if (_additionalObservedTicks >= 90)
                    {
                        if (_additionalMinObservedX < _additionalAfterSkill1Position.x - 0.15f)
                        {
                            throw new InvalidOperationException($"additional.skill3InsertedSkill1 later stages pulled caster back to old aim point. start={_additionalStartPosition}, beforeSkill1={_additionalBeforeSkill1Position}, afterSkill1={_additionalAfterSkill1Position}, minObservedX={_additionalMinObservedX:F3}");
                        }

                        if (_additionalMaxObservedHeight <= _additionalStartPosition.y + CasterJumpHeightEpsilon)
                        {
                            throw new InvalidOperationException($"additional.skill3InsertedSkill1 later stages did not keep vertical jump. start={_additionalStartPosition}, maxObservedHeight={_additionalMaxObservedHeight:F3}");
                        }

                        s_scenarioResults.Add($"additional.skill3InsertedSkill1: beforeSkill1={_additionalBeforeSkill1Position}, afterSkill1={_additionalAfterSkill1Position}, minObservedX={_additionalMinObservedX:F3}, maxObservedHeight={_additionalMaxObservedHeight:F3}");
                        _additionalPhase = AdditionalValidationPhase.Skill2RefreshSkill1Prepare;
                        Stage("additional.skill3InsertedSkill1.passed", flow, ctx);
                        FlowTick(flow, 2);
                    }

                    return null;

                case AdditionalValidationPhase.Skill2RefreshSkill1Prepare:
                    ResetActiveSkillCooldowns(ctx, ctx.LocalActorId, cancelRunningSkill: true);
                    ClearLianPoTransientAreas(ctx);
                    if (!TrySetActorPosition(ctx, ctx.LocalActorId, _startPosition))
                    {
                        throw new InvalidOperationException($"additional.skill2RefreshSkill1 failed to reset local actor position. actorId={ctx.LocalActorId}, position={_startPosition}");
                    }

                    if (!TrySetActorPosition(ctx, _enemyActorId, _startPosition + new Vector3(1.5f, 0f, 0f)))
                    {
                        throw new InvalidOperationException($"additional.skill2RefreshSkill1 failed to place enemy. enemyActorId={_enemyActorId}");
                    }

                    if (!TrySetActorHp(ctx, _enemyActorId, TestHp))
                    {
                        throw new InvalidOperationException($"additional.skill2RefreshSkill1 failed to reset enemy hp. enemyActorId={_enemyActorId}");
                    }

                    if (!TryGetActiveSkillRuntime(ctx, ctx.LocalActorId, skillSlot: 1, skillId: 10010101, out var skill1Runtime))
                    {
                        throw new InvalidOperationException("additional.skill2RefreshSkill1 could not find Lian Po skill 1 runtime.");
                    }

                    skill1Runtime.CooldownDurationMs = 5000;
                    skill1Runtime.CooldownEndTimeMs = 5000;
                    _additionalStartTick = _ticks;
                    _additionalPhase = AdditionalValidationPhase.Skill2RefreshSkill1Submit;
                    Stage("additional.skill2RefreshSkill1.prepared", flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case AdditionalValidationPhase.Skill2RefreshSkill1Submit:
                    ctx.SubmitHudSkillClick(slot: 2);
                    _additionalStartTick = _ticks;
                    _additionalPhase = AdditionalValidationPhase.Skill2RefreshSkill1Observe;
                    Stage("additional.skill2RefreshSkill1.submittedSkill2", flow, ctx);
                    FlowTick(flow, 2);
                    return null;

                case AdditionalValidationPhase.Skill2RefreshSkill1Observe:
                    if (TryGetActiveSkillRuntime(ctx, ctx.LocalActorId, skillSlot: 1, skillId: 10010101, out var refreshedRuntime) && refreshedRuntime.CooldownEndTimeMs <= 0L && refreshedRuntime.CooldownDurationMs <= 0)
                    {
                        s_scenarioResults.Add($"additional.skill2RefreshSkill1: cooldownEnd={refreshedRuntime.CooldownEndTimeMs}, cooldownDuration={refreshedRuntime.CooldownDurationMs}");
                        _additionalPhase = AdditionalValidationPhase.Complete;
                        Stage("additional.skill2RefreshSkill1.passed", flow, ctx);
                    }
                    else if (_ticks - _additionalStartTick > 90)
                    {
                        var cooldownText = refreshedRuntime != null ? $"cooldownEnd={refreshedRuntime.CooldownEndTimeMs}, cooldownDuration={refreshedRuntime.CooldownDurationMs}" : "runtimeUnavailable";
                        throw new TimeoutException($"additional.skill2RefreshSkill1 did not refresh skill 1 cooldown. {cooldownText}, " + BuildDiagnostic(flow, ctx));
                    }

                    return null;

                case AdditionalValidationPhase.Complete:
                    return "PASS: Lian Po headless skill validation completed. " + string.Join(" | ", s_scenarioResults);

                default:
                    throw new InvalidOperationException($"Unsupported additional validation phase: {_additionalPhase}");
            }
        }

        private static void SubmitScenarioInput(BattleContext ctx, SkillScenario scenario)
        {
            switch (scenario.InputMode)
            {
                case SkillInputMode.Click:
                    ctx.SubmitHudSkillClick(scenario.Slot);
                    return;
                case SkillInputMode.AimRelease:
                    ctx.SubmitHudSkillAim(scenario.Slot, scenario.AimDx, scenario.AimDz);
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported skill scenario input mode: {scenario.InputMode}");
            }
        }

        private static string ObserveScenario(GameFlowDomain flow, BattleContext ctx, SkillScenario scenario)
        {
            if (!TryGetLocalActorPosition(ctx, out var casterPosition))
            {
                throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
            }

            var casterHeightDelta = casterPosition.y - _scenarioStartCasterY;
            if (casterHeightDelta > _observedCasterJumpHeight) _observedCasterJumpHeight = casterHeightDelta;
            if (casterHeightDelta > CasterJumpHeightEpsilon) _casterJumpObserved = true;
            if (_casterJumpObserved && Math.Abs(casterHeightDelta) <= LandingHeightEpsilon) _casterLandedAfterJump = true;

            var moved = Math.Abs(casterPosition.x - _scenarioStartCasterX);
            if (moved > 0.1f)
            {
                if (scenario.RequireCasterMove && scenario.AimDx > 0f && casterPosition.x - _scenarioStartCasterX <= 0.1f)
                {
                    throw new InvalidOperationException($"{scenario.Name} should follow +X aim direction. startX={_scenarioStartCasterX:F3}, current={casterPosition}, moved={moved:F3}");
                }

                _movementObserved = true;
                _observedMoveDistance = moved;
            }

            if (!TryGetEnemyRuntimeState(ctx, _enemyActorId, out var enemyHp, out var enemyPosition))
            {
                throw new InvalidOperationException($"Prepared enemy actor disappeared. enemyActorId={_enemyActorId}");
            }

            _lastEnemyHp = enemyHp;
            _lastEnemyPosition = enemyPosition;
            var damage = _enemyStartHp - enemyHp;
            var enemyPlanarMove = PlanarDistance(_enemyStartPosition, enemyPosition);
            var enemyHeightDelta = enemyPosition.y - _enemyStartPosition.y;
            if (enemyHeightDelta > _observedKnockupHeight) _observedKnockupHeight = enemyHeightDelta;
            if (enemyHeightDelta > KnockupHeightEpsilon) _knockupObserved = true;

            var ticksSinceSubmit = _ticks - _scenarioSubmitTick;
            var secondsSinceSubmit = ctx.LogicTimeSeconds - _scenarioSubmitLogicTime;
            if (damage > _lastObservedDamage + DamageEpsilon)
            {
                if (scenario.RequireDamageAfterCasterLanding && !_casterLandedAfterJump)
                {
                    throw new InvalidOperationException($"{scenario.Name} dealt damage before caster landing. casterHeightDelta={casterHeightDelta:F3}, maxCasterHeightDelta={_observedCasterJumpHeight:F3}, ticksSinceSubmit={ticksSinceSubmit}, damage={damage:F3}");
                }

                _damageStepCount++;
                _lastObservedDamage = damage;
                if (_firstDamageTick <= 0)
                {
                    _firstDamageTick = ticksSinceSubmit;
                    _firstDamageSeconds = secondsSinceSubmit;
                    _firstDamageAfterCasterLanding = _casterLandedAfterJump;
                }
            }

            if (scenario.MinDamageSeconds > 0f && secondsSinceSubmit < scenario.MinDamageSeconds && damage > DamageEpsilon)
            {
                throw new InvalidOperationException($"{scenario.Name} dealt damage before configured delay. secondsSinceSubmit={secondsSinceSubmit:F3}, minDamageSeconds={scenario.MinDamageSeconds:F3}, ticksSinceSubmit={ticksSinceSubmit}, damage={damage:F3}");
            }

            var observedEnoughTicks = ticksSinceSubmit >= scenario.MinObserveTicks;
            var landed = Math.Abs(enemyHeightDelta) <= LandingHeightEpsilon;
            var movementOk = !scenario.RequireCasterMove || _movementObserved;
            var damageOk = !scenario.RequireDamage || damage > DamageEpsilon;
            var damageStepsOk = scenario.RequiredDamageSteps <= 0 || _damageStepCount >= scenario.RequiredDamageSteps;
            var damageDelayOk = scenario.MinDamageSeconds <= 0f || (_firstDamageTick > 0 && _firstDamageSeconds >= scenario.MinDamageSeconds);
            var knockupOk = !scenario.RequireKnockup || (_knockupObserved && landed);
            var casterJumpOk = !scenario.RequireCasterJump || (_casterJumpObserved && _casterLandedAfterJump);
            var damageAfterCasterLandingOk = !scenario.RequireDamageAfterCasterLanding || (_firstDamageTick > 0 && _firstDamageAfterCasterLanding);

            if (observedEnoughTicks && movementOk && damageOk && damageStepsOk && damageDelayOk && knockupOk && casterJumpOk && damageAfterCasterLandingOk)
            {
                var result = $"{scenario.Name}: input={scenario.InputMode}, damage={damage:F3}, damageSteps={_damageStepCount}, firstDamageTick={_firstDamageTick}, firstDamageTime={_firstDamageSeconds:F3}, casterMove={_observedMoveDistance:F3}, maxCasterHeightDelta={_observedCasterJumpHeight:F3}, casterLanded={_casterLandedAfterJump}, enemyPlanarMove={enemyPlanarMove:F3}, maxEnemyHeightDelta={_observedKnockupHeight:F3}, landedHeightDelta={enemyHeightDelta:F3}";
                s_scenarioResults.Add(result);
                _scenarioIndex++;
                _scenarioPhase = ScenarioPhase.Prepare;
                Stage("passed." + scenario.Name, flow, ctx);
                FlowTick(flow, 4);
                return null;
            }

            if (_ticks - _scenarioStartTick > scenario.MaxObserveTicks)
            {
                throw new TimeoutException($"{scenario.Name} validation timed out. damage={damage:F3}, damageSteps={_damageStepCount}, requiredDamageSteps={scenario.RequiredDamageSteps}, movementOk={movementOk}, damageOk={damageOk}, damageStepsOk={damageStepsOk}, knockupOk={knockupOk}, casterJumpOk={casterJumpOk}, damageAfterCasterLandingOk={damageAfterCasterLandingOk}, landed={landed}, casterLanded={_casterLandedAfterJump}, maxCasterHeightDelta={_observedCasterJumpHeight:F3}, " + BuildDiagnostic(flow, ctx));
            }

            Stage("observing." + scenario.Name, flow, ctx);
            return null;
        }

        private static void PrepareScenario(BattleContext ctx, SkillScenario scenario)
        {
            if (!TryGetLocalActorPosition(ctx, out _))
            {
                throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
            }

            var casterPosition = default(Vector3);
            if (scenario.PreserveCasterState)
            {
                if (!TryGetLocalActorPosition(ctx, out casterPosition))
                {
                    throw new InvalidOperationException(DescribeContextWaitFailure(ctx));
                }
            }
            else
            {
                casterPosition = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
                if (!TrySetActorPosition(ctx, ctx.LocalActorId, casterPosition))
                {
                    throw new InvalidOperationException($"Failed to reset local actor position. actorId={ctx.LocalActorId}, position={casterPosition}");
                }
            }

            if (!scenario.PreserveEnemyState)
            {
                var targetPosition = new Vector3(casterPosition.x + scenario.EnemyOffsetX, casterPosition.y, casterPosition.z + scenario.EnemyOffsetZ);
                if (!TrySetActorPosition(ctx, _enemyActorId, targetPosition))
                {
                    throw new InvalidOperationException($"Failed to place enemy actor for {scenario.Name}. enemyActorId={_enemyActorId}, targetPosition={targetPosition}");
                }

                if (scenario.ResetEnemyHp && !TrySetActorHp(ctx, _enemyActorId, TestHp))
                {
                    throw new InvalidOperationException($"Failed to reset enemy hp for {scenario.Name}. enemyActorId={_enemyActorId}");
                }
            }

            ResetActiveSkillCooldowns(ctx, ctx.LocalActorId, cancelRunningSkill: !scenario.PreserveCasterState);
            ClearLianPoTransientAreas(ctx);

            if (!TryGetEnemyRuntimeState(ctx, _enemyActorId, out _enemyStartHp, out _enemyStartPosition))
            {
                throw new InvalidOperationException($"Failed to capture enemy baseline after placement. enemyActorId={_enemyActorId}");
            }

            ValidateScenarioSetup(ctx, scenario, casterPosition);

            _lastEnemyHp = _enemyStartHp;
            _lastEnemyPosition = _enemyStartPosition;
            _scenarioStartCasterX = casterPosition.x;
            _scenarioStartCasterY = casterPosition.y;
            _movementObserved = false;
            _observedMoveDistance = 0f;
            _casterJumpObserved = false;
            _casterLandedAfterJump = false;
            _observedCasterJumpHeight = 0f;
            _knockupObserved = false;
            _observedKnockupHeight = 0f;
            _damageStepCount = 0;
            _lastObservedDamage = 0f;
            _firstDamageTick = 0;
            _firstDamageSeconds = 0d;
            _firstDamageAfterCasterLanding = false;
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

        private static void ClearLianPoTransientAreas(BattleContext ctx)
        {
            if (ctx?.Session == null || ctx.LocalActorId <= 0) return;
            if (!ctx.Session.TryGetWorld(out var world) || world?.Services == null) return;
            if (!world.Services.TryResolve<MobaAreaRuntimeService>(out var areas) || areas == null) return;

            for (var i = 0; i < s_lianPoTransientAreaTemplateIds.Length; i++)
            {
                areas.DespawnAreas(ctx.LocalActorId, s_lianPoTransientAreaTemplateIds[i], removeAll: true);
            }
        }

        private static void ValidateScenarioSetup(BattleContext ctx, SkillScenario scenario, Vector3 casterPosition)
        {
            if (ctx == null || scenario.Slot <= 1) return;
            if (ctx.Session == null || !ctx.Session.TryGetWorld(out var world) || world?.Services == null) return;

            var expectedTriggerId = scenario.Slot == 2 ? 10010211 : 10010331;
            if (!world.Services.TryResolve<TriggerPlanJsonDatabase>(out var triggerDb) || triggerDb == null)
            {
                throw new InvalidOperationException($"{scenario.Name} validation requires TriggerPlanJsonDatabase.");
            }

            if (!triggerDb.TryGetPlanByTriggerId(expectedTriggerId, out _))
            {
                throw new InvalidOperationException($"{scenario.Name} trigger plan was not loaded. triggerId={expectedTriggerId}");
            }

            var queryId = scenario.Slot == 2 ? 50010201 : 50010301;
            if (!world.Services.TryResolve<SearchTargetService>(out var search) || search == null)
            {
                throw new InvalidOperationException($"{scenario.Name} validation requires SearchTargetService.");
            }

            s_searchResults.Clear();
            var aim = ToVec3(new Vector3(scenario.AimDx, 0f, scenario.AimDz));
            if (!search.TrySearchActorIds(queryId, ctx.LocalActorId, in aim, explicitTargetActorId: 0, s_searchResults) || !s_searchResults.Contains(_enemyActorId))
            {
                throw new InvalidOperationException($"{scenario.Name} target query did not find prepared enemy. queryId={queryId}, caster={ctx.LocalActorId}, enemy={_enemyActorId}, casterPos={casterPosition}, enemyPos={_enemyStartPosition}, resultCount={s_searchResults.Count}");
            }

            Debug.Log($"[LocalDemoHeadlessSkillReleaseCommand] setup validated. scenario={scenario.Name}, triggerId={expectedTriggerId}, queryId={queryId}, targets={string.Join(",", s_searchResults)}");
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

        private static Vector3 ToUnityVector(Vec3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
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

        private static void Stage(string stage, GameFlowDomain flow, BattleContext ctx)
        {
            var changed = !string.Equals(_lastStage, stage, StringComparison.Ordinal);
            var periodic = _ticks - _lastPeriodicDiagnosticTick >= 60;
            if (!changed && !periodic) return;

            _lastStage = stage;
            _lastPeriodicDiagnosticTick = _ticks;
            Debug.Log("[LocalDemoHeadlessSkillReleaseCommand] " + BuildDiagnostic(flow, ctx));
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
                ? $", enemyActorId={_enemyActorId}, enemyStartHp={_enemyStartHp:F3}, lastEnemyHp={_lastEnemyHp:F3}, enemyStart={_enemyStartPosition}, lastEnemy={_lastEnemyPosition}, casterEnemyDistance={TryDescribeCasterEnemyDistance(ctx)}, enemyMove3d={Vector3.Distance(_enemyStartPosition, _lastEnemyPosition):F3}, enemyHeightDelta={(_lastEnemyPosition.y - _enemyStartPosition.y):F3}, observedKnockupHeight={_observedKnockupHeight:F3}, knockupObserved={_knockupObserved}, movementObserved={_movementObserved}, observedMove={_observedMoveDistance:F3}, damageSteps={_damageStepCount}, firstDamageTick={_firstDamageTick}, motion={DescribeActorMotion(ctx, ctx != null ? ctx.LocalActorId : 0)}, enemyMotion={DescribeActorMotion(ctx, _enemyActorId)}, search={DescribeScenarioSearch(ctx, scenario)}"
                : string.Empty;

            var inputMode = _scenarioIndex >= 0 && _scenarioIndex < s_scenarios.Length ? scenario.InputMode.ToString() : "n/a";
            return $"stage={_lastStage ?? "notStarted"}, ticks={_ticks}, battleRequested={_battleRequested}, scenario={scenarioName}, inputMode={inputMode}, scenarioIndex={_scenarioIndex}, scenarioPhase={_scenarioPhase}, hasStartPosition={_hasStartPosition}, battlePhase={(flow != null ? flow.CurrentBattlePhase.ToString() : "n/a")}, hasContext={ctx != null}, hasSession={(ctx != null && ctx.Session != null)}, localActorId={(ctx != null ? ctx.LocalActorId : 0)}, planPlayerId={(ctx != null ? ctx.Plan.World.PlayerId : "n/a")}, lastFrame={(ctx != null ? ctx.LastFrame : 0)}, logicTime={(ctx != null ? ctx.LogicTimeSeconds : 0f):F3}, start={(_hasStartPosition ? _startPosition.ToString() : "n/a")}, current={currentPosition}{enemyText}";
        }

        private static string TryDescribeCasterEnemyDistance(BattleContext ctx)
        {
            if (ctx == null || ctx.LocalActorId <= 0 || _enemyActorId <= 0) return "n/a";
            if (!TryGetLocalActorPosition(ctx, out var caster)) return "casterUnavailable";
            if (!TryGetEnemyRuntimeState(ctx, _enemyActorId, out _, out var enemy)) return "enemyUnavailable";
            return PlanarDistance(caster, enemy).ToString("F3");
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

        private static string DescribeScenarioSearch(BattleContext ctx, SkillScenario scenario)
        {
            if (ctx == null || scenario.Slot <= 1) return "n/a";
            if (ctx.Session == null || !ctx.Session.TryGetWorld(out var world) || world?.Services == null) return "worldUnavailable";
            if (!world.Services.TryResolve<SearchTargetService>(out var search) || search == null) return "searchUnavailable";

            var queryId = scenario.Slot == 2 ? 50010201 : 50010301;
            s_searchResults.Clear();
            var aim = ToVec3(new Vector3(scenario.AimDx, 0f, scenario.AimDz));
            var found = search.TrySearchActorIds(queryId, ctx.LocalActorId, in aim, explicitTargetActorId: 0, s_searchResults);
            var containsEnemy = s_searchResults.Contains(_enemyActorId);
            return $"queryId={queryId}, found={found}, containsEnemy={containsEnemy}, count={s_searchResults.Count}, targets={string.Join(",", s_searchResults)}";
        }

        private static void Finish(bool success, string message)
        {
            var resultPath = SessionState.GetString(ResultPathKey, Path.GetFullPath("../UnityHeadlessSkillReleaseCommand.xml"));
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(ResultPathKey);
            ResetState();

            WriteResult(resultPath, success, message);
            Debug.Log("[LocalDemoHeadlessSkillReleaseCommand] " + message);

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
            _scenarioStartCasterX = 0f;
            _scenarioStartCasterY = 0f;
            _movementObserved = false;
            _observedMoveDistance = 0f;
            _casterJumpObserved = false;
            _casterLandedAfterJump = false;
            _observedCasterJumpHeight = 0f;
            _knockupObserved = false;
            _observedKnockupHeight = 0f;
            _damageStepCount = 0;
            _lastObservedDamage = 0f;
            _firstDamageTick = 0;
            _firstDamageSeconds = 0d;
            _firstDamageAfterCasterLanding = false;
            _additionalPhase = AdditionalValidationPhase.Skill3InsertedSkill1Prepare;
            _additionalStartTick = 0;
            _additionalObservedTicks = 0;
            _additionalStartPosition = default;
            _additionalBeforeSkill1Position = default;
            _additionalAfterSkill1Position = default;
            _additionalMaxPlanarMove = 0f;
            _additionalMaxHeightDelta = 0f;
            _additionalMinObservedX = 0f;
            _additionalMaxObservedHeight = 0f;
            _additionalSkill3JumpObserved = false;
            s_scenarioResults.Clear();
            s_searchResults.Clear();
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

        private enum AdditionalValidationPhase
        {
            Skill3InsertedSkill1Prepare,
            Skill3InsertedSkill1SubmitSkill3,
            Skill3InsertedSkill1WaitFirstLanding,
            Skill3InsertedSkill1WaitSkill1Dash,
            Skill3InsertedSkill1ObserveResume,
            Skill2RefreshSkill1Prepare,
            Skill2RefreshSkill1Submit,
            Skill2RefreshSkill1Observe,
            Complete
        }

        private enum ScenarioPhase
        {
            Prepare,
            Submit,
            Observe
        }

        private enum SkillInputMode
        {
            Click,
            AimRelease
        }

        private readonly struct SkillScenario
        {
            public SkillScenario(string name, int slot, SkillInputMode inputMode, float aimDx, float aimDz, float enemyOffsetX, float enemyOffsetZ, bool requireCasterMove, bool requireDamage, bool requireKnockup, int minObserveTicks, int maxObserveTicks, bool preserveCasterState = false, bool preserveEnemyState = false, bool resetEnemyHp = true, float minDamageSeconds = 0f, int requiredDamageSteps = 0, bool requireCasterJump = false, bool requireDamageAfterCasterLanding = false)
            {
                Name = name;
                Slot = slot;
                InputMode = inputMode;
                AimDx = aimDx;
                AimDz = aimDz;
                EnemyOffsetX = enemyOffsetX;
                EnemyOffsetZ = enemyOffsetZ;
                RequireCasterMove = requireCasterMove;
                RequireDamage = requireDamage;
                RequireKnockup = requireKnockup;
                MinObserveTicks = minObserveTicks;
                MaxObserveTicks = maxObserveTicks;
                PreserveCasterState = preserveCasterState;
                PreserveEnemyState = preserveEnemyState;
                ResetEnemyHp = resetEnemyHp;
                MinDamageSeconds = minDamageSeconds;
                RequiredDamageSteps = requiredDamageSteps;
                RequireCasterJump = requireCasterJump;
                RequireDamageAfterCasterLanding = requireDamageAfterCasterLanding;
            }

            public string Name { get; }
            public int Slot { get; }
            public SkillInputMode InputMode { get; }
            public float AimDx { get; }
            public float AimDz { get; }
            public float EnemyOffsetX { get; }
            public float EnemyOffsetZ { get; }
            public bool RequireCasterMove { get; }
            public bool RequireDamage { get; }
            public bool RequireKnockup { get; }
            public int MinObserveTicks { get; }
            public int MaxObserveTicks { get; }
            public bool PreserveCasterState { get; }
            public bool PreserveEnemyState { get; }
            public bool ResetEnemyHp { get; }
            public float MinDamageSeconds { get; }
            public int RequiredDamageSteps { get; }
            public bool RequireCasterJump { get; }
            public bool RequireDamageAfterCasterLanding { get; }
        }
    }

    [InitializeOnLoad]
    internal static class DemoGameplayTestLauncher
    {
        private const string GameplayScenePath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Scenes/" + DemoSceneRoutes.Moba + ".unity";
        private const string PendingKey = "AbilityKit.DemoGameplayTestLauncher.Pending";
        private const string GameplayKey = "AbilityKit.DemoGameplayTestLauncher.Gameplay";
        private const string ModeKey = "AbilityKit.DemoGameplayTestLauncher.Mode";
        private const string ProfileKey = "AbilityKit.DemoGameplayTestLauncher.Profile";

        static DemoGameplayTestLauncher()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            RestorePendingRequest();
        }

        public static Scene OpenMobaLocalForPlay()
        {
            var scene = OpenGameplayScene();
            ScheduleRequest(DemoGameplayId.Moba, DemoLaunchMode.Local, string.Empty);
            return scene;
        }

        public static Scene OpenMobaLocalAndCompose()
        {
            Clear();
            var scene = OpenGameplayScene();
            PublishRequest(DemoGameplayId.Moba, DemoLaunchMode.Local, string.Empty);
            var bootstrap = Object.FindObjectOfType<DemoGameplayBootstrap>();
            if (bootstrap == null)
            {
                throw new InvalidOperationException(
                    $"{DemoSceneRoutes.Moba} must contain a {nameof(DemoGameplayBootstrap)}.");
            }
            if (!bootstrap.TryLaunch(out var error))
            {
                throw new InvalidOperationException(
                    $"Failed to compose the local MOBA test root: {error}");
            }

            return scene;
        }

        public static void Clear()
        {
            SessionState.EraseBool(PendingKey);
            SessionState.EraseInt(GameplayKey);
            SessionState.EraseInt(ModeKey);
            SessionState.EraseString(ProfileKey);
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
        }

        private static Scene OpenGameplayScene()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException(
                    $"MOBA gameplay scene should load from {GameplayScenePath}.");
            }

            return scene;
        }

        private static void ScheduleRequest(
            DemoGameplayId gameplay,
            DemoLaunchMode mode,
            string profileId)
        {
            SessionState.SetInt(GameplayKey, (int)gameplay);
            SessionState.SetInt(ModeKey, (int)mode);
            SessionState.SetString(ProfileKey, profileId ?? string.Empty);
            SessionState.SetBool(PendingKey, true);
            PublishRequest(gameplay, mode, profileId);
        }

        private static void RestorePendingRequest()
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            PublishRequest(
                (DemoGameplayId)SessionState.GetInt(GameplayKey, (int)DemoGameplayId.Moba),
                (DemoLaunchMode)SessionState.GetInt(ModeKey, (int)DemoLaunchMode.Local),
                SessionState.GetString(ProfileKey, string.Empty));
        }

        private static void PublishRequest(
            DemoGameplayId gameplay,
            DemoLaunchMode mode,
            string profileId)
        {
            var request = new DemoLaunchRequest(gameplay, mode, profileId);
            DemoLaunchIntent.Request(in request);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Clear();
            }
        }
    }
}
