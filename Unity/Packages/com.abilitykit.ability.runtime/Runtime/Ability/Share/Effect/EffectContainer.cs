using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Share.ECS;

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

            var inst = new EffectInstance(_nextId++, spec);

            if (context.SourceContextId != 0)
            {
                try
                {
                    var sourceContextId = context.SourceContextId;
                    try
                    {
                        if (context.Services != null)
                        {
                            var svc = context.Services.GetService(typeof(EffectSourceRegistry));
                            if (svc is EffectSourceRegistry r)
                            {
                                var frame = 0;
                                try { frame = context.Time != null ? context.Time.Frame.Value : 0; }
                                catch { frame = 0; }

                                var sourceActorId = 0;
                                var targetActorId = 0;
                                try
                                {
                                    if (context.Source is IUnitFacade s && s.Id.IsValid) sourceActorId = s.Id.ActorId;
                                }
                                catch
                                {
                                }
                                try
                                {
                                    if (context.Target is IUnitFacade t && t.Id.IsValid) targetActorId = t.Id.ActorId;
                                }
                                catch
                                {
                                }

                                var childId = r.CreateChild(
                                    parentContextId: sourceContextId,
                                    kind: AbilityKit.Ability.Impl.Moba.EffectSourceKind.Effect,
                                    configId: inst.Id,
                                    sourceActorId: sourceActorId,
                                    targetActorId: targetActorId,
                                    frame: frame);

                                if (childId != 0) sourceContextId = childId;
                            }
                        }
                    }
                    catch
                    {
                    }

                    inst.SetState(EffectSourceKeys.SourceContextId, sourceContextId);
                }
                catch
                {
                }
            }

            PublishDefaultEvent(context.EventBus, EffectTriggering.Events.Apply, in context, inst);

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

            (spec.Cue ?? NullGameplayEffectCue.Instance).OnActive(in context, inst);

            _active.Add(inst);

            if (spec.DurationPolicy == EffectDurationPolicy.Instant)
            {
                Remove(inst.Id, in context);
                return inst;
            }

            if (spec.PeriodSeconds > 0f && spec.ExecutePeriodicOnApply)
            {
                TickInstance(inst, in context);
            }

            if (spec.PeriodSeconds > 0f)
            {
                inst.NextTickInSeconds = System.Math.Max(0f, spec.PeriodSeconds);
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

            var dt = context.Time.DeltaTime;
            if (dt <= 0f) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var inst = _active[i];
                if (inst == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                var spec = inst.Spec;

                inst.ElapsedSeconds += dt;

                (spec.Cue ?? NullGameplayEffectCue.Instance).WhileActive(in context, inst);

                if (spec.DurationPolicy == EffectDurationPolicy.Duration)
                {
                    inst.RemainingSeconds -= dt;
                }

                if (spec.PeriodSeconds > 0f)
                {
                    inst.NextTickInSeconds -= dt;
                    while (inst.NextTickInSeconds <= 0f)
                    {
                        TickInstance(inst, in context);
                        inst.NextTickInSeconds += System.Math.Max(0.0001f, spec.PeriodSeconds);
                    }
                }

                if (spec.DurationPolicy == EffectDurationPolicy.Duration && inst.RemainingSeconds <= 0f)
                {
                    RemoveAt(i, in context);
                }
            }
        }

        private void TickInstance(EffectInstance inst, in EffectExecutionContext context)
        {
            PublishDefaultEvent(context.EventBus, EffectTriggering.Events.Tick, in context, inst);

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

            var endContextId = 0L;
            try
            {
                if (inst.TryGetState<long>(EffectSourceKeys.SourceContextId, out var scidL)) endContextId = scidL;
                else if (inst.TryGetState<int>(EffectSourceKeys.SourceContextId, out var scidI)) endContextId = scidI;
            }
            catch
            {
                endContextId = 0L;
            }

            PublishDefaultEvent(context.EventBus, EffectTriggering.Events.Remove, in context, inst);

            (inst.Spec.Cue ?? NullGameplayEffectCue.Instance).OnRemove(in context, inst);

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

            if (endContextId != 0)
            {
                try
                {
                    if (context.Services != null)
                    {
                        var svc = context.Services.GetService(typeof(EffectSourceRegistry));
                        if (svc is EffectSourceRegistry r)
                        {
                            var frame = 0;
                            try { frame = context.Time != null ? context.Time.Frame.Value : 0; }
                            catch { frame = 0; }

                            var reason = AbilityKit.Ability.Impl.Moba.EffectSourceEndReason.Completed;
                            if (inst.Spec != null && inst.Spec.DurationPolicy == EffectDurationPolicy.Duration && inst.RemainingSeconds <= 0f)
                            {
                                reason = AbilityKit.Ability.Impl.Moba.EffectSourceEndReason.Expired;
                            }

                            r.End(endContextId, frame, reason);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void PublishDefaultEvent(IEventBus bus, string eventId, in EffectExecutionContext context, EffectInstance instance)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            if (context.Services != null)
            {
                try
                {
                    var sw = context.Services.GetService(typeof(IEffectTriggeringSwitch));
                    if (sw is IEffectTriggeringSwitch s && !s.Enabled)
                    {
                        return;
                    }
                }
                catch
                {
                }
            }

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = context.Source;
            args[EffectTriggering.Args.Target] = context.Target;
            args[EffectTriggering.Args.Spec] = instance?.Spec;
            args[EffectTriggering.Args.Instance] = instance;
            args[EffectTriggering.Args.InstanceId] = instance != null ? instance.Id : 0;
            args[EffectTriggering.Args.StackCount] = instance != null ? instance.StackCount : 0;
            args[EffectTriggering.Args.ElapsedSeconds] = instance != null ? instance.ElapsedSeconds : 0f;
            args[EffectTriggering.Args.RemainingSeconds] = instance != null ? instance.RemainingSeconds : 0f;

            long sourceContextId = context.SourceContextId;
            if (sourceContextId == 0 && instance != null)
            {
                try
                {
                    if (instance.TryGetState<long>(EffectSourceKeys.SourceContextId, out var scidL)) sourceContextId = scidL;
                    else if (instance.TryGetState<int>(EffectSourceKeys.SourceContextId, out var scidI)) sourceContextId = scidI;
                }
                catch
                {
                }
            }

            if (sourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = sourceContextId;

                EffectOriginArgsHelper.FillFromServices(args, sourceContextId, context.Services);
            }

            bus.Publish(new TriggerEvent(eventId, instance, args));
        }
    }
}
