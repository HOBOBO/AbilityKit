using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;
using AbilityKit.ECS;

namespace AbilityKit.Demo.Moba.Systems
{
    internal readonly struct MobaGeneratedWorldSystemDescriptor
    {
        public readonly WorldSystemPhase Phase;
        public readonly int Order;
        public readonly string TypeName;
        public readonly Func<global::Entitas.IContexts, IWorldResolver, global::Entitas.ISystem> Factory;

        public MobaGeneratedWorldSystemDescriptor(
            WorldSystemPhase phase,
            int order,
            string typeName,
            Func<global::Entitas.IContexts, IWorldResolver, global::Entitas.ISystem> factory)
        {
            Phase = phase;
            Order = order;
            TypeName = typeName ?? string.Empty;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }

    internal static partial class MobaGeneratedWorldSystemManifest
    {
        public static void Install(
            global::Entitas.IContexts contexts,
            global::Entitas.Systems systems,
            IWorldResolver services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var descriptors = new List<MobaGeneratedWorldSystemDescriptor>();
            AddGenerated(descriptors);
            if (descriptors.Count == 0) return;

            var features = new Dictionary<WorldSystemPhase, global::Entitas.Systems>();
            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                if (!features.TryGetValue(descriptor.Phase, out var feature))
                {
                    feature = new global::Entitas.Systems();
                    features.Add(descriptor.Phase, feature);
                }

                feature.Add(descriptor.Factory(contexts, services));
            }

            for (var phase = WorldSystemPhase.PreExecute; phase <= WorldSystemPhase.PostExecute; phase++)
            {
                if (features.TryGetValue(phase, out var feature)) systems.Add(feature);
            }
        }

        static partial void AddGenerated(List<MobaGeneratedWorldSystemDescriptor> descriptors);
    }

    /// <summary>
    /// Moba World Bootstrap Module
    /// 委托给新的 Flow Bootstrap 系统
    /// </summary>
    public sealed partial class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        private static readonly MobaBootstrapFlow _flowBootstrap;

        static MobaWorldBootstrapModule()
        {
            // 触发静态初始化，确保所有 Stage 被注册
            MobaBootstrapFlowModule.EnsureInitialized();
            _flowBootstrap = new MobaBootstrapFlow();
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            _flowBootstrap.Configure(builder);
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (contexts is not global::Contexts)
            {
                Log.Warning("[MobaWorldBootstrapModule] Install: IContexts is not generated Contexts type, manual registration may be needed");
            }

            MobaGeneratedWorldSystemManifest.Install(contexts, systems, services);

            AutoSystemInstaller.Install(
                contexts,
                systems,
                services,
                assemblies: new[] { typeof(AbilityKit.Combat.Projectile.ProjectileTickSystem).Assembly },
                namespacePrefixes: new[]
                {
                    "AbilityKit.Combat.Projectile",
                }
            );

            _flowBootstrap.Install(contexts, systems, services);
        }
    }
}

