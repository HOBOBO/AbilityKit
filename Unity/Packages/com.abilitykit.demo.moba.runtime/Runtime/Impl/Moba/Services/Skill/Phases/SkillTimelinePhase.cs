using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillTimelinePhase : AbilityPipelinePhaseBase<SkillPipelineContext>
    {
        private readonly int _durationMs;
        private readonly SkillTimelineEventDTO[] _events;
        private readonly MobaEffectInvokerService _effects;

        private int _nextIndex;

        public SkillTimelinePhase(AbilityPipelinePhaseId phaseId, int durationMs, SkillTimelineEventDTO[] events, MobaEffectInvokerService effects)
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

            try { context?.SetData(MobaSkillPipelineSharedKeys.TimelineNextEventIndex, _nextIndex); }
            catch { }

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

                    var raw = e.ExecuteMode;
                    if (raw == (int)EffectExecuteMode.PublishEventOnly || raw == (int)EffectExecuteMode.InternalThenPublishEvent)
                    {
                        Log.Warning($"[SkillTimelinePhase] ExecuteMode={raw} is not supported (legacy publish removed). effectId={e.EffectId}");
                    }

                    _effects?.Execute(e.EffectId, context);
                    _nextIndex++;

                    try { context?.SetData(MobaSkillPipelineSharedKeys.TimelineNextEventIndex, _nextIndex); }
                    catch { }
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
