using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime.Builtins;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggerCompiler : IConditionCompiler
    {
        private readonly TriggerRegistry _registry;

        public TriggerCompiler(TriggerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public TriggerInstance Compile(TriggerDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var conditions = new List<ITriggerCondition>(def.Conditions.Count);
            for (int i = 0; i < def.Conditions.Count; i++)
            {
                var cdef = def.Conditions[i];
                if (cdef == null) continue;
                try
                {
                    conditions.Add(Compile(cdef));
                }
                finally
                {
                    if (cdef.Args is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex, $"[TriggerCompiler] condition args dispose failed (eventId={def.EventId})");
                        }
                    }
                }
            }

            var actions = new List<ITriggerAction>(def.Actions.Count);
            for (int i = 0; i < def.Actions.Count; i++)
            {
                var adef = def.Actions[i];
                if (adef == null) continue;
                try
                {
                    actions.Add(_registry.CreateAction(adef));
                }
                finally
                {
                    if (adef.Args is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex, $"[TriggerCompiler] action args dispose failed (eventId={def.EventId})");
                        }
                    }
                }
            }

            return new TriggerInstance(def.EventId, conditions, actions);
        }

        public ITriggerCondition Compile(ConditionDef def)
        {
            return CompileCondition(def);
        }

        private ITriggerCondition CompileCondition(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (string.Equals(def.Type, TriggerConditionTypes.All, StringComparison.Ordinal))
            {
                return AllCondition.FromDef(def, this);
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                return AnyCondition.FromDef(def, this);
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                return NotCondition.FromDef(def, this);
            }

            return _registry.CreateCondition(def);
        }
    }
}
