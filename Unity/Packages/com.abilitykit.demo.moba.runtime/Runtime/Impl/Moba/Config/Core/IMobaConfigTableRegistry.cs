using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 配置表注册器接口（扩展通用 IConfigTableRegistry）
    /// </summary>
    public interface IMobaConfigTableRegistry : IConfigTableRegistry
    {
        /// <summary>
        /// 获取所有配置表条目（MOBA 专用 API）
        /// </summary>
        BattleDemo.MobaRuntimeConfigTableRegistry.Entry[] MobaTables { get; }
    }
}
