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
using AbilityKit.Demo.Moba.Diagnostics;
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
            _ = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _preparation = new SkillCastPreparationService(_services, _eventBus, _units, _actors, _library);
            _policyResolver = new SkillCastPolicyResolver(_services);
            _runnerRegistry = new SkillRunnerRegistry(_clock, diagnostics, exceptions, skillLogger ?? SkillLogger.Instance);
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
                var failure = new MobaSkillCastFailure(
                    "Preparation",
                    null,
                    SkillFailureCodes.Cast.MissingSkill,
                    "Skill not found in slot.");
                var result = MobaSkillCastResult.Failed("Skill not found in slot.", in failure);
                CollectSkillFailure(actorId, skillId: 0, slot, targetActorId: 0, in result);
                return result;
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
            var result = ValidateSkillInput(actorId, in evt);
            if (result.Success)
            {
                result = DispatchSkillInputPhase(actorId, in evt);
            }

            if (!result.Success &&
                string.Equals(result.Failure.Source, "Input", StringComparison.Ordinal))
            {
                var failure = result.Failure;
                CollectSkillFailure(
                    actorId,
                    ResolveSkillId(actorId, evt.Slot),
                    evt.Slot,
                    evt.TargetActorId,
                    in failure,
                    runtimeHandle: default);
            }

            return result;
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
                var failure = new MobaSkillCastFailure(
                    "Preparation",
                    null,
                    SkillFailureCodes.Cast.MissingSkill,
                    "Skill not found in slot.");
                var result = MobaSkillCastResult.Failed("Skill not found in slot.", in failure);
                CollectSkillFailure(actorId, skillId: 0, slot, targetActorId, in result);
                return result;
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
            if (!TryValidateCombatRules(actorId, out var combatFailure, out var combatMessage))
            {
                var rejected = MobaSkillCastResult.Failed(combatMessage, in combatFailure);
                CollectSkillFailure(actorId, resolvedSkillId, slot, targetActorId, in rejected);
                return rejected;
            }

            var input = new SkillCastPreparationInput(actorId, resolvedSkillId, slot, in aimPos, in aimDir, hasAim, targetActorId);
            var prepared = _preparation.Prepare(in input);
            MobaSkillCastResult result;
            if (!prepared.Success)
            {
                var failure = prepared.Failure;
                result = MobaSkillCastResult.Failed(prepared.FailReason, in failure);
            }
            else
            {
                result = StartPreparedCast(actorId, resolvedSkillId, in prepared);
            }

            CollectSkillFailure(actorId, resolvedSkillId, slot, targetActorId, in result);
            return result;
        }

        private int ResolveSkillId(int actorId, int slot)
        {
            return _loadout.TryGetSkillId(actorId, slot, out var skillId) ? skillId : 0;
        }

        private void CollectSkillFailure(
            int actorId,
            int skillId,
            int slot,
            int targetActorId,
            in MobaSkillCastResult result)
        {
            if (result.Success) return;

            var failure = result.Failure;
            var runtimeHandle = result.RuntimeHandle;
            CollectSkillFailure(
                actorId,
                skillId,
                slot,
                targetActorId,
                in failure,
                in runtimeHandle);
        }

        private void CollectSkillFailure(
            int actorId,
            int skillId,
            int slot,
            int targetActorId,
            in MobaSkillCastFailure failure,
            in MobaSkillCastRuntimeHandle runtimeHandle)
        {
            if (!failure.HasValue ||
                _services == null ||
                !_services.TryResolve<IMobaBattleDiagnosticEventSink>(out var sink) ||
                sink == null)
            {
                return;
            }

            var payloadData = new BattleDiagnosticSkillFailurePayload(
                slot,
                failure.Source,
                failure.Stage,
                failure.Code,
                failure.Message);
            var payload = BattleDiagnosticEventPayload.FromSkillFailure(in payloadData);
            var runtime = runtimeHandle.IsValid
                ? new BattleDiagnosticRuntimeHandle(runtimeHandle.RuntimeId, runtimeHandle.Generation)
                : default;
            var rootContextId = runtimeHandle.IsValid ? runtimeHandle.RootTraceContextId : 0L;
            var summary = $"code={payloadData.Code}, source={payloadData.Source}, stage={payloadData.Stage}, slot={slot}";
            if (!string.IsNullOrEmpty(payloadData.Message)) summary += $", message={payloadData.Message}";
            var draft = new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.SkillFailure,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Failed,
                actorId,
                targetActorId,
                skillId,
                rootContextId,
                rootContextId,
                runtime,
                payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                summary: summary,
                payload: payload);
            sink.TryCollect(in draft);
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

            // Keep a post-preparation gate as a race-safe fallback. Any rejection after
            // preparation must release the formal runtime, which owns the root trace.
            if (!TryValidateCombatRules(actorId, out var combatFailure, out var combatMessage))
            {
                prepared.Runtimes.ForceTerminate(in ctx.RuntimeHandle, MobaSkillRuntimeEndReason.RollbackCleanup);
                return new MobaSkillCastResult(false, combatMessage, in ctx.RuntimeHandle, in combatFailure);
            }

            var req = prepared.Request;
            var runner = _runnerRegistry.GetOrCreate(actorId);
            var policy = _policyResolver.Resolve(skillId, _castPolicy);
            var startResult = runner.TryStart(
                prepared.PreCastConfig,
                prepared.PreCastPhases,
                prepared.CastConfig,
                prepared.CastPhases,
                abilityInstance: this,
                in req,
                ctx,
                in policy);
            var failure = MobaSkillCastFailure.None;
            if (!startResult.Success)
            {
                failure = SkillResultFactory.PipelineStartFailure(in startResult);
                prepared.Runtimes.ForceTerminate(in ctx.RuntimeHandle, MobaSkillRuntimeEndReason.RollbackCleanup);
            }

            return MobaSkillCastResult.From(startResult.Success, startResult.FailReason, in ctx.RuntimeHandle, in failure);
        }

        private bool TryValidateCombatRules(
            int actorId,
            out MobaSkillCastFailure failure,
            out string message)
        {
            failure = MobaSkillCastFailure.None;
            message = null;
            if (_services == null ||
                !_services.TryResolve<MobaCombatRulesService>(out var combatRules) ||
                combatRules == null)
            {
                return true;
            }

            var result = combatRules.CanCastSkill(actorId);
            if (result.Passed) return true;

            message = result.Message;
            failure = new MobaSkillCastFailure(
                "CombatRules",
                "CastGate",
                $"combat.{result.Failure}",
                result.Message);
            return false;
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
            _runnerRegistry.CancelAll(actorId);
        }

        public void RemoveActor(int actorId)
        {
            _runnerRegistry.CancelAndRemove(actorId, MobaSkillRuntimeEndReason.OwnerRemoved);
            _preparation.RemoveActor(actorId);
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

