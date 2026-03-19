using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Game.Battle.Moba.Config
{
    /// <summary>
    /// 配置模块 - 复用运行时包的 MobaConfigDatabase 注册
    /// 视图包不需要重新注册配置，因为运行时包已经通过 MobaWorldBootstrapModule.Stage.Config 完成了注册
    /// </summary>
    public sealed class MobaConfigWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            // 注意：MobaConfigDatabase 已经在 MobaWorldBootstrapModule.Stage.Config 中注册
            // 这里不需要重新注册，如果需要强制使用特定格式，可以通过 IMobaConfigFormatProvider 注入
        }
    }
}
