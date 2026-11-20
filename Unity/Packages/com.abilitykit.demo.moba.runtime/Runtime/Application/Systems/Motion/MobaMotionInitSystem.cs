using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    /// <summary>
    /// 在原 PreExecute 阶段初始化移动组件，默认运行依赖由世界服务持有。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.MotionInit, Phase = WorldSystemPhase.PreExecute)]
    public sealed class MobaMotionInitSystem : WorldSystemBase
    {
        private global::Entitas.IGroup<global::ActorEntity> _group;
        private MobaMotionRuntimeDependencyService _dependencies;

        /// <summary>
        /// 使用世界上下文及业务依赖解析器构建初始化系统。
        /// </summary>
        public MobaMotionInitSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        /// <summary>
        /// 解析正常业务依赖并建立组件分组，不提前构造求解器。
        /// </summary>
        protected override void OnInit()
        {
            /*
             * 默认依赖由世界业务服务持有，初始化仍在原正式帧阶段执行。
             */
            _dependencies = Services.Resolve<MobaMotionRuntimeDependencyService>();
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Motion));
        }

        /// <summary>
        /// 仅为尚未初始化的组件建立 Pipeline，保持原状态与配置取样规则。
        /// </summary>
        protected override void OnExecute()
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasMotion || !e.hasTransform || !e.hasActorId) continue;

                var m = e.motion;
                if (m.Initialized && m.Pipeline != null) continue;

                var t = e.transform.Value;

                var pipeline = m.Pipeline ?? new MotionPipeline();

                if (m.Policy != null)
                {
                    pipeline.Policy = m.Policy;
                }
                else
                {
                    pipeline.Policy ??= MobaMotionGroupConfigResolver.CreatePolicy(Services);
                }

                var solver = m.Solver ?? _dependencies.ResolveDefaultSolver();
                if (solver != null) pipeline.Solver = solver;
                if (m.Events != null) pipeline.Events = m.Events;

                var state = m.State;
                if (!m.Initialized)
                {
                    state = new MotionState(t.Position);
                    state.Forward = t.Forward;
                }

                var output = m.Output;
                output.Clear();

                e.ReplaceMotion(
                    newPipeline: pipeline,
                    newState: state,
                    newOutput: output,
                    newSolver: solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: true,
                    newHitTriggerRuntime: m.HitTriggerRuntime);
            }
        }

    }
}

