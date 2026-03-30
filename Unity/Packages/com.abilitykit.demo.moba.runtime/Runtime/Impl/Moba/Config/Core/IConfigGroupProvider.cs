using System.Collections.Generic;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// 配置组提供者接口，用于 DI 注入自定义配置组
    /// 继承自 AbilityKit.Ability.Config.IConfigGroupProvider
    /// </summary>
    public interface IMobaConfigGroupProvider : IConfigGroupProvider
    {
    }

    /// <summary>
    /// 默认配置组提供者，使用 MobaConfigGroups 中定义的组
    /// </summary>
    public sealed class DefaultMobaConfigGroupProvider : IMobaConfigGroupProvider
    {
        public static readonly DefaultMobaConfigGroupProvider Instance = new DefaultMobaConfigGroupProvider();

        private DefaultMobaConfigGroupProvider() { }

        public IReadOnlyList<IConfigGroup> GetGroups()
        {
            return MobaConfigGroups.All;
        }
    }
}
