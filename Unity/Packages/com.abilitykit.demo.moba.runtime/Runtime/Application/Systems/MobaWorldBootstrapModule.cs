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

    /// <summary>
    /// 合并生成的业务系统和显式外部物理系统，校验后按阶段建立唯一执行树。
    /// </summary>
    internal static partial class MobaGeneratedWorldSystemManifest
    {
        /// <summary>
        /// 在创建任何系统实例前校验身份与阶段顺序，再按声明顺序安装。
        /// 此入口只能为同一世界调用一次，不负责重排已经安装的其它树。
        /// </summary>
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
            if (descriptors.Count == 0)
                throw new InvalidOperationException(
                    "The generated MOBA world system manifest is empty. Battle world systems cannot be installed.");

            /*
             * 外部物理系统与生成的业务系统先合并，再建立唯一阶段树。
             * Entitas 按添加顺序执行，分批安装无法通过声明顺序重排已有树。
             */
            var projectileType = typeof(AbilityKit.Combat.Projectile.ProjectileTickSystem);
            var projectileAttribute = (WorldSystemAttribute)Attribute.GetCustomAttribute(projectileType, typeof(WorldSystemAttribute));
            if (projectileAttribute == null)
                throw new InvalidOperationException("ProjectileTickSystem requires a WorldSystem declaration.");
            descriptors.Add(new MobaGeneratedWorldSystemDescriptor(
                projectileAttribute.Phase, projectileAttribute.Order, projectileType.FullName,
                (c, s) => new AbilityKit.Combat.Projectile.ProjectileTickSystem(c, s)));

            var types = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<(WorldSystemPhase, int)>();
            foreach (var descriptor in descriptors)
            {
                if (descriptor.Phase < WorldSystemPhase.PreExecute || descriptor.Phase > WorldSystemPhase.PostExecute)
                    throw new InvalidOperationException($"Invalid MOBA system phase: {descriptor.TypeName} ({descriptor.Phase}).");
                if (!types.Add(descriptor.TypeName))
                    throw new InvalidOperationException($"Duplicate MOBA system: {descriptor.TypeName}.");
                if (!orders.Add((descriptor.Phase, descriptor.Order)))
                    throw new InvalidOperationException($"Conflicting MOBA system order: {descriptor.Phase}/{descriptor.Order} ({descriptor.TypeName}).");
            }
            descriptors.Sort((a, b) =>
            {
                var phase = a.Phase.CompareTo(b.Phase);
                if (phase != 0) return phase;
                var order = a.Order.CompareTo(b.Order);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.TypeName, b.TypeName);
            });

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
    /// 装配 MOBA 唯一战斗阶段树，并执行已注册的引导阶段。
    /// </summary>
    public sealed partial class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        private static readonly MobaBootstrapFlow _flowBootstrap;

        static MobaWorldBootstrapModule()
        {
            /*
             * 冻结引导阶段前先完成模块的静态注册。
             */
            MobaBootstrapFlowModule.EnsureInitialized();
            _flowBootstrap = new MobaBootstrapFlow();
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            _flowBootstrap.Configure(builder);
        }

        /// <summary>
        /// 统一安装物理及业务系统，再完成引导接线；不追加重复的同步或清理系统。
        /// </summary>
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

            _flowBootstrap.Install(contexts, systems, services);
        }
    }
}

