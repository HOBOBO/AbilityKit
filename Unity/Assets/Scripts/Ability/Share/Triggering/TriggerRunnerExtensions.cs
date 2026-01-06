using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public static class TriggerRunnerExtensions
    {
        private static readonly ObjectPool<Dictionary<string, object>> _dictPool = Pools.GetPool(
            createFunc: () => new Dictionary<string, object>(StringComparer.Ordinal),
            onRelease: dict => dict.Clear(),
            defaultCapacity: 32,
            maxSize: 256,
            collectionCheck: false);

        public static IEventSubscription Register(this TriggerRunner runner, TriggerRuntimeConfig config)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var def = config.ToTriggerDef();

            var locals = _dictPool.Get();
            try
            {
                config.FillInitialLocalVars(locals);
                return runner.Register(def, locals);
            }
            finally
            {
                _dictPool.Release(locals);
            }
        }

        public static IReadOnlyList<IEventSubscription> Register(this TriggerRunner runner, AbilityRuntimeSO skill)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (skill == null) throw new ArgumentNullException(nameof(skill));

            var list = new List<IEventSubscription>(skill.Triggers != null ? skill.Triggers.Count : 0);
            Register(runner, skill, list, clearOutput: false);
            return list;
        }

        public static void Register(this TriggerRunner runner, AbilityRuntimeSO skill, List<IEventSubscription> output, bool clearOutput = true)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (output == null) throw new ArgumentNullException(nameof(output));

            if (clearOutput) output.Clear();

            var triggers = skill.Triggers;
            if (triggers == null) return;

            if (output.Capacity < triggers.Count) output.Capacity = triggers.Count;

            for (int i = 0; i < triggers.Count; i++)
            {
                var t = triggers[i];
                if (t == null) continue;
                output.Add(Register(runner, t));
            }
        }

        private static void FillInitialLocalVars(this TriggerRuntimeConfig config, Dictionary<string, object> locals)
        {
            if (locals == null) throw new ArgumentNullException(nameof(locals));
            var list = config.LocalVars;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                locals[e.Key] = e.GetBoxedValue();
            }
        }
    }
}
