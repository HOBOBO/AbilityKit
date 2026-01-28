using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Triggering.Runtime.Builtins;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Triggering.Legacy
{
    public sealed class LegacyTriggerExecutor : ILegacyTriggerExecutor
    {
        private readonly TriggerRegistry _registry;
        private readonly ITriggerContextFactory _contextFactory;
        private readonly TriggerCompiler _compiler;

        public LegacyTriggerExecutor(TriggerRegistry registry, ITriggerContextFactory contextFactory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _compiler = new TriggerCompiler(_registry);
        }

        public bool Evaluate<TArgs>(string conditionType, IReadOnlyDictionary<string, object> args, in TArgs eventArgs, in ExecCtx ctx)
        {
            if (string.IsNullOrEmpty(conditionType)) return false;

            // Build a minimal old TriggerContext so existing built-in conditions/actions can run.
            // We do not have a strongly-typed mapping from new eventArgs -> old TriggerEvent payload/args,
            // so by default we put eventArgs into TriggerEvent.Payload.
            // NOTE: TriggerEvent.Id is required but not used by most conditions; pass dummy.
            var evt = new TriggerEvent("<legacy>", eventArgs, args);

            var localVars = new Dictionary<string, object>(StringComparer.Ordinal);
            var oldCtx = _contextFactory.CreateContext(in evt, localVars);
            try
            {
                oldCtx.Event = evt;

                var defArgs = args as Dictionary<string, object>;
                if (defArgs == null && args != null)
                {
                    defArgs = new Dictionary<string, object>(args, StringComparer.Ordinal);
                }

                var def = new ConditionDef(conditionType, defArgs);

                // Reuse old compiler logic for all/any/not so legacy trees can still work.
                // For leaf types, use registry.
                var condition = CompileLegacyCondition(def);
                return condition.Evaluate(oldCtx);
            }
            finally
            {
                TriggerContext.Return(oldCtx);
            }
        }

        public void Execute<TArgs>(string actionType, IReadOnlyDictionary<string, object> args, in TArgs eventArgs, in ExecCtx ctx)
        {
            if (string.IsNullOrEmpty(actionType)) return;

            var evt = new TriggerEvent("<legacy>", eventArgs, args);
            var localVars = new Dictionary<string, object>(StringComparer.Ordinal);
            var oldCtx = _contextFactory.CreateContext(in evt, localVars);
            try
            {
                oldCtx.Event = evt;

                var defArgs = args as Dictionary<string, object>;
                if (defArgs == null && args != null)
                {
                    defArgs = new Dictionary<string, object>(args, StringComparer.Ordinal);
                }

                var def = new ActionDef(actionType, defArgs);
                var act = _registry.CreateAction(def);
                act.Execute(oldCtx);
            }
            finally
            {
                TriggerContext.Return(oldCtx);
            }
        }

        private ITriggerCondition CompileLegacyCondition(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            // Mirror TriggerCompiler's special handling for all/any/not.
            if (string.Equals(def.Type, TriggerConditionTypes.All, StringComparison.Ordinal))
            {
                return AllCondition.FromDef(def, _compiler);
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                return AnyCondition.FromDef(def, _compiler);
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                return NotCondition.FromDef(def, _compiler);
            }

            return _registry.CreateCondition(def);
        }
    }
}
