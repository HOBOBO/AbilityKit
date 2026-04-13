using System.Collections.Generic;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// 閰嶇疆缁勬彁渚涜€呮帴鍙ｏ紝鐢ㄤ簬 DI 娉ㄥ叆鑷畾涔夐厤缃粍
    /// 缁ф壙鑷?AbilityKit.Ability.Config.IConfigGroupProvider
    /// </summary>
    public interface IMobaConfigGroupProvider : IConfigGroupProvider
    {
    }

    /// <summary>
    /// 榛樿閰嶇疆缁勬彁渚涜€咃紝浣跨敤 MobaConfigGroups 涓畾涔夌殑缁?
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
