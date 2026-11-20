using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Navigation;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 持有路径规划数据及同代实体、管线和池化来源的关联。
    /// 仅由正式路径系统推进；局部值恢复不承担世界名单或导航依赖恢复。
    /// </summary>
    [WorldService(typeof(MobaPathFollowingRuntimeService))]
    public sealed class MobaPathFollowingRuntimeService : IService
    {
        /// <summary>
        /// 保存规划权威值及可由来源路径重建的调试镜像。
        /// </summary>
        internal sealed class PathData
        {
            internal Vec3 LastTarget;
            internal Vec3[] Waypoints;
            internal int FramesSinceRepath;
        }

        /// <summary>
        /// 记录一个角色的原实体、管线及来源租用期间的本地修订。
        /// </summary>
        internal sealed class Binding
        {
            internal global::ActorEntity Actor;
            internal int Version;
            internal MotionPipeline Pipeline;
            internal PathFollowerMotionSource Source;
            internal long Revision;
            internal readonly PathData Data = new PathData();
        }

        /// <summary>
        /// 将纯数据与仅供同会话局部验证的原引用凭证分开保存，不可作为世界载荷序列化。
        /// </summary>
        internal sealed class LocalCapture
        {
            internal MobaPathFollowingState State;
            internal readonly MobaPathFollowingRuntimeService Owner;
            internal readonly global::ActorEntity Actor;
            internal readonly MotionPipeline Pipeline;
            internal readonly Binding Binding;
            internal readonly PathFollowerMotionSource Source;
            internal readonly long Revision;
            internal readonly IMotionSource[] Order;
            internal readonly int ActorId;
            internal readonly int Version;

            /// <summary>
            /// 固定捕获凭证；纯数据仍可独立检查，不能据其内容伪造运行依赖。
            /// </summary>
            internal LocalCapture(MobaPathFollowingRuntimeService owner, global::ActorEntity actor,
                int version, Binding binding, IMotionSource[] order, MobaPathFollowingState state, MotionPipeline pipeline)
            {
                Owner = owner;
                Actor = actor;
                Pipeline = pipeline;
                Binding = binding;
                Source = binding?.Source;
                Revision = binding?.Revision ?? 0;
                Order = order;
                ActorId = state.ActorId;
                Version = version;
                State = state;
            }
        }

        private readonly MobaActorRegistry _actors;
        [WorldInject(required: false)] private NavigationDebugState _debug = null;
        private readonly Dictionary<int, Binding> _bindings = new Dictionary<int, Binding>(64);
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removed = new List<int>(32);

        /// <summary>
        /// 使用本世界的实体注册表核对绑定身份，不依赖回滚服务或命令日志。
        /// </summary>
        public MobaPathFollowingRuntimeService(MobaActorRegistry actors)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        internal int Count => _bindings.Count;

        internal IEnumerable<int> BoundActorIds => _bindings.Keys;

        /// <summary>
        /// 开始正式帧遍历，不捕获历史状态。
        /// </summary>
        internal void BeginFrame() => _seen.Clear();

        /// <summary>
        /// 访问当前实体；失效绑定只从原管线解绑自身来源，再由正常业务决定是否重新创建。
        /// </summary>
        internal void Visit(global::ActorEntity actor, int version)
        {
            var id = actor.actorId.Value;
            _seen.Add(id);
            if (!_bindings.TryGetValue(id, out var binding)) return;
            if (version > 0 && ReferenceEquals(binding.Actor, actor) && binding.Version == version &&
                actor.hasMotion && actor.motion.Initialized && ReferenceEquals(binding.Pipeline, actor.motion.Pipeline) &&
                _actors.TryGet(id, out var registered) && ReferenceEquals(registered, actor) &&
                _actors.MatchesEntityVersion(id, version)) return;
            Remove(id);
        }

        /// <summary>
        /// 查询当前绑定，不创建来源或修改规划值。
        /// </summary>
        internal bool TryGet(int actorId, out Binding binding) => _bindings.TryGetValue(actorId, out binding);

        /// <summary>
        /// 在正常规划入口确保同代角色绑定存在，不在恢复中调用。
        /// </summary>
        internal Binding Ensure(global::ActorEntity actor, int version)
        {
            var id = actor.actorId.Value;
            var live = RequireActor(id, version);
            if (!ReferenceEquals(live, actor)) throw new InvalidOperationException("Path actor reference differs from its registry.");
            if (_bindings.TryGetValue(id, out var binding))
            {
                RequireBinding(binding, actor, version);
                return binding;
            }
            binding = new Binding { Actor = actor, Version = version, Pipeline = actor.motion.Pipeline };
            _bindings.Add(id, binding);
            return binding;
        }

        /// <summary>
        /// 在正常规划成功后租用并挂接来源，修订号防止旧凭证跨来源复用。
        /// </summary>
        internal void Attach(Binding binding, Vec3[] points, float speed, float epsilon, int priority)
        {
            if (binding.Source != null) throw new InvalidOperationException("Path binding already owns a source.");
            var revision = checked(binding.Revision + 1);
            var source = PathFollowerMotionSource.Rent(points, speed, epsilon, priority,
                MotionGroups.Path, MotionStacking.OverrideLowerPriority);
            try
            {
                binding.Pipeline.AddSource(source);
            }
            catch
            {
                /*
                 * 正常挂接失败也必须解除可能已加入的来源并归池，避免留下无人持有的租用对象。
                 */
                if (binding.Pipeline.SourceCount > 0) binding.Pipeline.RemoveSource(source);
                PathFollowerMotionSource.Release(source);
                throw;
            }
            binding.Revision = revision;
            binding.Source = source;
            binding.Data.Waypoints = points;
        }

        /// <summary>
        /// 正常取消或重规划时解绑并归还自身来源，保留目标和规划帧数。
        /// </summary>
        internal void Detach(Binding binding)
        {
            if (binding.Source != null)
            {
                var revision = checked(binding.Revision + 1);
                if (binding.Pipeline.SourceCount > 0) binding.Pipeline.RemoveSource(binding.Source);
                PathFollowerMotionSource.Release(binding.Source);
                binding.Source = null;
                binding.Revision = revision;
            }
            binding.Data.Waypoints = null;
        }

        /// <summary>
        /// 删除本服务绑定，不取消其它业务的来源。
        /// </summary>
        internal void Remove(int actorId)
        {
            if (!_bindings.TryGetValue(actorId, out var binding)) return;
            Detach(binding);
            _bindings.Remove(actorId);
        }

        /// <summary>
        /// 清理正式帧未访问的绑定。
        /// </summary>
        internal void EndFrame()
        {
            _removed.Clear();
            foreach (var pair in _bindings)
                if (!_seen.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (var actorId in _removed) Remove(actorId);
        }

        /// <summary>
        /// 显式捕获一个角色的独立值状态及原绑定凭证；不捕获世界名单或导航输入。
        /// </summary>
        internal LocalCapture CaptureState(int actorId)
            => CaptureState(actorId, true);

        /// <summary>
        /// 世界名单调用方另外核对整个管线；纯值捕获不重复复制成员次序。
        /// </summary>
        internal LocalCapture CaptureState(int actorId, bool captureOrder, int? restoreFrame = null)
        {
            var version = _actors.GetEntityVersion(actorId);
            global::ActorEntity actor;
            MotionPipeline pipeline;
            if (!captureOrder && restoreFrame.HasValue &&
                _actors.TryGetRetainedForRecovery(actorId, restoreFrame.Value, out var held, out actor) &&
                held.hasMotion && held.motion.Initialized && held.motion.Pipeline != null)
                pipeline = held.motion.Pipeline;
            else
            {
                actor = RequireActor(actorId, version);
                pipeline = actor.motion.Pipeline;
            }
            _bindings.TryGetValue(actorId, out var binding);
            if (binding != null) RequireBinding(binding, actor, version, pipeline);
            var source = binding?.Source;
            var state = new MobaPathFollowingState
            {
                ActorId = actorId, EntityVersion = version, BindingExists = binding != null,
                SourceExists = source != null, IsPipelineMember = source != null && pipeline.ContainsSource(source),
                LastTarget = binding?.Data.LastTarget ?? Vec3.Zero, FramesSinceRepath = binding?.Data.FramesSinceRepath ?? 0,
                Source = source == null ? (PathFollowerState?)null : source.CaptureState(),
            };
            var result = new LocalCapture(this, actor, version, binding,
                captureOrder ? pipeline.CaptureSourceOrder() : null, state, pipeline);
            if (captureOrder) ValidateState(result);
            else ValidateValues(in state);
            return result;
        }

        /// <summary>
        /// 只读核对充分状态、实体代际、原来源修订及完整成员次序，拒绝任何结构恢复。
        /// </summary>
        internal void ValidateState(LocalCapture capture)
        {
            if (capture == null || !ReferenceEquals(capture.Owner, this))
                throw new InvalidOperationException("Path local capture belongs to another service or is missing.");
            var state = capture.State;
            var actor = RequireActor(capture.ActorId, capture.Version);
            if (!ReferenceEquals(actor, capture.Actor) || !ReferenceEquals(actor.motion.Pipeline, capture.Pipeline) ||
                state.ActorId != capture.ActorId || state.EntityVersion != capture.Version)
                throw new InvalidOperationException("Path local capture actor identity or Pipeline changed.");
            _bindings.TryGetValue(capture.ActorId, out var binding);
            if (!ReferenceEquals(binding, capture.Binding) || state.BindingExists != (binding != null) ||
                state.SourceExists != (capture.Source != null) || state.SourceExists != state.Source.HasValue ||
                state.IsPipelineMember != (capture.Source != null && capture.Pipeline.ContainsSource(capture.Source)))
                throw new InvalidOperationException("Path local capture requires unchanged binding and source membership.");
            if (binding != null)
            {
                RequireBinding(binding, actor, capture.Version);
                if (!ReferenceEquals(binding.Source, capture.Source) || binding.Revision != capture.Revision)
                    throw new InvalidOperationException("Path source lease changed since local capture.");
            }
            var order = capture.Pipeline.CaptureSourceOrder();
            if (order.Length != capture.Order.Length) throw new InvalidOperationException("Path Pipeline membership changed.");
            var ownedCount = 0;
            for (var i = 0; i < order.Length; i++)
            {
                if (!ReferenceEquals(order[i], capture.Order[i])) throw new InvalidOperationException("Path Pipeline order changed.");
                if (!(order[i] is PathFollowerMotionSource path)) continue;
                if (!ReferenceEquals(path, capture.Source) || ++ownedCount > 1)
                    throw new InvalidOperationException("Path Pipeline contains an external or duplicated Path source.");
            }
            ValidateValues(in state);
        }

        /// <summary>
        /// 独立于本地引用校验结构和值，不允许活跃来源脱离管线。
        /// </summary>
        internal static void ValidateValues(in MobaPathFollowingState state)
        {
            if (state.SourceExists != state.Source.HasValue || !state.BindingExists && state.SourceExists ||
                !state.SourceExists && state.IsPipelineMember ||
                !Finite(state.LastTarget.X) || !Finite(state.LastTarget.Y) || !Finite(state.LastTarget.Z) ||
                state.FramesSinceRepath < 0 || state.FramesSinceRepath > 20 ||
                !state.BindingExists && (!state.LastTarget.Equals(Vec3.Zero) || state.FramesSinceRepath != 0))
                throw new InvalidOperationException("Path planning state must have a finite target and repath frame in [0, 20].");
            if (state.Source.HasValue)
            {
                var source = state.Source.Value;
                PathFollowerMotionSource.ValidateState(in source);
                if (source.IsActive && !state.IsPipelineMember)
                    throw new InvalidOperationException("An active Path source must remain in its owning Pipeline.");
            }
        }

        /// <summary>
        /// 安装目标绑定和值，不修改管线成员、不归池；联合事务负责次序与旧引用的寿命。
        /// 补偿可传入旧绑定及来源。新租用来源在发布前记录，便于失败时回收。
        /// </summary>
        internal void RestoreWorldSlot(LocalCapture live, in MobaPathFollowingState state, Binding target,
            ref PathFollowerMotionSource rented)
        {
            var actor = RequireActor(state.ActorId, state.EntityVersion);
            if (!ReferenceEquals(actor, live.Actor) || !ReferenceEquals(actor.motion.Pipeline, live.Pipeline))
                throw new InvalidOperationException("Path world slot requires its original actor Pipeline.");
            ValidateValues(in state);
            if (!state.BindingExists)
            {
                _bindings.Remove(state.ActorId);
                return;
            }
            target ??= new Binding { Actor = actor, Version = state.EntityVersion, Pipeline = live.Pipeline };
            RequireBinding(target, actor, state.EntityVersion);
            if (state.SourceExists != (target.Source != null) && target.Source != null)
                throw new InvalidOperationException("Path world slot cannot discard a leased source in place.");
            if (state.Source.HasValue)
            {
                var value = state.Source.Value;
                var mirror = (Vec3[])value.Points.Clone();
                if (target.Source == null) target.Source = rented = PathFollowerMotionSource.RentForRestore(in value);
                else target.Source.RestoreState(in value);
                target.Data.Waypoints = mirror;
            }
            else target.Data.Waypoints = null;
            target.Data.LastTarget = state.LastTarget;
            target.Data.FramesSinceRepath = state.FramesSinceRepath;
            _bindings[state.ActorId] = target;
        }

        /// <summary>
        /// 从已恢复的模拟值刷新调试投影；空绑定名单也清理历史路径。
        /// </summary>
        internal void RefreshRollbackDebug()
        {
            if (_debug != null) _debug.SetPaths(BuildDebugPaths(0, null, Vec3.Zero));
        }

        /// <summary>
        /// 静默恢复同绑定的值；先完成所有副本和调试投影准备，最后导入来源并赋值。
        /// 不运行正常创建、寻路、门禁或通知，也不修改来源成员、次序或本地修订。
        /// </summary>
        internal void RestoreState(LocalCapture capture)
        {
            ValidateState(capture);
            var state = capture.State;
            var binding = capture.Binding;
            var mirror = state.Source.HasValue ? (Vec3[])state.Source.Value.Points.Clone() : null;
            var debug = _debug == null ? null : BuildDebugPaths(capture.ActorId, mirror, state.LastTarget);
            if (debug != null && _debug.ActivePaths.Capacity < debug.Count) _debug.ActivePaths.Capacity = debug.Count;
            /*
             * 来源恢复先验证并复制，失败时其自身零写入。它是最后一个可能分配或校验失败的步骤；
             * 后续只赋规划值和已准备的调试镜像，列表容量已预留，不需要回调式补偿日志。
             */
            if (state.Source.HasValue)
            {
                var source = state.Source.Value;
                capture.Source.RestoreState(in source);
            }
            if (binding != null)
            {
                binding.Data.LastTarget = state.LastTarget;
                binding.Data.FramesSinceRepath = state.FramesSinceRepath;
                binding.Data.Waypoints = mirror;
            }
            if (debug != null) _debug.SetPaths(debug);
        }

        /// <summary>
        /// 按原调试时机投影路径镜像，不参与模拟状态或恢复准入。
        /// </summary>
        internal void ProjectDebugState(NavigationDebugState debug)
        {
            if (debug == null || _bindings.Count == 0) return;
            debug.SetPaths(BuildDebugPaths(0, null, Vec3.Zero));
        }

        /// <summary>
        /// 准备所有调试条目，可用已准备的恢复镜像替换一个角色，不修改当前绑定。
        /// </summary>
        private List<ActivePathEntry> BuildDebugPaths(int actorId, Vec3[] mirror, Vec3 target)
        {
            var entries = new List<ActivePathEntry>(_bindings.Count);
            foreach (var pair in _bindings)
            {
                var points = pair.Key == actorId ? mirror : pair.Value.Data.Waypoints;
                if (points == null || points.Length == 0) continue;
                var lastTarget = pair.Key == actorId ? target : pair.Value.Data.LastTarget;
                entries.Add(new ActivePathEntry(pair.Key, points, in lastTarget, Vec3.Zero));
            }
            return entries;
        }

        /// <summary>
        /// 只读定位已注册的同代初始化角色，不通过当前编号补认旧实体。
        /// </summary>
        private global::ActorEntity RequireActor(int actorId, int version)
        {
            if (!_actors.TryGet(actorId, out var actor) || !actor.hasActorId || actor.actorId.Value != actorId ||
                !_actors.MatchesEntityVersion(actorId, version) || !actor.hasMotion || !actor.motion.Initialized ||
                actor.motion.Pipeline == null)
                throw new InvalidOperationException($"Path actor {actorId} requires its same surviving initialized Pipeline and entity version.");
            return actor;
        }

        /// <summary>
        /// 验证原绑定归属，不尝试修复失效依赖。
        /// </summary>
        private static void RequireBinding(Binding binding, global::ActorEntity actor, int version, MotionPipeline pipeline = null)
        {
            if (!ReferenceEquals(binding.Actor, actor) || binding.Version != version ||
                !ReferenceEquals(binding.Pipeline, pipeline ?? actor.motion.Pipeline))
                throw new InvalidOperationException("Path binding belongs to a stale actor generation or Pipeline.");
        }

        /// <summary>
        /// 检查规划坐标是否为有限浮点数。
        /// </summary>
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// 世界退出时解绑并归还自身来源，不触及其它业务来源。
        /// </summary>
        public void Dispose()
        {
            foreach (var binding in _bindings.Values) Detach(binding);
            _bindings.Clear();
            _seen.Clear();
            _removed.Clear();
        }
    }
}
