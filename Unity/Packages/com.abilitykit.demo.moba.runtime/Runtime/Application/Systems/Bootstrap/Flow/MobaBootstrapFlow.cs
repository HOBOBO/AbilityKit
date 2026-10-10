using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow
{
    /// <summary>
    /// Moba Bootstrap Flow
    /// 使用 Flow 模式管理引导阶段
    /// 替代旧的 partial class 系统
    /// </summary>
    public sealed class MobaBootstrapFlow : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        private readonly MobaBootstrapStagePlan _plan;

        public MobaBootstrapFlow()
        {
            MobaBootstrapStageInitializer.Initialize();
            _plan = MobaBootstrapStageRegistry.Freeze();
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            foreach (var stage in _plan.OrderedStages)
            {
                stage.ExecuteConfigure(builder);
            }
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            foreach (var stage in _plan.OrderedStages)
            {
                stage.ExecuteInstall(contexts, systems, services);
            }
        }
    }
}
