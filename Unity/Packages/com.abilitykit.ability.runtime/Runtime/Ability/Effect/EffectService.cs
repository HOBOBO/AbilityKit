using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Effect;

namespace AbilityKit.Ability.Share.Effect
{
    using AbilityKit.Ability.Share.Effect;
    public sealed class EffectService : IEffectEventSink
    {
        private readonly IEventBus _eventBus;
        private readonly TriggerRunner _runner;

        public EffectService(IEventBus eventBus, TriggerRunner runner)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public void Publish(string eventId, object payload = null, Action<PooledTriggerArgs> fillArgs = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            try
            {
                fillArgs?.Invoke(args);
                _eventBus.Publish(new TriggerEvent(eventId, payload, args));
            }
            catch
            {
                args.Dispose();
                throw;
            }
        }

        public bool EvaluateOnce(TriggerDef def, object source = null, object target = null, object payload = null, Action<PooledTriggerArgs> fillArgs = null, IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var instance = _runner.Compile(def);
            return EvaluateOnce(instance, source, target, payload, fillArgs, initialLocalVars);
        }

        public bool EvaluateOnce(TriggerInstance instance, object source = null, object target = null, object payload = null, Action<PooledTriggerArgs> fillArgs = null, IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var args = PooledTriggerArgs.Rent();
            try
            {
                fillArgs?.Invoke(args);
                return _runner.EvaluateOnce(instance, source, target, payload, args, initialLocalVars);
            }
            finally
            {
                args.Dispose();
            }
        }

        public bool RunOnce(TriggerDef def, object source = null, object target = null, object payload = null, Action<PooledTriggerArgs> fillArgs = null, IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var instance = _runner.Compile(def);
            return RunOnce(instance, source, target, payload, fillArgs, initialLocalVars);
        }

        public bool RunOnce(TriggerInstance instance, object source = null, object target = null, object payload = null, Action<PooledTriggerArgs> fillArgs = null, IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var args = PooledTriggerArgs.Rent();
            try
            {
                fillArgs?.Invoke(args);
                return _runner.RunOnce(instance, source, target, payload, args, initialLocalVars);
            }
            finally
            {
                args.Dispose();
            }
        }

        public void PublishEffectApply(in EffectExecutionContext context, EffectInstance instance)
        {
            PublishEffectEvent(EffectTriggering.Events.Apply, in context, instance);
        }

        public void PublishEffectTick(in EffectExecutionContext context, EffectInstance instance)
        {
            PublishEffectEvent(EffectTriggering.Events.Tick, in context, instance);
        }

        public void PublishEffectRemove(in EffectExecutionContext context, EffectInstance instance)
        {
            PublishEffectEvent(EffectTriggering.Events.Remove, in context, instance);
        }

        public void PublishEffectEvent(string eventId, in EffectExecutionContext context, EffectInstance instance)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var source = context.Source;
            var target = context.Target;
            var sourceContextId = context.SourceContextId;
            var services = context.Services;
            var spec = instance?.Spec;
            var instanceId = instance != null ? instance.Id : 0;
            var stackCount = instance != null ? instance.StackCount : 0;
            var elapsedSeconds = instance != null ? instance.ElapsedSeconds : 0f;
            var remainingSeconds = instance != null ? instance.RemainingSeconds : 0f;

            Publish(eventId, payload: instance, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;
                args[EffectTriggering.Args.OriginSource] = source;
                args[EffectTriggering.Args.OriginTarget] = target;
                args[EffectTriggering.Args.Spec] = spec;
                args[EffectTriggering.Args.Instance] = instance;
                args[EffectTriggering.Args.InstanceId] = instanceId;
                args[EffectTriggering.Args.StackCount] = stackCount;
                args[EffectTriggering.Args.ElapsedSeconds] = elapsedSeconds;
                args[EffectTriggering.Args.RemainingSeconds] = remainingSeconds;

                if (sourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = sourceContextId;

                    EffectOriginArgsHelper.FillFromServices(args, sourceContextId, services);
                }
            });
        }
    }
}
