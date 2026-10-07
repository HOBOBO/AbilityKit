using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillParamContinuousModifierProjector : IMobaContinuousModifierProjector
    {
        private MobaSkillParamModifierService _skillParams;
        private MobaActorLookupService _actors;
        private MobaContinuousModifierCaptureStore _captureStore;

        public int TargetKind => MobaContinuousModifierTargetKind.SkillParameter;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _skillParams);
            services?.TryResolve(out _actors);
            _captureStore = services?.Resolve<MobaContinuousModifierCaptureStore>()
                ?? throw new System.InvalidOperationException("Modifier capture store is unavailable in this world.");
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (continuous == null || projection == null || modifiers == null || modifiers.Count == 0) return;
            var hasParameters = false;
            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i]?.TargetKind == TargetKind) { hasParameters = true; break; }
            if (!hasParameters) return;
            if (_skillParams == null)
            {
                for (var i = 0; i < modifiers.Count; i++)
                    if (modifiers[i]?.TargetKind == TargetKind)
                        throw new System.InvalidOperationException("Skill parameter modifier service is unavailable.");
                return;
            }
            if (projection.OwnerActorId <= 0 || projection.ModifierSourceId == 0)
                throw new System.InvalidOperationException("Skill parameter modifier requires a valid owner and source identity.");

            var owner = MobaModifierOwnerRef.Actor(projection.OwnerActorId);
            if (!owner.IsValid)
                throw new System.InvalidOperationException("Skill parameter modifier requires an Actor owner.");

            var stack = GetStack(continuous.Config);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec?.TargetKind != TargetKind) continue;
                if (!CanProject(spec))
                    throw new System.InvalidOperationException($"Invalid skill parameter modifier {spec.TargetId}: unsupported parameter or operation {spec.Op}.");
                var formulaMagnitude = spec.Magnitude;
                if (spec is IMobaContinuousFormulaSpec formula && formula.HasFormula)
                {
                    if (spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
                        throw new System.InvalidOperationException($"Skill parameter formula {spec.TargetId} requires OnApplySnapshot.");
                    if (continuous is MobaContinuousRuntimeBase capturedRuntime &&
                        capturedRuntime.TryGetModifierCapture(i, stack, spec.Op, out var previous))
                        formulaMagnitude = MagnitudeSource.Fixed(previous);
                    else
                    {
                        if (!MobaContinuousFormulaEvaluator.TryEvaluate(_actors, projection, formula,
                                stack, spec.Op, out var evaluated))
                            throw new System.InvalidOperationException("Continuous parameter formula could not be captured.");
                        if (!MobaContinuousFormulaEvaluator.TryEvaluate(_actors, projection, formula,
                                1, spec.Op, out var unitValue))
                            throw new System.InvalidOperationException("Continuous parameter formula unit value could not be captured.");
                        (continuous as MobaContinuousRuntimeBase)?.SetModifierCapture(i, stack, evaluated, unitValue);
                        formulaMagnitude = MagnitudeSource.Fixed(evaluated);
                    }
                }
                if (spec.Magnitude.Type == MagnitudeSourceType.Attribute &&
                    spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot &&
                    spec.AttributeSourceRole != MobaModifierAttributeSourceRole.Owner)
                    throw new System.InvalidOperationException($"Realtime skill parameter attribute {spec.TargetId} requires the owner source role.");

                AttributeContext context = null;
                var hasCapture = spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot &&
                                 continuous is MobaContinuousRuntimeBase runtime && runtime.TryGetModifierCapture(i, stack, spec.Op, out _);
                if (spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot && !hasCapture)
                {
                    var actorId = MobaModifierAttributeSourceResolver.ResolveActorId(projection, spec.AttributeSourceRole);
                    if (_actors == null || actorId <= 0 ||
                        !_actors.TryGetActorEntity(actorId, out var entity) || entity == null ||
                        !entity.hasAttributeGroup || (context = entity.attributeGroup.Ctx) == null)
                        throw new System.InvalidOperationException($"Skill parameter modifier {spec.TargetId} cannot resolve its attribute source actor {actorId}.");
                    MobaResourceAttributeContextProjector.Refresh(_actors, actorId);
                }

                var selector = spec is IMobaContinuousParameterSelectorSpec selected
                    ? selected.Selector : default;
                _skillParams.AddModifier(owner,
                    CreateModifierData(continuous, spec, i, projection, stack, context, formulaMagnitude), selector);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (_skillParams == null || projection == null) return;
            if (projection.OwnerActorId <= 0 || projection.ModifierSourceId == 0) return;

            var owner = MobaModifierOwnerRef.Actor(projection.OwnerActorId);
            if (owner.IsValid)
            {
                _skillParams.ClearSource(owner, projection.ModifierSourceId);
            }
            _captureStore.Clear(
                projection.ModifierSourceId, MobaContinuousModifierTargetKind.SkillParameter,
                projection.OwnerActorId);
        }

        private ModifierData CreateModifierData(IContinuous continuous, IMobaContinuousModifierSpec spec,
            int index, IMobaContinuousProjectionConfig projection, int stack,
            AttributeContext context, MagnitudeSource formulaMagnitude)
        {
            var sourceId = projection?.ModifierSourceId ?? 0;
            var stacked = spec is IMobaContinuousFormulaSpec formula && formula.HasFormula
                ? formulaMagnitude : spec.Magnitude;
            if (stacked.Type == MagnitudeSourceType.TimeDecay &&
                spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
                stacked = MagnitudeSource.Fixed(stacked.CalculateAtElapsed(continuous.ElapsedSeconds));
            if (!(spec is IMobaContinuousFormulaSpec evaluatedFormula && evaluatedFormula.HasFormula))
                stacked = ApplyStack(stacked, stack, spec.Op);
            var magnitude = stacked;
            if (spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
            {
                float value;
                if (!(continuous is MobaContinuousRuntimeBase runtime &&
                      runtime.TryGetModifierCapture(index, stack, spec.Op, out value)))
                {
                    var unitValue = spec.Magnitude.Calculate(context?.Level ?? 1f, context);
                    value = MobaContinuousModifierMath.ApplyStackValue(unitValue, stack, spec.Op);
                    (continuous as MobaContinuousRuntimeBase)?.SetModifierCapture(index, stack, value, unitValue);
                }
                magnitude = MagnitudeSource.Fixed(value);
                _captureStore.Record(new MobaContinuousModifierCaptureRecord(
                    continuous, spec, projection?.OwnerActorId ?? 0, spec.TargetKind, spec.TargetId,
                    spec.Op, spec.Value, spec.EvaluationPolicy, spec.Priority, stack, sourceId,
                    spec.Magnitude, stacked, magnitude, value, true, "OnApplySnapshot",
                    "Captured when continuous modifier was projected"));
            }
            return new ModifierData
            {
                Key = ResolveKey(spec.TargetId),
                Op = MobaContinuousModifierMath.ToModifierOp(spec.Op),
                Magnitude = magnitude,
                Priority = spec.Priority,
                SourceId = sourceId,
                SourceNameIndex = -1,
                Metadata = ModifierMetadata.CreateByIndex(-1, 0, sourceId)
            };
        }

        private static ModifierKey ResolveKey(int targetId)
        {
            return MobaSkillParamModifierKeys.TryResolve(targetId, out var key) ? key : ModifierKey.None;
        }

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack, int op)
        {
            if (stack <= 1) return magnitude;
            return magnitude.WithBaseValue(MobaContinuousModifierMath.ApplyStackValue(
                magnitude.BaseValue, stack, op));
        }

        private static bool CanProject(IMobaContinuousModifierSpec spec)
        {
            return spec != null &&
                   spec.TargetKind == MobaContinuousModifierTargetKind.SkillParameter &&
                   MobaSkillParamModifierKeys.TryResolve(spec.TargetId, out _) &&
                   MobaSkillParamModifierKeys.SupportsOperation(spec.TargetId, spec.Op);
        }

        private static int GetStack(IContinuousConfig config)
        {
            return config is IStackConfig stackConfig && stackConfig.Stack > 1 ? stackConfig.Stack : 1;
        }

    }
}
