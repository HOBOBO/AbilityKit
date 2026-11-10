using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;
using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaStateMachineParameterContinuousModifierProjector : IMobaContinuousModifierProjector
    {
        private MobaCharacterHfsmParameterService _parameters;
        public int TargetKind => MobaContinuousModifierTargetKind.StateMachineParameter;

        public void OnInit(IWorldResolver services) => services?.TryResolve(out _parameters);

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (projection == null || modifiers == null) return;
            var hasParameters = false;
            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i]?.TargetKind == TargetKind) { hasParameters = true; break; }
            if (!hasParameters) return;
            if (_parameters == null) throw new System.InvalidOperationException("HFSM parameter service is unavailable.");
            if (projection.OwnerActorId <= 0 || projection.ModifierSourceId <= 0)
                throw new System.InvalidOperationException("HFSM parameter modifier requires a valid owner and source identity.");

            var stack = MobaContinuousModifierStackResolver.Resolve(continuous.Config);
            for (var i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec?.TargetKind != TargetKind) continue;
                if (!_parameters.SupportsOperation(projection.OwnerActorId, spec.TargetId, spec.Op))
                    throw new System.InvalidOperationException($"Invalid HFSM parameter modifier {spec.TargetId}/{spec.Op}.");
                if (spec is IMobaContinuousFormulaSpec formula && formula.HasFormula)
                    throw new System.InvalidOperationException("HFSM parameter formula modifiers are not supported yet; use a fixed magnitude.");
                var magnitude = spec.Magnitude;
                if (magnitude.Type == MagnitudeSourceType.TimeDecay)
                    magnitude = MagnitudeSource.Fixed(magnitude.CalculateAtElapsed(continuous.ElapsedSeconds));
                if (stack > 1 && spec.Op != (int)ModifierOp.Override)
                    magnitude = magnitude.WithBaseValue(MobaContinuousModifierMath.ApplyStackValue(magnitude.BaseValue, stack, spec.Op));
                var data = new ModifierData
                {
                    Key = ModifierKey.FromPacked((uint)spec.TargetId),
                    Op = MobaContinuousModifierMath.ToModifierOp(spec.Op),
                    Magnitude = magnitude,
                    Priority = spec.Priority,
                    SourceId = projection.ModifierSourceId,
                    SourceNameIndex = -1,
                    Metadata = ModifierMetadata.CreateByIndex(-1, 0, projection.ModifierSourceId)
                };
                _parameters.Set(projection.OwnerActorId, projection.ModifierSourceId, spec.TargetId, in data);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection != null && projection.OwnerActorId > 0 && projection.ModifierSourceId > 0)
                _parameters?.ClearSource(projection.OwnerActorId, projection.ModifierSourceId);
        }
    }
}
