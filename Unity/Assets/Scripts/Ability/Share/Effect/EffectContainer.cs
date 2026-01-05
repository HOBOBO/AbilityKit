using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Effect
{
    public sealed class EffectContainer
    {
        private readonly List<EffectInstance> _active = new List<EffectInstance>(16);
        private int _nextId = 1;

        public IReadOnlyList<EffectInstance> Active => _active;

        public EffectInstance Apply(GameplayEffectSpec spec, in EffectExecutionContext context)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (context.Time == null) throw new ArgumentNullException(nameof(context.Time));

            var targetTags = context.TargetTags;
            if (spec.ApplicationRequirements.Required != null || spec.ApplicationRequirements.Blocked != null)
            {
                if (targetTags == null) return null;
                if (!spec.ApplicationRequirements.IsSatisfiedBy(targetTags)) return null;
            }

            var startFrame = context.Time.Frame.Value;
            var endFrame = int.MaxValue;
            if (spec.DurationPolicy == EffectDurationPolicy.Duration)
            {
                endFrame = startFrame + System.Math.Max(0, spec.DurationFrames);
            }

            var inst = new EffectInstance(_nextId++, spec, startFrame, endFrame);

            if (targetTags != null && spec.GrantedTags != null)
            {
                foreach (var tag in spec.GrantedTags)
                {
                    targetTags.Add(tag);
                }
            }

            var components = spec.Components;
            for (int i = 0; i < components.Count; i++)
            {
                components[i]?.OnApply(in context, inst);
            }

            _active.Add(inst);

            if (spec.DurationPolicy == EffectDurationPolicy.Instant)
            {
                Remove(inst.Id, in context);
                return inst;
            }

            if (spec.PeriodFrames > 0 && spec.ExecutePeriodicOnApply)
            {
                TickInstance(inst, in context, startFrame);
            }

            if (spec.PeriodFrames > 0)
            {
                inst.NextTickFrame = startFrame + System.Math.Max(1, spec.PeriodFrames);
            }

            return inst;
        }

        public bool Remove(int instanceId, in EffectExecutionContext context)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var inst = _active[i];
                if (inst == null || inst.Id != instanceId) continue;

                RemoveAt(i, in context);
                return true;
            }

            return false;
        }

        public void Step(in EffectExecutionContext context)
        {
            if (context.Time == null) throw new ArgumentNullException(nameof(context.Time));

            var frame = context.Time.Frame.Value;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var inst = _active[i];
                if (inst == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                var spec = inst.Spec;

                if (spec.PeriodFrames > 0 && frame >= inst.NextTickFrame)
                {
                    TickInstance(inst, in context, frame);

                    var period = System.Math.Max(1, spec.PeriodFrames);
                    while (inst.NextTickFrame <= frame)
                    {
                        inst.NextTickFrame += period;
                    }
                }

                if (spec.DurationPolicy == EffectDurationPolicy.Duration && frame >= inst.EndFrame)
                {
                    RemoveAt(i, in context);
                }
            }
        }

        private void TickInstance(EffectInstance inst, in EffectExecutionContext context, int frame)
        {
            var components = inst.Spec.Components;
            for (int i = 0; i < components.Count; i++)
            {
                components[i]?.OnTick(in context, inst);
            }
        }

        private void RemoveAt(int index, in EffectExecutionContext context)
        {
            var inst = _active[index];
            _active.RemoveAt(index);
            if (inst == null) return;

            var components = inst.Spec.Components;
            for (int i = 0; i < components.Count; i++)
            {
                components[i]?.OnRemove(in context, inst);
            }

            var targetTags = context.TargetTags;
            if (targetTags != null && inst.Spec.GrantedTags != null)
            {
                foreach (var tag in inst.Spec.GrantedTags)
                {
                    targetTags.Remove(tag);
                }
            }
        }
    }
}
