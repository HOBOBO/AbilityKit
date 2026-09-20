using AbilityKit.Ability;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Pipeline;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 效果执行期使用的管线上下文。
    /// 负责承接能力管线需要的基础数据，并保留与触发/效果兼容的数据键。
    /// </summary>
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

            // 使用强类型键枚举写入参与者信息
            this.SetParticipants(sourceActorId, targetActorId);
            this.SetContextKind(contextKind);
            this.SetSourceContextId(sourceContextId);

            FillSkillCompatibleKeys(sourceActorId, targetActorId);
        }

        private void FillSkillCompatibleKeys(int sourceActorId, int targetActorId)
        {
            // 保留技能兼容键，供仍然读取技能参数的触发/效果链路使用
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
