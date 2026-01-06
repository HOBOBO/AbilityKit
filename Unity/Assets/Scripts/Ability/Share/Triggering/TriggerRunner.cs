using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Share.Common.Pool;

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

        public TriggerRunner(IEventBus eventBus, TriggerRegistry registry, ITriggerContextFactory contextFactory)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _compiler = new TriggerCompiler(registry);
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
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

        public IEventSubscription Register(TriggerInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return _eventBus.Subscribe(instance.EventId, new TriggerEventHandler(instance, _contextFactory));
        }

        public IEventSubscription Register(TriggerInstance instance, System.Collections.Generic.IReadOnlyDictionary<string, object> initialLocalVars)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return _eventBus.Subscribe(instance.EventId, new TriggerEventHandler(instance, _contextFactory, initialLocalVars));
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
                                runner?.Add(running, context.Source);
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

            private static ITriggerActionRunner GetRunner(TriggerContext context)
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
    }
}
