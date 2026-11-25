using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems.Combat
{
    /// <summary>
    /// 在本帧决策与技能费用检查前回复资源；只消费正式世界步长。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.ResourceRegeneration, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaResourceRegenerationSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaResourceRegenerationService _regeneration;

        /// <summary>
        /// 由正式生成装配创建，不安装独立 Tick 树。
        /// </summary>
        public MobaResourceRegenerationSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services) { }

        /// <summary>
        /// 必要依赖缺失时由正式装配报错，不静默关闭已配置的回复能力。
        /// </summary>
        protected override void OnInit()
        {
            _clock = Services.Resolve<IWorldClock>();
            _regeneration = Services.Resolve<MobaResourceRegenerationService>();
        }

        /// <summary>
        /// 世界暂停时没有额外补时；恢复仅从下一次正常帧继续。
        /// </summary>
        protected override void OnExecute() => _regeneration.Tick(_clock.DeltaTime);
    }
}
