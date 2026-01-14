using System.Collections.Generic;
using AbilityKit.Ability.Impl.Triggering;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Triggering.Runtime.Builtins;
using UnityEngine;

namespace AbilityKit.Ability.Impl
{
    public sealed class UnityTriggerBootstrap : MonoBehaviour
    {
        private EventBus _eventBus;
        private TriggerRunner _runner;
        private IEventSubscription _subscription;

        private void Awake()
        {
            _eventBus = new EventBus();

            var registry = new TriggerRegistry();
            registry.RegisterCondition("arg_gt", new ArgGreaterThanConditionFactory());
            registry.RegisterAction("set_var", new SetVarActionFactory());
            registry.RegisterAction("seq", new SequenceActionFactory(registry));
            registry.RegisterAction("debug_log", new DebugLogActionFactory());

            var contextFactory = new UnityTriggerContextFactory();
            _runner = new TriggerRunner(_eventBus, registry, contextFactory);

            var condArgs = PooledDefArgs.Rent();
            condArgs["key"] = "damage";
            condArgs["value"] = 0;

            var actArgs = PooledDefArgs.Rent();
            actArgs["message"] = "UnitDamaged fired and damage>0";

            var trigger = new TriggerDef(
                eventId: "UnitDamaged",
                conditions: new List<ConditionDef>
                {
                    new ConditionDef("arg_gt", condArgs)
                },
                actions: new List<ActionDef>
                {
                    new ActionDef("debug_log", actArgs)
                }
            );

            _subscription = _runner.Register(trigger);
        }

        private void Start()
        {
            var args = PooledTriggerArgs.Rent();
            args["damage"] = 10;
            args["source"] = gameObject;
            args["target"] = gameObject;
            _eventBus.Publish(new TriggerEvent(id: "UnitDamaged", payload: null, args: args));
        }

        private void OnDestroy()
        {
            _subscription?.Unsubscribe();
            _subscription = null;
        }
    }
}
