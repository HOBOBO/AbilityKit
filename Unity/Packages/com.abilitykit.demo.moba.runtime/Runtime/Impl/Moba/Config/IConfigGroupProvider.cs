using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    /// <summary>
    /// 配置组提供者接口，用于 DI 注入自定义配置组
    /// </summary>
    public interface IConfigGroupProvider
    {
        /// <summary>
        /// 获取所有配置组
        /// </summary>
        IReadOnlyList<IConfigGroup> GetGroups();
    }

    /// <summary>
    /// 默认配置组提供者，使用 MobaConfigGroups 中定义的组
    /// </summary>
    public sealed class DefaultConfigGroupProvider : IConfigGroupProvider
    {
        public static readonly DefaultConfigGroupProvider Instance = new DefaultConfigGroupProvider();

        private DefaultConfigGroupProvider() { }

        public IReadOnlyList<IConfigGroup> GetGroups()
        {
            return MobaConfigGroups.All;
        }
    }
}
