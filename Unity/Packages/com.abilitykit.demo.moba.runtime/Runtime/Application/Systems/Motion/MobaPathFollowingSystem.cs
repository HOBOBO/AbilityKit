using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Navigation;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    /// <summary>
    /// 寻路跟随系统（A2）。
    ///
    /// 执行顺序：BrainTick(决策) → BrainOutputApply → MotionLocomotionInput → <b>本系统</b> → MotionTick。
    ///
    /// 通过 <c>MobaBrainIntentReader</c> 读取当前目标位置与停止距离，按 actor 持有
    /// <see cref="PathFollowerMotionSource"/>（group=Path、stacking=OverrideLowerPriority），
    /// 通过 <see cref="INavigationService"/> 规划路径并喂入移动管线，由 policy（Path 抑制 Locomotion）接管移动。
    ///
    /// 与 <see cref="MobaBrainOutputApplySystem"/> 的直线 <c>MoveInput</c> 互为兜底：
    /// - 所有者跟随成功挂接路径后，仅 Path 源贡献位移；无路径时由直线兜底。
    /// - 其他目标位置意图沿用原有管线策略。
    /// - 导航不可用或寻路失败：本系统不挂源，BrainOutputApply 的直线 MoveInput 驱动移动（旧行为）。
    ///
    /// Path 运行数据由业务服务持有；本系统只在正式帧中执行导航与来源更新。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.PathFollowing, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaPathFollowingSystem : WorldSystemBase
    {
        /*
         * 目标移动超过一米或累计二十帧时重新规划；到达阈值保持为四分之一米。
         */
        private const float RepathTargetThreshold = 1.0f;
        private const int RepathIntervalFrames = 20;
        private const float ArriveEpsilon = 0.25f;
        private const float AgentRadius = 0.5f;
        private const int PathPriority = 10;

        private readonly float _repathTargetThresholdSquared = RepathTargetThreshold * RepathTargetThreshold;
        private readonly float _arriveEpsilonSquared = ArriveEpsilon * ArriveEpsilon;

        private MobaBrainService _brains;
        private MobaCombatRulesService _combatRules;
        private INavigationService _navigation;
        private NavigationDebugState _navDebug;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        private MobaPathFollowingRuntimeService _pathRuntime;
        private MobaLocomotionRuntimeService _locomotion;
        private MobaActorRegistry _actors;
        private IWorldClock _clock;

        /// <summary>
        /// 使用当前世界的组件上下文和服务建立正式路径规划系统。
        /// </summary>
        public MobaPathFollowingSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        /// <summary>
        /// 解析当前世界的规划、绑定与实体服务，并订阅具备路径输入条件的角色集合。
        /// </summary>
        protected override void OnInit()
        {
            Services.TryResolve(out _brains);
            Services.TryResolve(out _combatRules);
            Services.TryResolve(out _navigation);
            Services.TryResolve(out _navDebug);
            _pathRuntime = Services.Resolve<MobaPathFollowingRuntimeService>();
            Services.TryResolve(out _locomotion);
            _actors = Services.Resolve<MobaActorRegistry>();
            Services.TryResolve(out _clock);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain,
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Motion));
        }

        /// <summary>
        /// 在正式帧位置核验绑定并按既有意图、阈值和节奏执行规划与取消。
        /// </summary>
        protected override void OnExecute()
        {
            if (_brains == null || _group == null) return;

            var navWorld = _navigation?.World;

            _pathRuntime.BeginFrame();

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0)
            {
                _pathRuntime.EndFrame();
                return;
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasActorBrain || !e.hasTransform || !e.hasMotion) continue;

                var actorId = e.actorId.Value;
                if (actorId <= 0) continue;
                var version = _actors.GetEntityVersion(actorId);
                _pathRuntime.Visit(e, version);
                var motion = e.motion;
                if (!motion.Initialized || motion.Pipeline == null) continue;

                if (_combatRules != null && !_combatRules.CanMove(actorId))
                {
                    CancelPath(actorId);
                    continue;
                }

                if (!_brains.TryGetBehavior(e.actorBrain.BehaviorInstanceId, out var behavior) || behavior == null ||
                    behavior.Phase != BehaviorPhase.Running)
                {
                    CancelPath(actorId);
                    continue;
                }

                /*
                 * 与 BrainOutputApply 消费相同的标准意图及停止距离；
                 * 停止、施法或方向意图撤销旧的位置路径。
                 */
                var intent = MobaBrainIntentReader.Read(behavior);
                if (!intent.IsValid() || intent.MovementKind != MobaActorMovementIntentKind.TargetPosition ||
                    navWorld == null)
                {
                    CancelPath(actorId);
                    continue;
                }

                var target = intent.MoveTarget;
                var ownerPos = e.transform.Value.Position;
                var speed = new MobaAttrs(e).MoveSpeed;
                var followLimit = intent.FollowsOwner
                    ? MobaOwnerFollowMovement.ResolveSpeed(e, _actors, in intent, speed, _clock?.DeltaTime ?? 0f)
                    : float.PositiveInfinity;
                if (speed <= 0f || followLimit <= 0f || (!intent.FollowsOwner &&
                    DistanceXZSquared(in ownerPos, in target) <= intent.MoveStopRange * intent.MoveStopRange))
                {
                    CancelPath(actorId);
                    continue;
                }

                var state = _pathRuntime.Ensure(e, version);

                var data = state.Data;
                var needRepath = state.Source == null
                    || !state.Source.IsActive
                    || DistanceXZSquared(in data.LastTarget, in target) > _repathTargetThresholdSquared
                    || data.FramesSinceRepath >= RepathIntervalFrames;

                if (!needRepath)
                {
                    ApplyFollowLimit(e, version, state.Source, followLimit);
                    data.FramesSinceRepath++;
                    continue;
                }

                /*
                 * 释放旧源，无论本轮能否拿到新路径。
                 */
                _pathRuntime.Detach(state);

                if (DistanceXZSquared(in ownerPos, in target) > _arriveEpsilonSquared)
                {
                    navWorld.FindPath(in ownerPos, in target, AgentRadius, out var path);
                    if (path.HasPath)
                    {
                        _pathRuntime.Attach(state, path.Waypoints, speed, ArriveEpsilon, PathPriority);
                        ApplyFollowLimit(e, version, state.Source, followLimit);
                    }
                    else
                    {
                        data.Waypoints = null;
                    }
                }

                data.LastTarget = target;
                data.FramesSinceRepath = 0;
            }

            _pathRuntime.EndFrame();
            _pathRuntime.ProjectDebugState(_navDebug);
        }

        private void ApplyFollowLimit(global::ActorEntity actor, int version, PathFollowerMotionSource source, float limit)
        {
            source.LimitNextTickSpeed(limit);
            if (!float.IsPositiveInfinity(limit) && source.IsActive && _locomotion != null &&
                _locomotion.TryGetBinding(actor.actorId.Value, actor, version, actor.motion.Pipeline, out var fallback))
                fallback.SetInput(0f, 0f);
        }

        private void CancelPath(int actorId)
        {
            if (_pathRuntime.TryGet(actorId, out var state)) _pathRuntime.Detach(state);
        }

        /// <summary>
        /// 在世界关闭时从各自的管线移除并归还本系统持有的寻路来源。
        /// </summary>
        protected override void OnTearDown()
        {
            _pathRuntime?.Dispose();
        }

        private static float DistanceXZSquared(in Vec3 a, in Vec3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }
    }
}
