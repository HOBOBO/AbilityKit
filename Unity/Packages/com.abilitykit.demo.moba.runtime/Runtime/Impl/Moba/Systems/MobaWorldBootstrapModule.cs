using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            ExecuteConfigurePipeline(builder);
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            Log.Info("[MobaWorldBootstrapModule] Install: begin");
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            AutoSystemInstaller.Install(
                contexts,
                systems,
                services,
                assemblies: new[] { typeof(MobaWorldBootstrapModule).Assembly },
                namespacePrefixes: new[]
                {
                    "AbilityKit.Ability.Share.Impl.Moba",
                    "AbilityKit.Ability.Share.Common.Projectile",
                }
            );

            Log.Info("[MobaWorldBootstrapModule] Install: AutoSystemInstaller.Install done");

            ExecuteInstallPipeline(contexts, systems, services);
        }
    }
}

