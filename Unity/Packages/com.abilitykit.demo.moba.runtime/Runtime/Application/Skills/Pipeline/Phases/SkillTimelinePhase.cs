using System;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba;
using AbilityKit.Core.Logging;
using AbilityKit.GameplayTags;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillTimelinePhase : SkillRollbackPhaseBase, IAbilityPipelinePhaseInstanceFactory<SkillPipelineContext>,
        IAbilityPhaseRollbackRebind<SkillPipelineContext>, IAbilityPhaseRollbackDetach
    {
        private readonly int _durationMs;
        private readonly SkillTimelineEventDTO[] _events;
        private readonly MobaEffectInvokerService _effects;
        // Q32.32 raw 时间累计；elapsedMs 用整数换算 (raw×1000)>>32。
        private long _elapsedRaw;
        private int _nextEventIndex;
        private readonly int _recoveryStartMs;
        private readonly int _gateTemplateId;
        private readonly int _gateEndMs;
        private bool _gateActive;
        private SkillPipelineContinuousRuntime _gateOwner;

        internal bool CanHandoff => !IsComplete && !_gateActive && _recoveryStartMs >= 0 &&
            ((_elapsedRaw * 1000L) >> 32) >= _recoveryStartMs &&
            _nextEventIndex >= (_events?.Length ?? 0);

        internal void FinishRecovery(SkillPipelineContext context)
        {
            if (!CanHandoff) throw new InvalidOperationException("Timeline is not in normal recovery.");
            Complete(context);
        }

        public SkillTimelinePhase(AbilityPipelinePhaseId phaseId, int durationMs, SkillTimelineEventDTO[] events,
            MobaEffectInvokerService effects, int recoveryStartMs = -1, int gateTemplateId = 0, int gateEndMs = 0)
            : base(phaseId)
        {
            _durationMs = durationMs;
            _events = events;
            _effects = effects;
            _recoveryStartMs = recoveryStartMs;
            _gateTemplateId = gateTemplateId;
            _gateEndMs = gateEndMs;
            SkillRecoveryContract.ValidateTimelineGate(durationMs, recoveryStartMs, gateTemplateId, gateEndMs);
            if (recoveryStartMs < -1 || recoveryStartMs >= 0 && recoveryStartMs >= durationMs)
                throw new ArgumentException("Recovery entry must be within the full Timeline duration.");
            if (recoveryStartMs >= 0 && events != null)
                foreach (var effect in events)
                    if (effect != null && effect.AtMs > recoveryStartMs)
                        throw new ArgumentException("Recovery cannot skip unsubmitted effects.");
        }

        protected override void OnEnter(SkillPipelineContext context)
        {
            _elapsedRaw = 0L;
            _nextEventIndex = 0;
            context?.SetTimelineNextEventIndex(0);
            if (_gateTemplateId != 0) BindGate(context, restoring: false);
        }

        protected override void OnExecute(SkillPipelineContext context)
        {
            OnUpdate(context, 0f);
        }

        public override void OnUpdate(SkillPipelineContext context, float deltaTime)
        {
            if (IsComplete) return;

            if (deltaTime > 0f)
            {
                _elapsedRaw += AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            }

            var nextIndex = _nextEventIndex;
            var elapsedMs = (int)((_elapsedRaw * 1000L) >> 32);

            if (_events != null)
            {
                while (nextIndex < _events.Length)
                {
                    var e = _events[nextIndex];
                    if (e == null)
                    {
                        nextIndex++;
                        context.SetTimelineNextEventIndex(nextIndex);
                        continue;
                    }

                    if (elapsedMs < e.AtMs) break;
                    if (_gateActive && e.AtMs > _gateEndMs) ReleaseGate(restoring: false);

                    var raw = e.ExecuteMode;
                    if (raw != (int)EffectExecuteMode.InternalOnly)
                    {
                        throw new InvalidOperationException($"Unsupported timeline effect execute mode. phase={PhaseId.Value}, eventIndex={nextIndex}, effectId={e.EffectId}, executeMode={raw}, skillId={context?.SkillId ?? 0}");
                    }

                    if (e.EffectId <= 0)
                    {
                        throw new InvalidOperationException($"Invalid timeline effect id. phase={PhaseId.Value}, eventIndex={nextIndex}, effectId={e.EffectId}, skillId={context?.SkillId ?? 0}");
                    }

                    var effects = ResolveEffects(context);
                    if (effects == null)
                    {
                        throw new InvalidOperationException($"Skill timeline requires MobaEffectInvokerService. phase={PhaseId.Value}, eventIndex={nextIndex}, effectId={e.EffectId}, skillId={context?.SkillId ?? 0}");
                    }

                    var outcome = effects.ExecuteWithResult(e.EffectId, context);
                    if (outcome == MobaEffectExecutionOutcome.Failed && e.AbortOnFailure)
                    {
                        context.FailReason = $"Skill timeline effect failed: {e.EffectId}.";
                        context.IsAborted = true;
                        ReleaseGate(restoring: false);
                        return;
                    }

                    nextIndex++;
                    _nextEventIndex = nextIndex;
                    context.SetTimelineNextEventIndex(nextIndex);
                }
            }

            if (_gateActive && elapsedMs >= _gateEndMs) ReleaseGate(restoring: false);

            if (_durationMs > 0)
            {
                if (elapsedMs >= _durationMs)
                {
                    Complete(context);
                }
            }
            else
            {
                if (_events == null || nextIndex >= _events.Length)
                {
                    Complete(context);
                }
            }
        }

        public override void Reset()
        {
            ReleaseGate(restoring: false);
            base.Reset();
            _elapsedRaw = 0L;
            _nextEventIndex = 0;
        }

        public IAbilityPipelinePhase<SkillPipelineContext> CreateRunPhase()
            => new SkillTimelinePhase(PhaseId, _durationMs, _events, _effects, _recoveryStartMs, _gateTemplateId, _gateEndMs);

        protected override void OnExit(SkillPipelineContext context) => ReleaseGate(restoring: false);

        public void DetachForRollback() => ReleaseGate(restoring: true);

        public void RebindAfterRollback(SkillPipelineContext context)
        {
            if (_gateActive) BindGate(context, restoring: true);
        }

        private void BindGate(SkillPipelineContext context, bool restoring)
        {
            if (context?.PipelineContinuous == null || context.WorldServices == null ||
                !context.WorldServices.TryResolve<IMobaContinuousTagTemplateRegistry>(out var templates) ||
                !templates.TryGet(_gateTemplateId, out var requirements) || !IsCastGateTemplate(requirements))
                throw new InvalidOperationException($"Invalid Timeline cast gate template {_gateTemplateId} for {PhaseId.Value}.");
            _gateOwner = context.PipelineContinuous;
            _gateActive = true;
            _gateOwner.SetTimelineGrant(this, requirements.ApplicationTags, restoring);
        }

        private void ReleaseGate(bool restoring)
        {
            var owner = _gateOwner;
            _gateOwner = null;
            _gateActive = false;
            owner?.SetTimelineGrant(this, null, restoring);
        }

        internal static bool IsCastGateTemplate(ContinuousTagRequirements requirements)
        {
            if (requirements?.ApplicationTags == null || requirements.ApplicationTags.IsEmpty ||
                requirements.ExecutionBlockedResultMask != 0 || !IsEmpty(requirements.ActivationRequired) ||
                !IsEmpty(requirements.RemovalRequired) || !IsEmpty(requirements.OngoingRequired)) return false;
            foreach (var tag in requirements.ApplicationTags)
                if (tag.TagName != "State.Casting.CannotMove" && tag.TagName != "State.Casting.CannotTurn") return false;
            return true;
        }

        private static bool IsEmpty(GameplayTagRequirements requirements) =>
            (requirements.Required == null || requirements.Required.IsEmpty) &&
            (requirements.Blocked == null || requirements.Blocked.IsEmpty) && !requirements.Query.IsValid;

        public override PipelinePhaseRollbackState CaptureRollbackState()
        {
            var state = base.CaptureRollbackState();
            state.Longs = new[] { _elapsedRaw };
            state.Integers = _gateTemplateId == 0 ? new[] { _nextEventIndex } :
                new[] { _nextEventIndex, _gateActive ? 1 : 0 };
            return state;
        }

        public override void ValidateRollbackState(PipelinePhaseRollbackState state)
        {
            base.ValidateRollbackState(state);
            if (state?.Longs == null || state.Longs.Length != 1 || state.Longs[0] < 0 ||
                state.Integers == null || state.Integers.Length != (_gateTemplateId == 0 ? 1 : 2) ||
                state.Integers[0] < 0 || state.Integers[0] > (_events?.Length ?? 0) ||
                _gateTemplateId != 0 && (state.Integers[1] is < 0 or > 1 || state.Integers[1] == 1 &&
                    (state.IsComplete || ((state.Longs[0] * 1000L) >> 32) >= _gateEndMs)))
                throw new InvalidOperationException("Invalid skill timeline rollback state.");
        }

        public override void RestoreRollbackState(PipelinePhaseRollbackState state)
        {
            ValidateRollbackState(state);
            base.RestoreRollbackState(state);
            _elapsedRaw = state.Longs[0];
            _nextEventIndex = state.Integers[0];
            _gateActive = _gateTemplateId != 0 && state.Integers[1] == 1;
        }

        private MobaEffectInvokerService ResolveEffects(SkillPipelineContext context)
        {
            if (_effects != null) return _effects;
            if (context?.WorldServices == null) return null;
            return context.WorldServices.TryResolve<MobaEffectInvokerService>(out var effects) ? effects : null;
        }
    }
}
