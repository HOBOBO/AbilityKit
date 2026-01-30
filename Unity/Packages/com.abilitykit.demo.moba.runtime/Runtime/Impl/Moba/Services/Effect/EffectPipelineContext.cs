using AbilityKit.Ability;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectPipelineContext : AAbilityPipelineContext
    {
        public IWorldResolver WorldServices { get; private set; }
        public IEventBus EventBus { get; private set; }

        public void Initialize(
            object abilityInstance,
            int sourceActorId,
            int targetActorId,
            int contextKind,
            long sourceContextId,
            IWorldResolver worldServices,
            IEventBus eventBus)
        {
            base.Initialize(abilityInstance);

            WorldServices = worldServices;
            EventBus = eventBus;

            SharedData[MobaEffectPipelineSharedKeys.SourceActorId] = sourceActorId;
            SharedData[MobaEffectPipelineSharedKeys.TargetActorId] = targetActorId;
            SharedData[MobaEffectPipelineSharedKeys.ContextKind] = contextKind;
            SharedData[MobaEffectPipelineSharedKeys.SourceContextId] = sourceContextId;

            // keep skill-compatible keys for triggers/effects that still read skill args
            SharedData[MobaSkillPipelineSharedKeys.SkillId] = 0;
            SharedData[MobaSkillPipelineSharedKeys.SkillSlot] = 0;
            SharedData[MobaSkillPipelineSharedKeys.CasterActorId] = sourceActorId;
            SharedData[MobaSkillPipelineSharedKeys.TargetActorId] = targetActorId;
            SharedData[MobaSkillPipelineSharedKeys.AimPos] = Vec3.Zero;
            SharedData[MobaSkillPipelineSharedKeys.AimDir] = Vec3.Forward;
        }

        public override void Reset()
        {
            base.Reset();
            WorldServices = null;
            EventBus = null;
        }
    }
}
