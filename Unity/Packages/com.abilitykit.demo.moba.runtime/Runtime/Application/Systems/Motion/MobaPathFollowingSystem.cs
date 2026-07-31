using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Navigation;

namespace AbilityKit.Demo.Moba.Systems.Motion
{
    /// <summary>
    /// 寻路跟随系统（A2）。
    ///
    /// 执行顺序：BrainTick(决策) → BrainOutputApply → MotionLocomotionInput → <b>本系统</b> → MotionTick。
    ///
    /// 直接读取脑决策输出 <c>behavior.Output.Movement.TargetPosition</c>，按 actor 持有
    /// <see cref="PathFollowerMotionSource"/>（group=Path、stacking=OverrideLowerPriority），
    /// 通过 <see cref="INavigationService"/> 规划路径并喂入移动管线，由 policy（Path 抑制 Locomotion）接管移动。
    ///
    /// 与 <see cref="MobaBrainOutputApplySystem"/> 的直线 <c>MoveInput</c> 互为兜底：
    /// - 导航世界可用且能规划路径：本系统驱动 Path 源，policy 抑制 Locomotion。
    /// - 导航不可用或寻路失败：本系统不挂源，BrainOutputApply 的直线 MoveInput 驱动移动（旧行为）。
    ///
    /// 生命周期管理镜像 <see cref="MobaMotionLocomotionInputSystem"/>：per-actor 源字典 + stamp 失活清扫。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.PathFollowing, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaPathFollowingSystem : WorldSystemBase
    {
        private const float RepathTargetThreshold = 1.0f;   // 目标移动超过此距离触发重算
        private const int RepathIntervalFrames = 20;        // 强制重算周期（帧）
        private const float ArriveEpsilon = 0.25f;          // 视为到达的距离
        private const float AgentRadius = 0.5f;
        private const int PathPriority = 10;

        private readonly float _repathTargetThresholdSquared = RepathTargetThreshold * RepathTargetThreshold;
        private readonly float _arriveEpsilonSquared = ArriveEpsilon * ArriveEpsilon;

        private MobaBrainService _brains;
        private MobaCombatRulesService _combatRules;
        private INavigationService _navigation;
        private NavigationDebugState _navDebug;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        private readonly Dictionary<int, PathFollowingState> _stateByActorId = new Dictionary<int, PathFollowingState>(64);
        private readonly Dictionary<int, int> _seenStampByActorId = new Dictionary<int, int>(64);
        private readonly List<int> _tmpRemoveActorIds = new List<int>(32);
        private int _stamp;

        public MobaPathFollowingSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _brains);
            Services.TryResolve(out _combatRules);
            Services.TryResolve(out _navigation);
            Services.TryResolve(out _navDebug);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain,
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Motion));
        }

        protected override void OnExecute()
        {
            if (_brains == null || _group == null) return;

            var navWorld = _navigation?.World;

            _stamp++;
            if (_stamp == int.MaxValue) _stamp = 1;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0)
            {
                SweepStale();
                return;
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasActorBrain || !e.hasTransform || !e.hasMotion) continue;

                var actorId = e.actorId.Value;
                if (actorId <= 0) continue;
                _seenStampByActorId[actorId] = _stamp;

                var motion = e.motion;
                if (!motion.Initialized || motion.Pipeline == null) continue;

                if (_combatRules != null && !_combatRules.CanMove(actorId))
                {
                    CancelPath(actorId, motion.Pipeline);
                    continue;
                }

                if (!_brains.TryGetBehavior(e.actorBrain.BehaviorInstanceId, out var behavior) || behavior == null) continue;
                if (behavior.Phase != BehaviorPhase.Running) continue;

                var movement = behavior.Output.Movement;
                if (!movement.HasValue || !movement.Value.TargetPosition.HasValue)
                {
                    CancelPath(actorId, motion.Pipeline);
                    continue;
                }

                // 无导航世界时让 BrainOutputApply 的直线 MoveInput 兜底。
                if (navWorld == null) continue;

                var target = movement.Value.TargetPosition.Value;
                var ownerPos = e.transform.Value.Position;
                var speed = new MobaAttrs(e).MoveSpeed;
                if (speed <= 0f) continue;

                var state = EnsureState(actorId);

                var needRepath = state.Source == null
                    || !state.Source.IsActive
                    || DistanceXZSquared(in state.LastTarget, in target) > _repathTargetThresholdSquared
                    || state.FramesSinceRepath >= RepathIntervalFrames;

                if (!needRepath)
                {
                    state.FramesSinceRepath++;
                    continue;
                }

                // 释放旧源（无论本轮能否拿到新路径）。
                if (state.Source != null)
                {
                    motion.Pipeline.RemoveSource(state.Source);
                    PathFollowerMotionSource.Release(state.Source);
                    state.Source = null;
                }

                if (DistanceXZSquared(in ownerPos, in target) > _arriveEpsilonSquared)
                {
                    navWorld.FindPath(in ownerPos, in target, AgentRadius, out var path);
                    if (path.HasPath)
                    {
                        state.Source = PathFollowerMotionSource.Rent(
                            path.Waypoints,
                            speed,
                            arriveEpsilon: ArriveEpsilon,
                            priority: PathPriority,
                            groupId: MotionGroups.Path,
                            stacking: MotionStacking.OverrideLowerPriority);
                        motion.Pipeline.AddSource(state.Source);
                        state.Waypoints = path.Waypoints;
                    }
                    else
                    {
                        state.Waypoints = null;
                    }
                }

                state.LastTarget = target;
                state.FramesSinceRepath = 0;
            }

            SweepStale();
            WriteDebugState();
        }

        private void WriteDebugState()
        {
            if (_navDebug == null || _stateByActorId.Count == 0) return;

            var entries = new List<ActivePathEntry>(_stateByActorId.Count);
            foreach (var kv in _stateByActorId)
            {
                var state = kv.Value;
                if (state.Waypoints == null || state.Waypoints.Length == 0) continue;
                entries.Add(new ActivePathEntry(
                    kv.Key, state.Waypoints,
                    in state.LastTarget,
                    Vec3.Zero)); // ownerPos 由 Gizmo 侧从 Transform 取
            }

            _navDebug.SetPaths(entries);
        }

        private PathFollowingState EnsureState(int actorId)
        {
            if (!_stateByActorId.TryGetValue(actorId, out var state))
            {
                state = new PathFollowingState();
                _stateByActorId[actorId] = state;
            }

            return state;
        }

        private void CancelPath(int actorId, MotionPipeline pipeline)
        {
            if (!_stateByActorId.TryGetValue(actorId, out var state) || state.Source == null) return;
            pipeline?.RemoveSource(state.Source);
            PathFollowerMotionSource.Release(state.Source);
            state.Source = null;
        }

        private void SweepStale()
        {
            if (_stateByActorId.Count == 0) return;

            _tmpRemoveActorIds.Clear();
            foreach (var kv in _stateByActorId)
            {
                if (!_seenStampByActorId.TryGetValue(kv.Key, out var s) || s != _stamp)
                {
                    _tmpRemoveActorIds.Add(kv.Key);
                }
            }

            for (int i = 0; i < _tmpRemoveActorIds.Count; i++)
            {
                var id = _tmpRemoveActorIds[i];
                if (_stateByActorId.TryGetValue(id, out var state) && state.Source != null)
                {
                    PathFollowerMotionSource.Release(state.Source);
                }

                _stateByActorId.Remove(id);
                _seenStampByActorId.Remove(id);
            }
        }

        private static float DistanceXZSquared(in Vec3 a, in Vec3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private sealed class PathFollowingState
        {
            public PathFollowerMotionSource Source;
            public Vec3 LastTarget;
            public Vec3[] Waypoints;
            public int FramesSinceRepath;
        }
    }
}
