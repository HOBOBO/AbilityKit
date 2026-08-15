using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;

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

        public void Register(IMobaContinuousModifierProjector projector)
        {
            if (projector == null) return;
            _projectors[projector.TargetKind] = projector;
        }

        public void OnInit(IWorldResolver services)
        {
            foreach (var projector in _projectors.Values)
            {
                projector.OnInit(services);
            }
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (continuous == null || projection == null || modifiers == null || modifiers.Count == 0) return;
 
            foreach (var projector in _projectors.Values)
            {
                projector.Apply(continuous, projection, modifiers);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection == null) return;

            foreach (var projector in _projectors.Values)
            {
                projector.Clear(projection);
            }
        }
    }
}
