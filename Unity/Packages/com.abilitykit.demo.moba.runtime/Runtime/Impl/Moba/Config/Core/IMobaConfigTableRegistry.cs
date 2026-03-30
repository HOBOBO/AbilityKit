using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigTableRegistry
    {
        /// <summary>
        /// 获取所有配置表条目（向后兼容）
        /// </summary>
        BattleDemo.MobaRuntimeConfigTableRegistry.Entry[] Tables { get; }

        /// <summary>
        /// 获取所有配置表条目（MOBA 专用 API）
        /// </summary>
        BattleDemo.MobaRuntimeConfigTableRegistry.Entry[] MobaTables { get; }
    }
}
