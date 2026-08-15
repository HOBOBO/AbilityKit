using System;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba;
using AbilityKit.Core.Logging;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillTimelinePhase : AbilityPipelinePhaseBase<SkillPipelineContext>
    {
        private readonly int _durationMs;
        private readonly SkillTimelineEventDTO[] _events;
        private readonly MobaEffectInvokerService _effects;
        // Q32.32 raw 时间累计；elapsedMs 用整数换算 (raw×1000)>>32。
        private long _elapsedRaw;
        private int _nextEventIndex;

        public SkillTimelinePhase(AbilityPipelinePhaseId phaseId, int durationMs, SkillTimelineEventDTO[] events, MobaEffectInvokerService effects)
            : base(phaseId)
        {
            _durationMs = durationMs;
            _events = events;
            _effects = effects;
        }

        protected override void OnEnter(SkillPipelineContext context)
        {
            _elapsedRaw = 0L;
            _nextEventIndex = 0;
            context?.SetTimelineNextEventIndex(0);
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

                    effects.Execute(e.EffectId, context);

                    nextIndex++;
                    _nextEventIndex = nextIndex;
                    context.SetTimelineNextEventIndex(nextIndex);
                }
            }

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
            base.Reset();
            _elapsedRaw = 0L;
            _nextEventIndex = 0;
        }

        private MobaEffectInvokerService ResolveEffects(SkillPipelineContext context)
        {
            if (_effects != null) return _effects;
            if (context?.WorldServices == null) return null;
            return context.WorldServices.TryResolve<MobaEffectInvokerService>(out var effects) ? effects : null;
        }
    }
}
