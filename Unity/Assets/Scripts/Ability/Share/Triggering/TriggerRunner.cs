using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggerRunner
    {
        private static readonly ObjectPool<Dictionary<string, object>> _localVarsPool = Pools.GetPool(
            createFunc: () => new Dictionary<string, object>(StringComparer.Ordinal),
            onRelease: dict => dict.Clear(),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private readonly IEventBus _eventBus;
        private readonly TriggerCompiler _compiler;
        private readonly ITriggerContextFactory _contextFactory;

        private readonly Dictionary<string, List<IEventHandler>> _handlersByEventId = new Dictionary<string, List<IEventHandler>>(StringComparer.Ordinal);

        public TriggerRunner(IEventBus eventBus, TriggerRegistry registry, ITriggerContextFactory contextFactory)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _compiler = new TriggerCompiler(registry);
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public TriggerInstance Compile(TriggerDef def)
        {
            return _compiler.Compile(def);
        }

        public IEventSubscription Register(TriggerDef def)
        {
            var instance = _compiler.Compile(def);
            return Register(instance);
        }

        public IEventSubscription Register(TriggerDef def, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars)
        {
            var instance = _compiler.Compile(def);
            return Register(instance, initialLocalVars);
        }

        public bool EvaluateOnce(TriggerDef def, object source = null, object target = null, object payload = null, System.Collections.Generic.IReadOnlyDictionary<string, object> args = null, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return EvaluateOnce(_compiler.Compile(def), source, target, payload, args, initialLocalVars);
        }

        public bool EvaluateOnce(TriggerInstance instance, object source = null, object target = null, object payload = null, System.Collections.Generic.IReadOnlyDictionary<string, object> args = null, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var localVars = _localVarsPool.Get();
            try
            {
                if (initialLocalVars != null)
                {
                    foreach (var kv in initialLocalVars)
                    {
                        if (kv.Key == null) continue;
                        localVars[kv.Key] = kv.Value;
                    }
                }

                var evt = new TriggerEvent(instance.EventId, payload, args);
                // WorldTriggerContextFactory reads source/target from evt.Args, so ensure they exist.
                if (evt.Args is Dictionary<string, object> dictArgs)
                {
                    dictArgs[EffectTriggering.Args.Source] = source;
                    dictArgs[EffectTriggering.Args.Target] = target;
                }
                else if (evt.Args != null)
                {
                    // Args is read-only; fall back to putting source/target into local vars.
                    localVars[EffectTriggering.Args.Source] = source;
                    localVars[EffectTriggering.Args.Target] = target;
                }
                else
                {
                    localVars[EffectTriggering.Args.Source] = source;
                    localVars[EffectTriggering.Args.Target] = target;
                }

                return ExecuteInternal(instance, in evt, localVars, runActions: false);
            }
            finally
            {
                _localVarsPool.Release(localVars);
            }
        }

        public bool RunOnce(TriggerDef def, object source = null, object target = null, object payload = null, System.Collections.Generic.IReadOnlyDictionary<string, object> args = null, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return RunOnce(_compiler.Compile(def), source, target, payload, args, initialLocalVars);
        }

        public bool RunOnce(TriggerInstance instance, object source = null, object target = null, object payload = null, System.Collections.Generic.IReadOnlyDictionary<string, object> args = null, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var localVars = _localVarsPool.Get();
            try
            {
                if (initialLocalVars != null)
                {
                    foreach (var kv in initialLocalVars)
                    {
                        if (kv.Key == null) continue;
                        localVars[kv.Key] = kv.Value;
                    }
                }

                var evt = new TriggerEvent(instance.EventId, payload, args);
                if (evt.Args is Dictionary<string, object> dictArgs)
                {
                    dictArgs[EffectTriggering.Args.Source] = source;
                    dictArgs[EffectTriggering.Args.Target] = target;
                }
                else if (evt.Args != null)
                {
                    localVars[EffectTriggering.Args.Source] = source;
                    localVars[EffectTriggering.Args.Target] = target;
                }
                else
                {
                    localVars[EffectTriggering.Args.Source] = source;
                    localVars[EffectTriggering.Args.Target] = target;
                }

                return ExecuteInternal(instance, in evt, localVars, runActions: true);
            }
            finally
            {
                _localVarsPool.Release(localVars);
            }
        }

        private bool ExecuteInternal(TriggerInstance instance, in TriggerEvent evt, Dictionary<string, object> localVars, bool runActions)
        {
            var context = _contextFactory.CreateContext(in evt, localVars);
            try
            {
                context.Event = evt;

                for (int i = 0; i < instance.Conditions.Count; i++)
                {
                    if (!instance.Conditions[i].Evaluate(context)) return false;
                }

                if (!runActions) return true;

                for (int i = 0; i < instance.Actions.Count; i++)
                {
                    var a = instance.Actions[i];
                    if (a is ITriggerActionV2 v2)
                    {
                        var running = v2.Start(context);
                        if (running != null)
                        {
                            var runner = TriggerEventHandler.GetRunner(context);
                            if (TryGetOwnerKey(context.Event.Args, out var ownerKey))
                            {
                                runner?.Add(running, ownerKey);
                            }
                            else
                            {
                                runner?.Add(running, context.Event.Payload ?? context.Source);
                            }
                        }
                        continue;
                    }

                    a.Execute(context);
                }

                return true;
            }
            finally
            {
                TriggerContext.Return(context);
            }
        }

        public IEventSubscription Register(TriggerInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var h = new TriggerEventHandler(instance, _contextFactory);
            AddLocalHandler(instance.EventId, h);
            var sub = _eventBus.Subscribe(instance.EventId, h);
            return new Subscription(this, instance.EventId, h, sub);
        }

        public IEventSubscription Register(TriggerInstance instance, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var h = new TriggerEventHandler(instance, _contextFactory, initialLocalVars);
            AddLocalHandler(instance.EventId, h);
            var sub = _eventBus.Subscribe(instance.EventId, h);
            return new Subscription(this, instance.EventId, h, sub);
        }

        public void Dispatch(in TriggerEvent evt, bool disposeArgs = true)
        {
            if (evt.Id == null) return;

            try
            {
                if (_handlersByEventId.TryGetValue(evt.Id, out var handlers))
                {
                    if (handlers.Count == 0) return;

                    if (handlers.Count == 1)
                    {
                        try
                        {
                            handlers[0]?.Handle(in evt);
                        }
                        catch
                        {
                        }
                        return;
                    }

                    // Snapshot to avoid issues if handlers list is modified during dispatch.
                    var snapshot = new List<IEventHandler>(handlers);
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        try
                        {
                            snapshot[i]?.Handle(in evt);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                if (disposeArgs && evt.Args is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void AddLocalHandler(string eventId, IEventHandler handler)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlersByEventId.TryGetValue(eventId, out var handlers))
            {
                handlers = new List<IEventHandler>(4);
                _handlersByEventId[eventId] = handlers;
            }

            handlers.Add(handler);
        }

        private void RemoveLocalHandler(string eventId, IEventHandler handler)
        {
            if (eventId == null || handler == null) return;

            if (_handlersByEventId.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlersByEventId.Remove(eventId);
                }
            }
        }

        private sealed class Subscription : IEventSubscription
        {
            private readonly TriggerRunner _runner;
            private readonly string _eventId;
            private readonly IEventHandler _handler;
            private IEventSubscription _eventBusSub;

            public Subscription(TriggerRunner runner, string eventId, IEventHandler handler, IEventSubscription eventBusSub)
            {
                _runner = runner;
                _eventId = eventId;
                _handler = handler;
                _eventBusSub = eventBusSub;
            }

            public void Unsubscribe()
            {
                var sub = _eventBusSub;
                if (sub == null) return;
                _eventBusSub = null;

                try
                {
                    sub.Unsubscribe();
                }
                finally
                {
                    _runner.RemoveLocalHandler(_eventId, _handler);
                }
            }
        }

        private sealed class TriggerEventHandler : IEventHandler, IDisposable
        {
            private readonly TriggerInstance _trigger;
            private readonly ITriggerContextFactory _contextFactory;
            private readonly Dictionary<string, object> _localVars;

            public TriggerEventHandler(TriggerInstance trigger, ITriggerContextFactory contextFactory)
            {
                _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
                _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
                _localVars = _localVarsPool.Get();
            }

            public TriggerEventHandler(TriggerInstance trigger, ITriggerContextFactory contextFactory, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars)
                : this(trigger, contextFactory)
            {
                if (initialLocalVars == null) return;
                foreach (var kv in initialLocalVars)
                {
                    if (kv.Key == null) continue;
                    _localVars[kv.Key] = kv.Value;
                }
            }

            public void Handle(in TriggerEvent evt)
            {
                var context = _contextFactory.CreateContext(in evt, _localVars);
                try
                {
                    context.Event = evt;

                    for (int i = 0; i < _trigger.Conditions.Count; i++)
                    {
                        if (!_trigger.Conditions[i].Evaluate(context)) return;
                    }

                    for (int i = 0; i < _trigger.Actions.Count; i++)
                    {
                        var a = _trigger.Actions[i];
                        if (a is ITriggerActionV2 v2)
                        {
                            var running = v2.Start(context);
                            if (running != null)
                            {
                                var runner = GetRunner(context);
                                if (TryGetOwnerKey(context.Event.Args, out var ownerKey))
                                {
                                    runner?.Add(running, ownerKey);
                                }
                                else
                                {
                                    runner?.Add(running, context.Event.Payload ?? context.Source);
                                }
                            }
                            continue;
                        }

                        a.Execute(context);
                    }
                }
                finally
                {
                    TriggerContext.Return(context);
                }
            }

            public void Dispose()
            {
                _localVarsPool.Release(_localVars);
            }

            public static ITriggerActionRunner GetRunner(TriggerContext context)
            {
                var sp = context?.Services;
                if (sp == null) return null;

                try
                {
                    return sp.GetService(typeof(ITriggerActionRunner)) as ITriggerActionRunner;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static bool TryGetOwnerKey(System.Collections.Generic.IReadOnlyDictionary<string, object> args, out long ownerKey)
        {
            ownerKey = 0;
            if (args == null) return false;

            if (!args.TryGetValue("effect.sourceContextId", out var v) || v == null) return false;

            if (v is long l)
            {
                ownerKey = l;
                return ownerKey != 0;
            }

            if (v is int i)
            {
                ownerKey = i;
                return ownerKey != 0;
            }

            if (v is string s && !string.IsNullOrEmpty(s) && long.TryParse(s, out var parsed))
            {
                ownerKey = parsed;
                return ownerKey != 0;
            }

            return false;
        }
    }
}
