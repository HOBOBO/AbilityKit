using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Triggering.Runtime.Builtins;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggeringWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.TryRegister<ITriggerContextFactory>(WorldLifetime.Scoped, services =>
            {
                var sp = new WorldServiceProviderAdapter(services);
                return new WorldTriggerContextFactory(sp);
            });

            builder.TryRegister<TriggerRegistry>(WorldLifetime.Scoped, _ =>
            {
                var registry = new TriggerRegistry();
                registry.RegisterCondition("arg_eq", new ArgEqualsConditionFactory());
                registry.RegisterCondition("arg_gt", new ArgGreaterThanConditionFactory());
                registry.RegisterAction("set_var", new SetVarActionFactory());
                registry.RegisterAction("seq", new SequenceActionFactory(registry));
                registry.RegisterAction("attr_effect_duration", CreateFactory("AbilityKit.Ability.Triggering.Runtime.Builtins.AddAttributeEffectForDurationActionFactory"));
                registry.RegisterAction("debug_log", CreateFactory("AbilityKit.Ability.Impl.Triggering.DebugLogActionFactory, AbilityKit.Ability.Unity"));
                registry.RegisterAction("log_attacker", CreateFactory("AbilityKit.Ability.Impl.Triggering.LogAttackerNameActionFactory, AbilityKit.Ability.Unity"));
                registry.RegisterAction("effect_execute", CreateFactory("AbilityKit.Ability.Impl.Triggering.ExecuteEffectActionFactory, AbilityKit.Ability.Unity"));
                return registry;
            });

            builder.TryRegister<TriggerRunner>(WorldLifetime.Scoped, services =>
            {
                var bus = services.Resolve<IEventBus>();
                var registry = services.Resolve<TriggerRegistry>();
                var ctxFactory = services.Resolve<ITriggerContextFactory>();
                return new TriggerRunner(bus, registry, ctxFactory);
            });
        }

        private static IActionFactory CreateFactory(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentNullException(nameof(typeName));

            var type = Type.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException($"Trigger action factory type not found: {typeName}");
            }

            if (!(Activator.CreateInstance(type) is IActionFactory factory))
            {
                throw new InvalidOperationException($"Type is not an IActionFactory: {typeName}");
            }

            return factory;
        }
    }
}
