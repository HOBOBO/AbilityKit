using AbilityKit.Ability;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Eventing;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectPipelineContext : AAbilityPipelineContext
    {
        public IWorldResolver WorldServices { get; private set; }
        public AbilityKit.Triggering.Eventing.IEventBus EventBus { get; private set; }

        public void Initialize(
            object abilityInstance,
            int sourceActorId,
            int targetActorId,
            int contextKind,
            long sourceContextId,
            IWorldResolver worldServices,
            AbilityKit.Triggering.Eventing.IEventBus eventBus)
        {
            base.Initialize(abilityInstance);

            WorldServices = worldServices;
            EventBus = eventBus;

            // 使用强类型键枚举
            this.SetParticipants(sourceActorId, targetActorId);
            this.SetContextKind(contextKind);
            this.SetSourceContextId(sourceContextId);

            FillSkillCompatibleKeys(sourceActorId, targetActorId);
        }

        private void FillSkillCompatibleKeys(int sourceActorId, int targetActorId)
        {
            // keep skill-compatible keys for triggers/effects that still read skill args
            this.SetSkillInfo(0, 0, 0);
            this.SetParticipants(sourceActorId, targetActorId);
            Vec3 zero = Vec3.Zero;
            Vec3 forward = Vec3.Forward;
            this.SetAim(in zero, in forward);
        }

        public override void Reset()
        {
            base.Reset();
            WorldServices = null;
            EventBus = null;
        }
    }
}
