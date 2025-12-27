using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Definitions;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class TriggerRunner
    {
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

        private sealed class TriggerEventHandler : IEventHandler
        {
            private readonly TriggerInstance _trigger;
            private readonly ITriggerContextFactory _contextFactory;
            private readonly Dictionary<string, object> _localVars;

            public TriggerEventHandler(TriggerInstance trigger, ITriggerContextFactory contextFactory)
            {
                _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
                _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
                _localVars = new Dictionary<string, object>(StringComparer.Ordinal);
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
                context.Event = evt;

                for (int i = 0; i < _trigger.Conditions.Count; i++)
                {
                    if (!_trigger.Conditions[i].Evaluate(context)) return;
                }

                for (int i = 0; i < _trigger.Actions.Count; i++)
                {
                    _trigger.Actions[i].Execute(context);
                }
            }
        }
    }
}
