using System;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;

namespace AbilityKit.Demo.Moba.Systems
{
    internal static partial class MobaGeneratedWorldServiceManifest
    {
        public static void Configure(
            WorldContainerBuilder builder,
            WorldServiceProfile profile = WorldServiceProfile.All)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            AddGenerated(builder, profile);
        }

        static partial void AddGenerated(WorldContainerBuilder builder, WorldServiceProfile profile);
    }

    /// <summary>
    /// 聚合 MOBA 逻辑世界使用的 runtime 服务注册模块。
    /// 作为 host 的稳定入口保留，同时内部模块组仍可替换。
    /// </summary>
    public sealed class MobaServicesAutoModule : IWorldModule
    {
        private readonly Assembly _targetAssembly;

        public static readonly string[] TargetNamespacePrefixes = MobaApplicationServicesModule.NamespacePrefixes;

        public MobaServicesAutoModule() : this(null)
        {
        }

        public MobaServicesAutoModule(Assembly targetAssembly)
        {
            _targetAssembly = targetAssembly ?? typeof(MobaServicesAutoModule).Assembly;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            Log.Info($"[MobaServicesAutoModule] Configure services for assembly: {_targetAssembly.GetName().Name}");

            if (_targetAssembly == typeof(MobaServicesAutoModule).Assembly)
            {
                MobaGeneratedWorldServiceManifest.Configure(builder, WorldServiceProfile.All);
                Log.Info("[MobaServicesAutoModule] Configure done (generated manifest)");
                return;
            }

            builder.AddModule(new MobaApplicationServicesModule(_targetAssembly));
            builder.AddModule(new MobaApplicationSystemsServicesModule(_targetAssembly));
            builder.AddModule(new MobaInfrastructureServicesModule(_targetAssembly));

            Log.Info("[MobaServicesAutoModule] Configure done");
        }
    }

    public sealed class MobaApplicationServicesModule : IWorldModule
    {
        private readonly Assembly _targetAssembly;

        public static readonly string[] NamespacePrefixes = new[]
        {
            "AbilityKit.Demo.Moba.Services",
            "AbilityKit.Demo.Moba.Runtime.Application.Services",
            "AbilityKit.Demo.Moba.Gameplay",
        };

        public MobaApplicationServicesModule(Assembly targetAssembly)
        {
            _targetAssembly = targetAssembly ?? throw new ArgumentNullException(nameof(targetAssembly));
        }

        public void Configure(WorldContainerBuilder builder)
        {
            MobaServiceModuleUtil.AddAttributeModule(builder, _targetAssembly, NamespacePrefixes);
        }
    }

    public sealed class MobaApplicationSystemsServicesModule : IWorldModule
    {
        private readonly Assembly _targetAssembly;

        public static readonly string[] NamespacePrefixes = new[]
        {
            "AbilityKit.Demo.Moba.Systems",
            "AbilityKit.Demo.Moba.Runtime.Application.Systems",
        };

        public MobaApplicationSystemsServicesModule(Assembly targetAssembly)
        {
            _targetAssembly = targetAssembly ?? throw new ArgumentNullException(nameof(targetAssembly));
        }

        public void Configure(WorldContainerBuilder builder)
        {
            MobaServiceModuleUtil.AddAttributeModule(builder, _targetAssembly, NamespacePrefixes);
        }
    }

    public sealed class MobaInfrastructureServicesModule : IWorldModule
    {
        private readonly Assembly _targetAssembly;

        public static readonly string[] NamespacePrefixes = new[]
        {
            "AbilityKit.Demo.Moba.Util",
        };

        public MobaInfrastructureServicesModule(Assembly targetAssembly)
        {
            _targetAssembly = targetAssembly ?? throw new ArgumentNullException(nameof(targetAssembly));
        }

        public void Configure(WorldContainerBuilder builder)
        {
            MobaServiceModuleUtil.AddAttributeModule(builder, _targetAssembly, NamespacePrefixes);
        }
    }

    internal static class MobaServiceModuleUtil
    {
        public static void AddAttributeModule(WorldContainerBuilder builder, Assembly assembly, string[] namespacePrefixes)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            builder.AddModule(new AttributeWorldServicesModule(
                WorldServiceProfile.All,
                assemblies: new[] { assembly },
                namespacePrefixes: namespacePrefixes));
        }
    }
}
