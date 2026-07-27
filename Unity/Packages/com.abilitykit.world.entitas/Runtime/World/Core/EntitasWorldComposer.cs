using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 负责 Entitas 世界的组合和初始化。
    /// 包含模块依赖解析、服务注册和系统安装逻辑。
    /// </summary>
    internal static class EntitasWorldComposer
    {
        /// <summary>
        /// 组合并初始化 Entitas 世界。
        /// </summary>
        /// <param name="world">要初始化的 Entitas 世界</param>
        /// <param name="options">世界创建选项</param>
        public static void Compose(EntitasWorld world, WorldCreateOptions options)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var builder = options.ServiceBuilder ?? WorldServiceContainerFactory.CreateDefaultOnly();
            var context = $"World[{world.Id.Value}/{world.WorldType}]";
            var modulePlan = WorldModulePlanner.Create(options.Modules, context);

            builder.RegisterInstance<WorldId>(world.Id);
            builder.RegisterInstance<string>(world.WorldType);
            builder.RegisterExternalInstance<IWorld>(world);
            builder.RegisterExternalInstance<IEntitasWorld>(world);
            builder.RegisterInstance<global::Entitas.IContexts>(world.Contexts);
            builder.RegisterInstance<global::Entitas.Systems>(world.Systems);
            builder.Register<IEntitasWorldContext>(
                WorldLifetime.Scoped,
                resolver => new EntitasWorldContext(
                    world.Id,
                    world.WorldType,
                    world.Contexts,
                    world.Systems,
                    resolver));
            builder.Register<IWorldContext>(
                WorldLifetime.Scoped,
                resolver => resolver.Resolve<IEntitasWorldContext>());

            for (var i = 0; i < modulePlan.Entries.Count; i++)
            {
                builder.AddModule(modulePlan.Entries[i].Module);
            }

            var container = builder.Build();
            var scope = container.CreateScope();
            world.SetComposition(container, scope);

            var logger = scope.Resolve<IWorldLogger>();
            logger.Info($"World compose start: id={world.Id.Value}, type={world.WorldType}");
            for (var i = 0; i < modulePlan.Entries.Count; i++)
            {
                var entry = modulePlan.Entries[i];
                logger.Info(
                    $"World module[{i}] (srcIndex={entry.SourceIndex}, order={entry.Order}, " +
                    $"id={entry.Id ?? "<null>"}): {entry.ModuleType.FullName}");
            }

            logger.Info($"World services registered: {container.RegisteredServiceTypes.Count}");
            foreach (var serviceType in container.RegisteredServiceTypes)
            {
                logger.Info($"World service: {serviceType.FullName}");
            }

            for (var i = 0; i < modulePlan.Entries.Count; i++)
            {
                if (modulePlan.Entries[i].Module is IEntitasSystemsInstaller installer)
                {
                    logger.Info($"World installer[{i}]: {installer.GetType().FullName}");
                    installer.Install(world.Contexts, world.Systems, scope);
                }
            }

            world.Systems.Initialize();

            var report = WorldCompositionReportBuilder.Create(
                world.Id.Value,
                world.WorldType,
                modulePlan,
                builder);
            for (var i = 0; i < modulePlan.Entries.Count; i++)
            {
                if (modulePlan.Entries[i].Module is IEntitasSystemsInstaller)
                {
                    report.AddInstaller(modulePlan.Entries[i].ModuleType.FullName);
                }
            }

            foreach (var serviceType in container.RegisteredServiceTypes)
            {
                report.AddRegisteredService(serviceType.FullName);
            }

            WorldDebugRegistry.Report(report);
            logger.Info($"World compose done: id={world.Id.Value}, type={world.WorldType}");
        }
    }
}
