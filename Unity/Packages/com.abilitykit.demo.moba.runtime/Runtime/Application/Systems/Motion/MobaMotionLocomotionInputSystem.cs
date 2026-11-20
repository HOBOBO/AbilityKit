using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    /// <summary>
    /// 在正式输入阶段驱动普通移动来源；关联由业务服务持有，属性和门禁仅在正常帧取样。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.MotionLocomotionInput, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMotionLocomotionInputSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaCombatRulesService _combatRules;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        private MobaLocomotionRuntimeService _locomotion;
        private MobaActorRegistry _actors;
        private int _sampleLogCount;

        /// <summary>
        /// 使用当前世界的组件上下文和服务构建普通移动输入系统。
        /// </summary>
        public MobaMotionLocomotionInputSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _combatRules);
            _locomotion = Services.Resolve<MobaLocomotionRuntimeService>();
            _actors = Services.Resolve<MobaActorRegistry>();
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Motion,
                global::ActorComponentsLookup.MoveInput));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;

            _locomotion.BeginFrame();
            var entities = _group.GetEntities();

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasActorId || !e.hasMotion || !e.hasMoveInput) continue;

                var m = e.motion;
                if (!m.Initialized || m.Pipeline == null) continue;

                var actorId = e.actorId.Value;
                if (actorId <= 0) continue;

                var version = _actors.GetEntityVersion(actorId);
                var loco = _locomotion.Visit(e, version);

                if (_combatRules != null && !_combatRules.CanMove(actorId))
                {
                    loco?.SetInput(0f, 0f);
                    continue;
                }

                var speed = new MobaAttrs(e).MoveSpeed;

                if (loco == null) loco = _locomotion.Create(e, version, speed);
                else loco.Speed = speed;

                loco.SetInput(e.moveInput.Dx, e.moveInput.Dz);

                _sampleLogCount++;
                if (_sampleLogCount <= 5 || _sampleLogCount % 60 == 0)
                {
                    Log.Info($"[MobaMotionLocomotionInputSystem] ActorId={actorId}, MoveInput=({e.moveInput.Dx:F3},{e.moveInput.Dz:F3}), Speed={speed:F3}, Dt={_clock.DeltaTime:F4}, Count={_sampleLogCount}");
                }

                e.ReplaceMotion(
                    newPipeline: m.Pipeline,
                    newState: m.State,
                    newOutput: m.Output,
                    newSolver: m.Solver,
                    newPolicy: m.Policy,
                    newEvents: m.Events,
                    newInitialized: m.Initialized,
                    newHitTriggerRuntime: m.HitTriggerRuntime);
            }

            _locomotion.EndFrame();
        }
    }
}
