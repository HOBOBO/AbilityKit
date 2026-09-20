using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// TargetingAndSkills 阶段。
    /// 注册事件订阅与触发器索引服务。
    /// </summary>
    [MobaBootstrapStage]
    public sealed class TargetingAndSkillsStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.TargetingAndSkills;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.WorldModules,
        };

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            builder.TryRegister<MobaEventSubscriptionRegistry>(WorldLifetime.Singleton, _ =>
            {
                var reg = new MobaEventSubscriptionRegistry();
                reg.DiscoverAndRegister();
                return reg;
            });

        }
    }
}
