using System;
using AbilityKit.Ability.Share.Common.AttributeSystem;

namespace AbilityKit.Ability.Share.Effect.Components
{
    public sealed class AttributeEffectComponent : IEffectComponent
    {
        private static readonly object HandleKey = new object();

        private readonly AttributeEffect _effect;

        public AttributeEffectComponent(AttributeEffect effect)
        {
            _effect = effect;
        }

        public void OnApply(in EffectExecutionContext context, EffectInstance instance)
        {
            if (_effect == null) return;
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var attrs = context.TargetAttributes;
            if (attrs == null) return;

            var handle = attrs.ApplyEffect(_effect);
            if (handle != null)
            {
                instance.SetState(HandleKey, handle);
            }
        }

        public void OnTick(in EffectExecutionContext context, EffectInstance instance)
        {
        }

        public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (instance.TryGetState<AttributeEffectHandle>(HandleKey, out var handle) && handle != null)
            {
                handle.Dispose();
                instance.RemoveState(HandleKey);
            }
        }
    }
}
