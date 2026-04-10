using System;
using AbilityKit.Ability.Share.Common.AttributeSystem;

namespace AbilityKit.Ability.Share.Effect.Components
{
    /// <summary>
    /// 属性效果组件。
    /// 应用属性效果并在移除时自动清理。
    /// </summary>
    public sealed class AttributeEffectComponent : IEffectComponent
    {
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

            var sourceId = attrs.ApplyEffect(_effect);
            if (sourceId != 0)
            {
                instance.SetState("AttributeEffectSourceId", sourceId);
            }
        }

        public void OnTick(in EffectExecutionContext context, EffectInstance instance)
        {
        }

        public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (instance.TryGetState<int>("AttributeEffectSourceId", out var sourceId) && sourceId != 0)
            {
                var attrs = context.TargetAttributes;
                attrs?.ClearModifiers(sourceId);
                instance.RemoveState("AttributeEffectSourceId");
            }
        }
    }
}
