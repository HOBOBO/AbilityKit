using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousModifierProjector
    {
        int TargetKind { get; }
        void OnInit(IWorldResolver services);
        void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers);
        void Clear(IMobaContinuousProjectionConfig projection);
    }

    public sealed class MobaContinuousModifierProjectorRegistry
    {
        private readonly Dictionary<int, IMobaContinuousModifierProjector> _projectors = new Dictionary<int, IMobaContinuousModifierProjector>();
        private readonly List<IMobaContinuousModifierProjector> _orderedProjectors = new List<IMobaContinuousModifierProjector>();

        public void Register(IMobaContinuousModifierProjector projector)
        {
            if (projector == null) throw new ArgumentNullException(nameof(projector));
            if (!_projectors.TryAdd(projector.TargetKind, projector))
                throw new InvalidOperationException($"Duplicate modifier projector target kind {projector.TargetKind}.");
            _orderedProjectors.Add(projector);
        }

        public void OnInit(IWorldResolver services)
        {
            for (var i = 0; i < _orderedProjectors.Count; i++)
                _orderedProjectors[i].OnInit(services);
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (continuous == null || projection == null || modifiers == null || modifiers.Count == 0) return;

            MobaContinuousModifierContractValidator.ValidateAll(modifiers);

            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i] == null || !_projectors.ContainsKey(modifiers[i].TargetKind))
                    throw new InvalidOperationException($"No modifier projector for target kind {modifiers[i]?.TargetKind}.");

            try
            {
                for (var i = 0; i < _orderedProjectors.Count; i++)
                    _orderedProjectors[i].Apply(continuous, projection, modifiers);
            }
            catch (Exception failure)
            {
                try { Clear(projection); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Modifier projection and cleanup both failed.",
                        failure, cleanupFailure);
                }
                throw;
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection == null) return;

            List<Exception> failures = null;
            for (var i = _orderedProjectors.Count - 1; i >= 0; i--)
            {
                try { _orderedProjectors[i].Clear(projection); }
                catch (Exception failure)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(failure);
                }
            }
            if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures != null) throw new AggregateException("Modifier cleanup failed.", failures);
        }

        public void ReprojectTimeVarying(IContinuous continuous,
            IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (continuous == null || projection == null || modifiers == null) return;
            for (var projectorIndex = 0; projectorIndex < _orderedProjectors.Count; projectorIndex++)
            {
                var projector = _orderedProjectors[projectorIndex];
                var hasCurve = false;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var spec = modifiers[i];
                    if (spec == null || spec.TargetKind != projector.TargetKind ||
                        spec.Magnitude.Type != MagnitudeSourceType.TimeDecay) continue;
                    hasCurve = true;
                    break;
                }
                if (!hasCurve) continue;
                projector.Clear(projection);
                try
                {
                    projector.Apply(continuous, projection, modifiers);
                }
                catch (Exception failure)
                {
                    try { Clear(projection); }
                    catch (Exception cleanupFailure)
                    {
                        throw new AggregateException("Modifier curve projection and cleanup both failed.",
                            failure, cleanupFailure);
                    }
                    throw;
                }
            }
        }
    }
}
