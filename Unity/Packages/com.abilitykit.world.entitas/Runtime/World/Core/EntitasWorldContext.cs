using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas 世界的上下文和系统容器。
    /// 封装了 Entitas 上下文集合和系统集合，供系统使用。
    /// </summary>
    public sealed class EntitasWorldContext : IEntitasWorldContext
    {
        /// <summary>
        /// 创建 Entitas 世界上下文。
        /// </summary>
        /// <param name="id">世界标识</param>
        /// <param name="worldType">世界类型</param>
        /// <param name="contexts">Entitas 上下文集合</param>
        /// <param name="systems">Entitas 系统集合</param>
        /// <param name="services">服务解析器</param>
        public EntitasWorldContext(WorldId id, string worldType, global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            Id = id;
            WorldType = worldType;
            Contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <inheritdoc/>
        public WorldId Id { get; }

        /// <inheritdoc/>
        public string WorldType { get; }

        /// <inheritdoc/>
        public IWorldResolver Services { get; }

        /// <inheritdoc/>
        public global::Entitas.IContexts Contexts { get; }

        /// <inheritdoc/>
        public global::Entitas.Systems Systems { get; }
    }
}