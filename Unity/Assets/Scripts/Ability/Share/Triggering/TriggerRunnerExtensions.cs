using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public static class TriggerRunnerExtensions
    {
        public static IEventSubscription Register(this TriggerRunner runner, TriggerRuntimeConfig config)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var def = config.ToTriggerDef();
            var locals = config.BuildInitialLocalVars();
            return runner.Register(def, locals);
        }

        public static IReadOnlyList<IEventSubscription> Register(this TriggerRunner runner, AbilityRuntimeSO skill)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (skill == null) throw new ArgumentNullException(nameof(skill));

            var list = new List<IEventSubscription>(skill.Triggers != null ? skill.Triggers.Count : 0);
            if (skill.Triggers == null) return list;

            for (int i = 0; i < skill.Triggers.Count; i++)
            {
                var t = skill.Triggers[i];
                if (t == null) continue;
                list.Add(Register(runner, t));
            }

            return list;
        }

        private static Dictionary<string, object> BuildInitialLocalVars(this TriggerRuntimeConfig config)
        {
            var locals = new Dictionary<string, object>(StringComparer.Ordinal);
            var list = config.LocalVars;
            if (list == null) return locals;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                locals[e.Key] = e.GetBoxedValue();
            }

            return locals;
        }
    }
}
