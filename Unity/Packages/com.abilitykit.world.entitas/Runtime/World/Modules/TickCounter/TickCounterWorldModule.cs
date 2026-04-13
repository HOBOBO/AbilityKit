using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Tick计数器世界模块。
    /// 注册 WorldTickCounter 服务并安装 TickCounterSystem。
    /// </summary>
    public sealed class TickCounterWorldModule : IWorldModule, IEntitasSystemsInstaller
    {
        /// <summary>
        /// 配置模块，注册 WorldTickCounter 服务。
        /// </summary>
        /// <param name="builder">服务构建器</param>
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Register<WorldTickCounter>(WorldLifetime.Scoped, _ => new WorldTickCounter());
        }

        /// <summary>
        /// 安装 TickCounterSystem 到 Entitas 系统容器中。
        /// </summary>
        /// <param name="contexts">Entitas 上下文集合（未使用）</param>
        /// <param name="systems">Entitas 系统容器</param>
        /// <param name="services">服务解析器</param>
        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var counter = services.Resolve<WorldTickCounter>();
            var ctx = services.Resolve<IWorldContext>();
            systems.Add(new TickCounterSystem(ctx, counter));
        }
    }
}