using System.Collections.Generic;
using AbilityKit.Triggering;
using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Builtins;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class TriggerListenerExample : MonoBehaviour
    {
        public bool OnlyWhenSelfAttacked;

        private TriggerRunner _runner;
        private IEventSubscription _subscription;

        private void Awake()
        {
            var registry = new TriggerRegistry();
            registry.RegisterCondition("arg_eq", new ArgEqualsConditionFactory());
            registry.RegisterAction("debug_log", new DebugLogActionFactory());
            registry.RegisterAction("log_attacker", new LogAttackerNameActionFactory());

            _runner = new TriggerRunner(UnityGlobalEventBus.Instance, registry, new UnityTriggerContextFactory());

            var conditions = new List<ConditionDef>();
            if (OnlyWhenSelfAttacked)
            {
                conditions.Add(new ConditionDef("arg_eq", new Dictionary<string, object>
                {
                    {"key", "source"},
                    {"value", gameObject}
                }));
            }

            var actions = new List<ActionDef>();
            if (OnlyWhenSelfAttacked)
            {
                actions.Add(new ActionDef("debug_log", new Dictionary<string, object>
                {
                    {"message", "我自己发动了攻击"}
                }));
            }
            else
            {
                actions.Add(new ActionDef("log_attacker", new Dictionary<string, object>
                {
                    {"format", "{0}发动了攻击"}
                }));
            }

            var trigger = new TriggerDef(
                eventId: "Attack",
                conditions: conditions,
                actions: actions
            );

            _subscription = _runner.Register(trigger);
        }

        private void OnDestroy()
        {
            _subscription?.Unsubscribe();
            _subscription = null;
        }
    }
}
