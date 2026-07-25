using System;
using System.Collections.Generic;
using AbilityKit.Ability;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Protocol.Moba;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct SkillCastPolicy
    {
        public static readonly SkillCastPolicy Default = new SkillCastPolicy(allowParallel: false, interruptRunning: false);

        public SkillCastPolicy(bool allowParallel, bool interruptRunning)
        {
            AllowParallel = allowParallel;
            InterruptRunning = interruptRunning;
        }

        public bool AllowParallel { get; }
        public bool InterruptRunning { get; }

        public SkillCastPolicy WithAllowParallel(bool allowParallel)
        {
            return new SkillCastPolicy(allowParallel, InterruptRunning);
        }

        public SkillCastPolicy WithInterruptRunning(bool interruptRunning)
        {
            return new SkillCastPolicy(AllowParallel, interruptRunning);
        }
    }

    [WorldService(typeof(SkillCastCoordinator))]
    public sealed class SkillCastCoordinator : IService
    {
        private readonly IWorldResolver _services;
        private readonly IWorldClock _clock;
        private readonly IFrameTime _time;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly IUnitResolver _units;
        private readonly MobaSkillLoadoutService _loadout;
        private readonly MobaActorLookupService _actors;
        private readonly IMobaSkillPipelineLibrary _library;
        private readonly SkillCastPreparationService _preparation;
        private readonly SkillCastPolicyResolver _policyResolver;
        private readonly SkillRunnerRegistry _runnerRegistry;
        private SkillCastPolicy _castPolicy = SkillCastPolicy.Default;

        public SkillCastPolicy CastPolicy
        {
            get => _castPolicy;
            set => _castPolicy = value;
        }

        public bool AllowParallel
        {
            get => _castPolicy.AllowParallel;
            set => _castPolicy = _castPolicy.WithAllowParallel(value);
        }

        public bool InterruptRunning
        {
            get => _castPolicy.InterruptRunning;
            set => _castPolicy = _castPolicy.WithInterruptRunning(value);
        }

        public SkillCastCoordinator(
            IWorldResolver services,
            IWorldClock clock,
            IFrameTime time,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            IUnitResolver units,
            MobaSkillLoadoutService loadout,
            MobaActorLookupService actors,
            IMobaSkillPipelineLibrary library,
            IMobaBattleDiagnosticsService diagnostics = null,
            IMobaBattleExceptionPolicy exceptions = null,
            ISkillLogger skillLogger = null)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _preparation = new SkillCastPreparationService(_services, _eventBus, _units, _actors, _library);
            _policyResolver = new SkillCastPolicyResolver(_services);
            _runnerRegistry = new SkillRunnerRegistry(diagnostics, exceptions, skillLogger ?? SkillLogger.Instance);
        }

        public bool CastBySlot(int actorId, int slot)
        {
            return CastBySlot(actorId, slot, out _);
        }

        public bool CastBySlot(int actorId, int slot, out string failReason)
        {
            var result = TryCastBySlot(actorId, slot);
            failReason = result.FailReason;
            return result.Success;
        }

        public MobaSkillCastResult TryCastBySlot(int actorId, int slot)
        {
            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                return MobaSkillCastResult.Failed("Skill not found in slot.");
            }

            return TryCastSkill(actorId, skillId, slot);
        }

        public bool HandleInput(int actorId, in SkillInputEvent evt)
        {
            return TryHandleInputResult(actorId, in evt).Success;
        }

        public bool TryHandleInput(int actorId, in SkillInputEvent evt, out string failReason)
        {
            var result = TryHandleInputResult(actorId, in evt);
            failReason = result.Success ? result.Message : result.Failure.Message ?? result.Message;
            return result.Success;
        }

        public MobaSkillInputHandleResult TryHandleInputResult(int actorId, in SkillInputEvent evt)
        {
            var validation = ValidateSkillInput(actorId, in evt);
            if (!validation.Success)
            {
                return validation;
            }

            return DispatchSkillInputPhase(actorId, in evt);
        }

        private static MobaSkillInputHandleResult ValidateSkillInput(int actorId, in SkillInputEvent evt)
        {
            if (actorId <= 0)
            {
                return MobaSkillInputHandleResult.Failed("skill.input.invalidActor", "Invalid actor id.");
            }

            if (evt.Slot <= 0)
            {
                return MobaSkillInputHandleResult.Failed("skill.input.invalidSlot", "Invalid skill slot.");
            }

            return MobaSkillInputHandleResult.Accepted();
        }

        private MobaSkillInputHandleResult DispatchSkillInputPhase(int actorId, in SkillInputEvent evt)
        {
            switch (evt.Phase)
            {
                case SkillInputPhase.Press:
                    return HandlePressInput(actorId, in evt);
                case SkillInputPhase.Hold:
                    return HandleHoldInput(actorId, in evt);
                case SkillInputPhase.Release:
                    return HandleReleaseInput(actorId, in evt);
                case SkillInputPhase.Cancel:
                    return HandleCancelInput(actorId, evt.Slot);
                default:
                    return SkillResultFactory.InputFailed("skill.input.unsupportedPhase", "Unsupported skill input phase.");
            }
        }

        private MobaSkillInputHandleResult HandlePressInput(int actorId, in SkillInputEvent evt)
        {
            if (_runnerRegistry.TryUpdateRunningInput(actorId, evt.Slot, in evt.AimPos, in evt.AimDir, evt.TargetActorId))
            {
                return MobaSkillInputHandleResult.Accepted("skill.input.running.updated");
            }

            return TryStartCastFromInput(actorId, in evt);
        }

        private MobaSkillInputHandleResult HandleHoldInput(int actorId, in SkillInputEvent evt)
        {
            if (_runnerRegistry.TryUpdateRunningInput(actorId, evt.Slot, in evt.AimPos, in evt.AimDir, evt.TargetActorId))
            {
                return MobaSkillInputHandleResult.Accepted("skill.input.running.updated");
            }

            return MobaSkillInputHandleResult.Failed("skill.input.noRunningForHold", "No running skill for hold input.");
        }

        private MobaSkillInputHandleResult HandleReleaseInput(int actorId, in SkillInputEvent evt)
        {
            if (_runnerRegistry.TryUpdateRunningInputAndRelease(actorId, evt.Slot, in evt.AimPos, in evt.AimDir, evt.TargetActorId))
            {
                return MobaSkillInputHandleResult.Accepted("skill.input.running.released");
            }

            return TryStartCastFromInput(actorId, in evt);
        }

        private MobaSkillInputHandleResult HandleCancelInput(int actorId, int slot)
        {
            if (_runnerRegistry.TryCancelBySlot(actorId, slot))
            {
                return MobaSkillInputHandleResult.Accepted("skill.input.running.cancelled");
            }

            return MobaSkillInputHandleResult.Failed("skill.input.noRunningForCancel", "No running skill for cancel input.");
        }

        private MobaSkillInputHandleResult TryStartCastFromInput(int actorId, in SkillInputEvent evt)
        {
            var result = TryCastBySlot(actorId, evt.Slot, in evt.AimPos, in evt.AimDir, evt.TargetActorId);
            return MobaSkillInputHandleResult.FromCast(in result, "skill.input.cast.started");
        }

        public bool CastBySlot(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, out string failReason)
        {
            return CastBySlot(actorId, slot, in aimPos, in aimDir, targetActorId: 0, out failReason);
        }

        public bool CastBySlot(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, int targetActorId, out string failReason)
        {
            var result = TryCastBySlot(actorId, slot, in aimPos, in aimDir, targetActorId);
            failReason = result.FailReason;
            return result.Success;
        }

        public MobaSkillCastResult TryCastBySlot(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir)
        {
            return TryCastBySlot(actorId, slot, in aimPos, in aimDir, targetActorId: 0);
        }

        public MobaSkillCastResult TryCastBySlot(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                return MobaSkillCastResult.Failed(
                    "Skill not found in slot.",
                    new MobaSkillCastFailure("Preparation", null, "skill.cast.slotNotFound", "Skill not found in slot."));
            }

            return TryCastSkill(actorId, skillId, slot, in aimPos, in aimDir, targetActorId);
        }

        public bool CastSkill(int actorId, int skillId)
        {
            return TryCastSkill(actorId, skillId).Success;
        }

        public MobaSkillCastResult TryCastSkill(int actorId, int skillId)
        {
            return TryCastSkill(actorId, skillId, slot: 0);
        }

        public bool CastSkill(int actorId, int skillId, int slot, out string failReason)
        {
            var result = TryCastSkill(actorId, skillId, slot);
            failReason = result.FailReason;
            return result.Success;
        }

        public MobaSkillCastResult TryCastSkill(int actorId, int skillId, int slot)
        {
            return CastSkillInternal(actorId, skillId, slot, aimPos: default, aimDir: default, hasAim: false);
        }

        public bool CastSkill(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir, out string failReason)
        {
            var result = TryCastSkill(actorId, skillId, slot, in aimPos, in aimDir);
            failReason = result.FailReason;
            return result.Success;
        }

        public MobaSkillCastResult TryCastSkill(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir)
        {
            return TryCastSkill(actorId, skillId, slot, in aimPos, in aimDir, targetActorId: 0);
        }

        public MobaSkillCastResult TryCastSkill(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            return CastSkillInternal(actorId, skillId, slot, aimPos, aimDir, hasAim: true, targetActorId);
        }

        private MobaSkillCastResult CastSkillInternal(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir, bool hasAim, int targetActorId = 0)
        {
            var resolvedSkillId = ResolveModifiedSkillId(actorId, skillId);
            var input = new SkillCastPreparationInput(actorId, resolvedSkillId, slot, in aimPos, in aimDir, hasAim, targetActorId);
            var prepared = _preparation.Prepare(in input);
            if (!prepared.Success)
            {
                var failure = prepared.Failure;
                return MobaSkillCastResult.Failed(prepared.FailReason, in failure);
            }

            return StartPreparedCast(actorId, resolvedSkillId, in prepared);
        }

        private int ResolveModifiedSkillId(int actorId, int skillId)
        {
            if (actorId <= 0 || skillId <= 0) return skillId;
            if (!IsNormalAttackSkill(skillId)) return skillId;
            if (_services == null || !_services.TryResolve<MobaSkillParamModifierService>(out var modifiers) || modifiers == null) return skillId;

            var resolved = modifiers.Skill.ResolveSkillId(actorId, skillId);
            return resolved > 0 ? resolved : skillId;
        }

        private bool IsNormalAttackSkill(int skillId)
        {
            if (skillId <= 0) return false;
            if (_services == null || !_services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return false;
            if (!configs.TryGetSkill(skillId, out var skill) || skill == null) return false;

            return skill.SkillType == SkillType.NormalAttack;
        }

        private MobaSkillCastResult StartPreparedCast(int actorId, int skillId, in SkillCastPreparationResult prepared)
        {
            var ctx = prepared.Context;

            // Combat rules gate: reject cast if caster is dead/stunned/silenced.
            // Previously CanCastSkill was defined in MobaCombatRulesService but never called
            // — dead/stunned/silenced actors could still start skill pipelines.
            // This check closes that gap. See ability-kit skill invariants for background.
            if (_services != null && _services.TryResolve<MobaCombatRulesService>(out var combatRules) && combatRules != null)
            {
                var ruleResult = combatRules.CanCastSkill(actorId);
                if (!ruleResult.Passed)
                {
                    var combatFailure = new MobaSkillCastFailure(
                        "CombatRules",
                        "StartPreparedCast",
                        $"combat.{ruleResult.Failure}",
                        ruleResult.Message);
                    return new MobaSkillCastResult(false, ruleResult.Message, in ctx.RuntimeHandle, in combatFailure);
                }
            }

            var req = prepared.Request;
            var runner = _runnerRegistry.GetOrCreate(actorId);
            var policy = _policyResolver.Resolve(skillId, _castPolicy);
            var success = runner.Start(
                prepared.PreCastConfig,
                prepared.PreCastPhases,
                prepared.CastConfig,
                prepared.CastPhases,
                abilityInstance: this,
                in req,
                ctx,
                out var failReason,
                policy: policy);
            var failure = MobaSkillCastFailure.None;
            if (success)
            {
                ApplyConfiguredCooldown(actorId, skillId, in prepared);
            }
            else
            {
                failure = SkillResultFactory.StartReject(runner, failReason);
                if (!failure.HasValue)
                {
                    failure = SkillResultFactory.PipelineFailure(runner, failReason);
                }

                if (!failure.HasValue)
                {
                    failure = SkillResultFactory.UnknownCastFailure(failReason);
                }

                prepared.Runtimes.ForceTerminate(in ctx.RuntimeHandle, MobaSkillRuntimeEndReason.RollbackCleanup);
            }

            return MobaSkillCastResult.From(success, failReason, in ctx.RuntimeHandle, in failure);
        }

        // CD Refund 策略（2026-07-20 确定）：
        //
        // 当前行为已经是竞技级 MOBA 的合理默认——CD 只在 pipeline 成功启动后扣（success==true），
        // 失败/被拦截时不扣。具体场景：
        //
        // 1. 起手被状态拦截（CanCastSkill 返回 Dead/Stunned/Silenced）：
        //    → 不进入 runner.Start → 不扣 CD。玩家无成本重试。✅ 已由 CanCastSkill 检查保证。
        //
        // 2. pipeline 启动失败（runner.Start 返回 false）：
        //    → ApplyConfiguredCooldown 不被调用 → 不扣 CD。✅ 已由 success 分支保证。
        //
        // 3. 运行中被硬控打断（stun/silence 中断正在飞的施法）：
        //    → CD 已扣，**不退**。竞技级惯例：惩罚被打断的位置差，避免"无成本试探"。
        //
        // 4. 主动切换技能打断（玩家施放另一个技能取消当前施法）：
        //    → CD 已扣，**不退**。避免频繁切换无成本。
        //
        // 如果未来游戏设计需要不同的 refund 策略（如"被硬控打断退 50% CD"），
        // 在 ForceTerminate 的 RollbackCleanup 分支或 SkillPipelineRunner.Interrupt
        // 路径加 MobaSkillRuntimeAccess.TryResetSkillCooldown 调用即可。
        private void ApplyConfiguredCooldown(int actorId, int skillId, in SkillCastPreparationResult prepared)
        {
            var slot = prepared.Request.SkillSlot;
            if (slot <= 0) return;

            var cooldownMs = ResolveConfiguredCooldownMs(skillId, prepared.Context?.SkillLevel ?? 0);
            if (cooldownMs <= 0) return;

            var now = MobaSkillRuntimeAccess.GetCurrentTimeMs(_time);
            if (!MobaSkillRuntimeAccess.TrySetActiveSkillCooldown(_actors, actorId, slot, skillId, now + cooldownMs, cooldownMs))
            {
                Log.Warning($"[SkillCastCoordinator] Failed to apply configured cooldown. actor={actorId}, slot={slot}, skillId={skillId}, cooldownMs={cooldownMs}.");
            }
        }

        private int ResolveConfiguredCooldownMs(int skillId, int skillLevel)
        {
            if (skillId <= 0) return 0;
            if (_services == null || !_services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return 0;
            if (!configs.TryGetSkill(skillId, out var skill) || skill == null) return 0;

            var cooldownMs = Math.Max(0, skill.CooldownMs);
            if (skill.LevelTableId <= 0 || skillLevel <= 0) return cooldownMs;
            if (!configs.TryGetSkillLevelTable(skill.LevelTableId, out var table) || table == null) return cooldownMs;

            var levels = table.Levels;
            var index = skillLevel - 1;
            if (levels == null || index < 0 || index >= levels.Count || levels[index] == null) return cooldownMs;

            return levels[index].CooldownMs > 0 ? levels[index].CooldownMs : cooldownMs;
        }

        public bool TryGetRunningBySlot(int actorId, int slot, out SkillPipelineRunner.RunningSnapshot snapshot)
        {
            return _runnerRegistry.TryGetLatestRunningBySlot(actorId, slot, out snapshot);
        }

        public bool TryGetRunningByInstanceId(int actorId, long instanceId, out SkillPipelineRunner.RunningSnapshot snapshot)
        {
            return _runnerRegistry.TryGetRunningByInstanceId(actorId, instanceId, out snapshot);
        }

        public void CancelAll(int actorId)
        {
            _runnerRegistry.GetOrCreate(actorId).CancelAll();
        }

        public bool CancelBySlot(int actorId, int slot)
        {
            return _runnerRegistry.TryCancelBySlot(actorId, slot);
        }

        public void CancelBySkillId(int actorId, int skillId)
        {
            _runnerRegistry.CancelBySkillId(actorId, skillId);
        }

        public void Step(int actorId)
        {
            _runnerRegistry.Step(actorId);
        }

        public void FillRunningSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            _runnerRegistry.FillRunningSnapshots(actorId, buffer);
        }

        public void FillEndedSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            _runnerRegistry.FillEndedSnapshots(actorId, buffer);
        }

        public void Dispose()
        {
            _runnerRegistry.Dispose();
        }
    }
}

