using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>Reads explicitly addressed Cast inputs once. Persistent results use the existing capture store.</summary>
    public static class MobaContinuousCastMagnitude
    {
        public const string Prefix = "skill_runtime:";

        public static bool IsRequested(IMobaContinuousModifierSpec spec) =>
            spec?.Magnitude.Type == MagnitudeSourceType.ContextFloat &&
            (spec.Magnitude.ContextFloatKey?.StartsWith(Prefix, StringComparison.Ordinal) ?? false);

        public static void Validate(IMobaContinuousModifierSpec spec)
        {
            if (!IsRequested(spec)) return;
            if (spec.TargetKind != MobaContinuousModifierTargetKind.Attribute ||
                spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot ||
                spec is IMobaContinuousFormulaSpec formula && formula.HasFormula)
                throw new InvalidOperationException("Cast context magnitudes require an Attribute OnApplySnapshot modifier without a second formula.");
            Parse(spec.Magnitude.ContextFloatKey, out _, out _);
        }

        public static void Capture(IContinuous continuous, IMobaContinuousProjectionConfig projection,
            IReadOnlyList<IMobaContinuousModifierSpec> specs, MobaSkillCastRuntimeService casts)
        {
            // Stage every read before committing any capture; a missing input cannot leave a partial sample.
            List<(IMobaContinuousModifierSpec Spec, int Ordinal, int Stack, float Unit, float Value)> pending = null;
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (!IsRequested(spec)) continue;
                Validate(spec);
                if (!(continuous is MobaContinuousRuntimeBase runtime))
                    throw new InvalidOperationException("Cast context magnitude requires a persistent Continuous capture owner.");
                var stack = MobaContinuousModifierStackResolver.Resolve(continuous.Config);
                if (runtime.TryGetModifierCapture(spec, i, stack, spec.Op, out _)) continue;
                if (casts == null || !(continuous is IMobaContinuousExecutionContextProvider provider) ||
                    !provider.TryGetCombatExecutionContext(out var context))
                    throw new InvalidOperationException("Cast context magnitude has no execution source.");
                var handle = context.SkillRuntimeHandle;
                if (!casts.TryGet(in handle, out var cast) || !cast.Handle.Equals(handle) || cast.RootContextId != handle.RootContextId)
                    throw new InvalidOperationException("Cast context magnitude has no live matching Cast.");

                Parse(spec.Magnitude.ContextFloatKey, out var scope, out var name);
                var owner = scope == MobaSkillRuntimeBlackboardScope.Target ? projection.OwnerActorId :
                    scope == MobaSkillRuntimeBlackboardScope.Child ? context.OwnerContextId : 0L;
                if (scope != MobaSkillRuntimeBlackboardScope.Cast && owner == 0L)
                    throw new InvalidOperationException("Cast context magnitude has no scope owner.");
                var address = new MobaSkillRuntimeBlackboardAddress(scope, owner);
                var keyId = BlackboardIdMapper.KeyId("skill_runtime." + ScopeName(scope) + "." + name);
                if (!cast.Blackboard.TryGetValue(keyId, in address, out _, out var raw))
                    throw new InvalidOperationException("Cast context magnitude input is missing: " + spec.Magnitude.ContextFloatKey);
                double sampled;
                switch (raw.Kind)
                {
                    case MobaSkillRuntimeValueKind.Float: sampled = raw.FloatValue; break;
                    case MobaSkillRuntimeValueKind.Double: sampled = raw.DoubleValue; break;
                    case MobaSkillRuntimeValueKind.Int: sampled = raw.IntValue; break;
                    default: throw new InvalidOperationException("Cast context magnitude input must be numeric.");
                }
                if (double.IsNaN(sampled) || double.IsInfinity(sampled) || Math.Abs(sampled) > float.MaxValue)
                    throw new InvalidOperationException("Cast context magnitude input is not finite.");
                var unit = (spec.Magnitude.BaseValue + (float)sampled) * spec.Magnitude.Coefficient;
                var value = MobaContinuousModifierMath.ApplyStackValue(unit, stack, spec.Op);
                if (float.IsNaN(unit) || float.IsInfinity(unit) || float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidOperationException("Cast context magnitude result is not finite.");
                pending ??= new List<(IMobaContinuousModifierSpec, int, int, float, float)>();
                pending.Add((spec, i, stack, unit, value));
            }
            if (pending == null) return;
            foreach (var item in pending)
                ((MobaContinuousRuntimeBase)continuous).SetModifierCapture(item.Spec, item.Ordinal, item.Stack, item.Value, item.Unit);
        }

        private static string ScopeName(MobaSkillRuntimeBlackboardScope scope) =>
            scope == MobaSkillRuntimeBlackboardScope.Cast ? "cast" :
            scope == MobaSkillRuntimeBlackboardScope.Target ? "target" : "child";

        private static void Parse(string key, out MobaSkillRuntimeBlackboardScope scope, out string name)
        {
            var path = key.Substring(Prefix.Length);
            var separator = path.IndexOf('.');
            var scopeName = separator > 0 ? path.Substring(0, separator) : string.Empty;
            name = separator > 0 ? path.Substring(separator + 1) : string.Empty;
            switch (scopeName)
            {
                case "cast": scope = MobaSkillRuntimeBlackboardScope.Cast; break;
                case "target": scope = MobaSkillRuntimeBlackboardScope.Target; break;
                case "child": scope = MobaSkillRuntimeBlackboardScope.Child; break;
                default: throw new InvalidOperationException("Unsupported Cast magnitude scope: " + key);
            }
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim())
                throw new InvalidOperationException("Cast context magnitude requires a nonempty key: " + key);
        }
    }
}
