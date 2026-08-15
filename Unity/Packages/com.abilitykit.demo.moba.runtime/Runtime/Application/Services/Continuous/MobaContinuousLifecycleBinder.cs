using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaContinuousLifecycleBinder : IContinuousLifecycleBinder
    {
        private readonly MobaContinuousModifierProjectorRegistry _modifierProjectors;

        public MobaContinuousLifecycleBinder(MobaContinuousModifierProjectorRegistry modifierProjectors)
        {
            _modifierProjectors = modifierProjectors;
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            ApplyModifiers(continuous);
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
            ClearModifiers(continuous);
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
            ApplyModifiers(continuous);
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            ClearModifiers(continuous);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            ClearModifiers(continuous);
        }

        public void Reproject(IContinuous continuous)
        {
            ClearModifiers(continuous);
            if (continuous != null && continuous.IsActive && !continuous.IsTerminated)
            {
                ApplyModifiers(continuous);
            }
        }

        private void ApplyModifiers(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsActive || continuous.IsTerminated) return;
            if (!(continuous.Config is IMobaContinuousProjectionConfig projection)) return;
            if (!(continuous.Config is IMobaContinuousModifierConfig modifiers)) return;

            _modifierProjectors?.Apply(continuous, projection, modifiers.Modifiers);
        }

        private void ClearModifiers(IContinuous continuous)
        {
            if (continuous == null) return;
            if (!(continuous.Config is IMobaContinuousProjectionConfig projection)) return;

            _modifierProjectors?.Clear(projection);
        }
    }
}
