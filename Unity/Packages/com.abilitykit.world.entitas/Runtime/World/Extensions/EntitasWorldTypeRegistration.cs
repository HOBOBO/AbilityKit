using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas 世界类型注册扩展方法。
    /// 用于将 EntitasWorld 注册到 WorldTypeRegistry 中。
    /// </summary>
    public static class EntitasWorldTypeRegistration
    {
        /// <summary>
        /// 将 Entitas 世界类型注册到注册表。
        /// </summary>
        /// <param name="registry">世界类型注册表</param>
        /// <param name="worldType">世界类型标识</param>
        /// <returns>返回注册表本身，用于链式调用</returns>
        public static WorldTypeRegistry RegisterEntitasWorld(this WorldTypeRegistry registry, string worldType)
        {
            return registry.Register(worldType, opts => new EntitasWorld(opts));
        }
    }
}