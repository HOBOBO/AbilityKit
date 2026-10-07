using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaAttributeContinuousModifierProjector : IMobaContinuousModifierProjector
    {
        private MobaActorLookupService _actors;
        private MobaContinuousModifierCaptureStore _captureStore;

        public int TargetKind => MobaContinuousModifierTargetKind.Attribute;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _actors);
            _captureStore = services?.Resolve<MobaContinuousModifierCaptureStore>()
                ?? throw new System.InvalidOperationException("Modifier capture store is unavailable in this world.");
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (continuous == null || projection == null || modifiers == null || modifiers.Count == 0) return;
            var hasAttributes = false;
            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i]?.TargetKind == TargetKind) { hasAttributes = true; break; }
            if (!hasAttributes) return;
            if (projection.ModifierSourceId == 0)
                throw new System.InvalidOperationException("Attribute modifier requires a source identity.");
            if (!TryGetAttributeTarget(projection.OwnerActorId, out var ctx, out var group))
                throw new System.InvalidOperationException($"Attribute modifier owner {projection.OwnerActorId} has no attribute target.");
            MobaResourceAttributeContextProjector.Refresh(_actors, projection.OwnerActorId);
 
            var stack = GetStack(continuous.Config);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec?.TargetKind != TargetKind) continue;
                if (!CanProject(spec) || !TryResolveAttributeId(spec.TargetId, out var attr))
                    throw new System.InvalidOperationException($"Invalid attribute modifier target {spec.TargetId}.");
                var formulaMagnitude = spec.Magnitude;
                if (spec is IMobaContinuousFormulaSpec formula && formula.HasFormula)
                {
                    if (spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
                        throw new System.InvalidOperationException($"Attribute formula {spec.TargetId} requires OnApplySnapshot.");
                    if (continuous is MobaContinuousRuntimeBase capturedRuntime &&
                        capturedRuntime.TryGetModifierCapture(i, stack, spec.Op, out var previous))
                        formulaMagnitude = MagnitudeSource.Fixed(previous);
                    else
                    {
                        if (!MobaContinuousFormulaEvaluator.TryEvaluate(_actors, projection, formula,
                                stack, spec.Op, out var evaluated))
                            throw new System.InvalidOperationException("Continuous attribute formula could not be captured.");
                        if (!MobaContinuousFormulaEvaluator.TryEvaluate(_actors, projection, formula,
                                1, spec.Op, out var unitValue))
                            throw new System.InvalidOperationException("Continuous attribute formula unit value could not be captured.");
                        (continuous as MobaContinuousRuntimeBase)?.SetModifierCapture(i, stack, evaluated, unitValue);
                        formulaMagnitude = MagnitudeSource.Fixed(evaluated);
                    }
                }

                var hasCapture = spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot &&
                                 continuous is MobaContinuousRuntimeBase runtime && runtime.TryGetModifierCapture(i, stack, spec.Op, out _);
                var magnitudeContext = ctx;
                if (!hasCapture && spec.Magnitude.Type == MagnitudeSourceType.Attribute)
                {
                    if (spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot &&
                        spec.AttributeSourceRole != MobaModifierAttributeSourceRole.Owner)
                        throw new System.InvalidOperationException($"Realtime attribute modifier {spec.TargetId} requires the owner source role.");
                    var actorId = MobaModifierAttributeSourceResolver.ResolveActorId(projection, spec.AttributeSourceRole);
                    if (actorId <= 0 || !TryGetAttributeTarget(actorId, out magnitudeContext, out _) || magnitudeContext == null)
                        throw new System.InvalidOperationException($"Attribute modifier {spec.TargetId} cannot resolve its attribute source actor {actorId}.");
                    MobaResourceAttributeContextProjector.Refresh(_actors, actorId);
                }

                var data = CreateModifierData(continuous, spec, i, projection, attr, stack,
                    magnitudeContext, formulaMagnitude);
                if (group != null) group.AddModifier(attr, data);
                else ctx?.AddModifier(attr, data);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection == null) return;
            if (projection.ModifierSourceId == 0) return;

            _captureStore.Clear(
                projection.ModifierSourceId, MobaContinuousModifierTargetKind.Attribute,
                projection.OwnerActorId);
            if (!TryGetAttributeTarget(projection.OwnerActorId, out var ctx, out var group)) return;

            if (ctx != null) ctx.ClearModifiers(projection.ModifierSourceId);
            else group?.ClearModifiers(projection.ModifierSourceId);
        }

        private bool TryGetAttributeTarget(int actorId, out AttributeContext ctx, out AttributeGroup group)
        {
            ctx = null;
            group = null;

            if (_actors == null) return false;
            if (!_actors.TryGetActorEntity(actorId, out var entity) || entity == null) return false;
            if (!entity.hasAttributeGroup) return false;

            ctx = entity.attributeGroup.Ctx;
            group = entity.attributeGroup.Group;
            return ctx != null || group != null;
        }

        private ModifierData CreateModifierData(IContinuous continuous, IMobaContinuousModifierSpec spec,
            int index, IMobaContinuousProjectionConfig projection, AttributeId attribute,
            int stack, AttributeContext context, MagnitudeSource formulaMagnitude)
        {
            var key = ModifierKey.FromPacked((uint)attribute.RawId);
            var sourceId = projection?.ModifierSourceId ?? 0;
            var stacked = spec is IMobaContinuousFormulaSpec formula && formula.HasFormula
                ? formulaMagnitude : spec.Magnitude;
            if (stacked.Type == MagnitudeSourceType.TimeDecay &&
                spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
                stacked = MagnitudeSource.Fixed(stacked.CalculateAtElapsed(continuous.ElapsedSeconds));
            if (!(spec is IMobaContinuousFormulaSpec evaluatedFormula && evaluatedFormula.HasFormula))
                stacked = ApplyStack(stacked, stack, spec.Op);
            var magnitude = ApplyEvaluationPolicy(continuous, index, stack, spec.Op, spec.Magnitude, stacked,
                spec.EvaluationPolicy, context, out var capturedValue, out var hasCapturedValue);
            _captureStore.Record(new MobaContinuousModifierCaptureRecord(
                continuous,
                spec,
                projection?.OwnerActorId ?? 0,
                spec.TargetKind,
                spec.TargetId,
                spec.Op,
                spec.Value,
                spec.EvaluationPolicy,
                spec.Priority,
                stack,
                sourceId,
                spec.Magnitude,
                stacked,
                magnitude,
                capturedValue,
                hasCapturedValue,
                spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot ? "OnApplySnapshot" : "Realtime",
                hasCapturedValue ? "Captured when continuous modifier was projected" : "Realtime magnitude kept for attribute calculation"));

            return new ModifierData
            {
                Key = key,
                Op = MobaContinuousModifierMath.ToModifierOp(spec.Op),
                Magnitude = magnitude,
                Priority = spec.Priority,
                SourceId = sourceId,
                SourceNameIndex = -1,
                Metadata = ModifierMetadata.CreateByIndex(-1, 0, sourceId)
            };
        }

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack, int op)
        {
            if (stack <= 1) return magnitude;
            return magnitude.WithBaseValue(MobaContinuousModifierMath.ApplyStackValue(
                magnitude.BaseValue, stack, op));
        }

        private static MagnitudeSource ApplyEvaluationPolicy(IContinuous continuous, int index, int stack,
            int op, MagnitudeSource unitMagnitude, MagnitudeSource magnitude, int policy, AttributeContext context,
            out float capturedValue, out bool hasCapturedValue)
        {
            capturedValue = 0f;
            hasCapturedValue = false;
            if (policy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot) return magnitude;

            if (continuous is MobaContinuousRuntimeBase runtime &&
                runtime.TryGetModifierCapture(index, stack, op, out capturedValue))
            {
                hasCapturedValue = true;
                return MagnitudeSource.Fixed(capturedValue);
            }
            var unitValue = unitMagnitude.Calculate(context?.Level ?? 1f, context);
            capturedValue = MobaContinuousModifierMath.ApplyStackValue(unitValue, stack, op);
            (continuous as MobaContinuousRuntimeBase)?.SetModifierCapture(index, stack, capturedValue, unitValue);
            hasCapturedValue = true;
            return MagnitudeSource.Fixed(capturedValue);
        }

        private static bool CanProject(IMobaContinuousModifierSpec spec)
        {
            return spec != null &&
                   spec.TargetKind == MobaContinuousModifierTargetKind.Attribute &&
                   spec.TargetId != 0 &&
                   spec.Op >= (int)ModifierOp.Add && spec.Op <= (int)ModifierOp.PercentAdd;
        }

        private static bool TryResolveAttributeId(int targetId, out AttributeId attr)
        {
            attr = MobaAttributeIds.Get((BattleAttributeType)targetId);
            return attr.IsValid;
        }

        private static int GetStack(IContinuousConfig config)
        {
            return config is IStackConfig stackConfig && stackConfig.Stack > 1 ? stackConfig.Stack : 1;
        }

    }

    public static class MobaModifierAttributeSourceResolver
    {
        public static int ResolveActorId(IMobaContinuousProjectionConfig projection, int role)
        {
            if (projection == null) return 0;
            switch (role)
            {
                case MobaModifierAttributeSourceRole.Owner: return projection.OwnerActorId;
                case MobaModifierAttributeSourceRole.Source: return projection.SourceActorId;
                case MobaModifierAttributeSourceRole.Target: return projection.TargetActorId;
                default: return 0;
            }
        }
    }
}
