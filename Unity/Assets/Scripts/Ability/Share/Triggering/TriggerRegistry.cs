using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggerRegistry
    {
        private readonly Dictionary<string, IConditionFactory> _conditionFactories = new Dictionary<string, IConditionFactory>(StringComparer.Ordinal);
        private readonly Dictionary<string, IActionFactory> _actionFactories = new Dictionary<string, IActionFactory>(StringComparer.Ordinal);

        public void AutoRegisterFromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0) throw new ArgumentNullException(nameof(assemblies));

            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                if (asm == null) continue;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;
                    if (type.IsAbstract || type.IsInterface) continue;

                    var actionAttr = type.GetCustomAttribute<TriggerActionTypeAttribute>(inherit: false);
                    if (actionAttr != null && typeof(IActionFactory).IsAssignableFrom(type))
                    {
                        var factory = CreateFactoryInstance<IActionFactory>(type);
                        if (factory != null) RegisterAction(actionAttr.Type, factory);
                        continue;
                    }

                    var conditionAttr = type.GetCustomAttribute<TriggerConditionTypeAttribute>(inherit: false);
                    if (conditionAttr != null && typeof(IConditionFactory).IsAssignableFrom(type))
                    {
                        var factory = CreateFactoryInstance<IConditionFactory>(type);
                        if (factory != null) RegisterCondition(conditionAttr.Type, factory);
                    }
                }
            }
        }

        private T CreateFactoryInstance<T>(Type type) where T : class
        {
            if (type == null) return null;

            try
            {
                // Support factories that need registry (e.g. SequenceActionFactory).
                var ctorWithRegistry = type.GetConstructor(new[] { typeof(TriggerRegistry) });
                if (ctorWithRegistry != null)
                {
                    return ctorWithRegistry.Invoke(new object[] { this }) as T;
                }

                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null) return null;
                return Activator.CreateInstance(type) as T;
            }
            catch
            {
                return null;
            }
        }

        public void RegisterCondition(string type, IConditionFactory factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _conditionFactories[type] = factory;
        }

        public void RegisterAction(string type, IActionFactory factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _actionFactories[type] = factory;
        }

        public ITriggerCondition CreateCondition(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (!_conditionFactories.TryGetValue(def.Type, out var factory))
            {
                throw new InvalidOperationException($"Condition type not registered: {def.Type}");
            }

            return factory.Create(def);
        }

        public ITriggerAction CreateAction(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (!_actionFactories.TryGetValue(def.Type, out var factory))
            {
                Log.Error($"Action type not registered: {def.Type}");
                throw new InvalidOperationException($"Action type not registered: {def.Type}");
            }

            return factory.Create(def);
        }
    }
}
