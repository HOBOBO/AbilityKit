using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 閰嶇疆琛ㄦ敞鍐屽櫒鎺ュ彛锛堟墿灞曢€氱敤 IConfigTableRegistry锛?
    /// </summary>
    public interface IMobaConfigTableRegistry : IConfigTableRegistry
    {
        /// <summary>
        /// 鑾峰彇鎵€鏈夐厤缃〃鏉＄洰锛圡OBA 涓撶敤 API锛?
        /// </summary>
        BattleDemo.MobaRuntimeConfigTableRegistry.Entry[] MobaTables { get; }
    }
}
