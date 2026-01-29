using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillTimelinePhase : AbilityPipelinePhaseBase<SkillPipelineContext>
    {
        private readonly int _durationMs;
        private readonly SkillTimelineEventDTO[] _events;
        private readonly MobaEffectExecutionService _effects;

        private int _nextIndex;

        public SkillTimelinePhase(AbilityPipelinePhaseId phaseId, int durationMs, SkillTimelineEventDTO[] events, MobaEffectExecutionService effects)
            : base(phaseId)
        {
            _durationMs = durationMs;
            _events = events;
            _effects = effects;
        }

        protected override void OnEnter(SkillPipelineContext context)
        {
            _nextIndex = 0;
        }

        protected override void OnExecute(SkillPipelineContext context)
        {
            // wait for OnUpdate
        }

        public override void OnUpdate(SkillPipelineContext context, float deltaTime)
        {
            if (IsComplete) return;

            var elapsedMs = (int)(context.ElapsedTime * 1000f);

            if (_events != null)
            {
                while (_nextIndex < _events.Length)
                {
                    var e = _events[_nextIndex];
                    if (e == null)
                    {
                        _nextIndex++;
                        continue;
                    }

                    if (elapsedMs < e.AtMs) break;

                    var mode = EffectExecuteMode.InternalOnly;
                    var raw = e.ExecuteMode;
                    if (raw == (int)EffectExecuteMode.PublishEventOnly || raw == (int)EffectExecuteMode.InternalThenPublishEvent)
                    {
                        mode = (EffectExecuteMode)raw;
                    }
                    _effects?.Execute(e.EffectId, context, mode);
                    _nextIndex++;
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
                if (_events == null || _nextIndex >= _events.Length)
                {
                    Complete(context);
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            _nextIndex = 0;
        }
    }
}
