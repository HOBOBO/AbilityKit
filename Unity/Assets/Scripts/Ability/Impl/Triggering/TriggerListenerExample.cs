using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Triggering.Runtime.Builtins;
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
            registry.RegisterAction("seq", new SequenceActionFactory(registry));
            registry.RegisterAction("debug_log", new DebugLogActionFactory());
            registry.RegisterAction("log_attacker", new LogAttackerNameActionFactory());

            _runner = new TriggerRunner(UnityGlobalEventBus.Instance, registry, new UnityTriggerContextFactory());

            var conditions = new List<ConditionDef>();
            if (OnlyWhenSelfAttacked)
            {
                var condArgs = PooledDefArgs.Rent();
                condArgs["key"] = "source";
                condArgs["value"] = gameObject;
                conditions.Add(new ConditionDef("arg_eq", condArgs));
            }

            var actions = new List<ActionDef>();
            if (OnlyWhenSelfAttacked)
            {
                var actArgs = PooledDefArgs.Rent();
                actArgs["message"] = "我自己发动了攻击";
                actions.Add(new ActionDef("debug_log", actArgs));
            }
            else
            {
                var actArgs = PooledDefArgs.Rent();
                actArgs["format"] = "{0}发动了攻击";
                actions.Add(new ActionDef("log_attacker", actArgs));
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
