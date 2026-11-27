using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Attributes.Core;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Triggering.Eventing;
using AbilityKit.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.Effect
{
    /// <summary>
    /// 可选的效果应用观察端口。只读观察新实例与应用上下文，不参与应用判定和恢复。
    /// </summary>
    public interface IEffectApplicationObserver
    {
        /// <summary>
        /// 在通过应用要求并分配实例后调用；不得修改效果或战斗状态。
        /// </summary>
        void OnApplication(AbilityKit.Ability.Share.Effect.EffectInstance instance, in EffectExecutionContext context);
    }

    public readonly struct EffectExecutionContext
    {
        public readonly IServiceProvider Services;
        public readonly IFrameTime Time;

        public readonly object Source;
        public readonly object Target;
        public readonly long SourceContextId;

        public readonly IUnitFacade TargetUnit;

        public GameplayTagContainer TargetTags => TargetUnit.Tags;
        public AttributeContext TargetAttributes => TargetUnit.Attributes;
        public readonly IEventBus EventBus;

        public EffectExecutionContext(
            IServiceProvider services,
            IFrameTime time,
            object source,
            object target,
            long sourceContextId,
            IUnitFacade targetUnit,
            IEventBus eventBus)
        {
            Services = services;
            Time = time;
            Source = source;
            Target = target;
            SourceContextId = sourceContextId;
            TargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
            EventBus = eventBus;
        }

        public EffectExecutionContext WithSourceContextId(long sourceContextId)
        {
            return new EffectExecutionContext(
                Services, Time, Source, Target, sourceContextId, TargetUnit, EventBus);
        }
    }
}
