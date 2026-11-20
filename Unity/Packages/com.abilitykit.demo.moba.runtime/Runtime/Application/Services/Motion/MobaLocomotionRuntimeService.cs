using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.MotionSystem.Core;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 持有普通移动来源及其实体、Pipeline 关联。仅由正式 System 驱动，
    /// 不依赖回滚启用；恢复仅重建已批准的普通移动槽位，不重新准入玩法。
    /// </summary>
    [WorldService(typeof(MobaLocomotionRuntimeService))]
    public sealed class MobaLocomotionRuntimeService : IService
    {
        /// <summary>
        /// 记录正常业务持有的来源引用及所属实体版本，不作为对象图写入快照。
        /// </summary>
        private sealed class Binding
        {
            internal global::ActorEntity Actor;
            internal int Version;
            internal MotionPipeline Pipeline;
            internal LocomotionMotionSource Source;
        }

        private readonly Dictionary<int, Binding> _bindings = new Dictionary<int, Binding>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _removed = new List<int>();

        /// <summary>
        /// 记录同代实体首次捕获时的 Pipeline 依赖，不保存帧历史或来源对象图。
        /// </summary>
        private sealed class PipelineDependency
        {
            internal int Version;
            internal MotionPipeline Pipeline;
        }

        /*
         * 仅在显式捕获时记录同代 Pipeline 依赖；弱键不延长退役实体的生命周期。
         * 同代替换 Pipeline 不覆盖旧锚点，因此不能冒充原检查点的运行依赖。
         */
        private ConditionalWeakTable<global::ActorEntity, PipelineDependency> _rollbackPipelines =
            new ConditionalWeakTable<global::ActorEntity, PipelineDependency>();

        internal void BeginFrame() => _seen.Clear();

        internal LocomotionMotionSource Visit(global::ActorEntity actor, int version)
        {
            var id = actor.actorId.Value;
            _seen.Add(id);
            if (!_bindings.TryGetValue(id, out var binding)) return null;
            if (ReferenceEquals(binding.Actor, actor) && binding.Version == version &&
                ReferenceEquals(binding.Pipeline, actor.motion.Pipeline) && binding.Pipeline.ContainsSource(binding.Source))
                return binding.Source;
            Detach(binding);
            _bindings.Remove(id);
            return null;
        }

        internal LocomotionMotionSource Create(global::ActorEntity actor, int version, float speed)
        {
            if (version <= 0 || _bindings.ContainsKey(actor.actorId.Value))
                throw new InvalidOperationException("Locomotion requires a unique registered actor identity.");
            /*
             * MoveInput 已经是世界 XZ 方向，不能随角色朝向再次旋转。
             * 正常创建仍只在允许移动时发生，回滚不调用此入口。
             */
            var source = new LocomotionMotionSource(speed, MotionInputSpace.World, 0);
            actor.motion.Pipeline.AddSource(source);
            _bindings.Add(actor.actorId.Value, new Binding
            { Actor = actor, Version = version, Pipeline = actor.motion.Pipeline, Source = source });
            return source;
        }

        internal bool TryGetBinding(int actorId, global::ActorEntity actor, int version,
            MotionPipeline pipeline, out LocomotionMotionSource source)
        {
            source = null;
            if (!_bindings.TryGetValue(actorId, out var binding) || !ReferenceEquals(binding.Actor, actor) ||
                binding.Version != version || !ReferenceEquals(binding.Pipeline, pipeline) ||
                !pipeline.ContainsSource(binding.Source)) return false;
            source = binding.Source;
            return true;
        }

        /// <summary>
        /// 在显式捕获入口记住稳定依赖；同代替换不覆盖，预检入口不调用此方法。
        /// </summary>
        internal void RememberPipelineForRollback(global::ActorEntity actor, int version, MotionPipeline pipeline)
        {
            if (_rollbackPipelines.TryGetValue(actor, out var previous) && previous.Version == version) return;
            _rollbackPipelines.Remove(actor);
            _rollbackPipelines.Add(actor, new PipelineDependency { Version = version, Pipeline = pipeline });
        }

        internal void ForgetPipelineForRollback(global::ActorEntity actor) => _rollbackPipelines.Remove(actor);

        /// <summary>
        /// 只读核对同代 Pipeline 及当前绑定，空槽返回 null；过期绑定必须明确拒绝。
        /// </summary>
        internal LocomotionMotionSource ValidateSlotForRollback(int actorId, global::ActorEntity actor,
            int version, MotionPipeline pipeline, global::ActorEntity componentOwner = null)
        {
            componentOwner ??= actor;
            if (version <= 0 || !componentOwner.hasMotion || !componentOwner.motion.Initialized ||
                !ReferenceEquals(componentOwner.motion.Pipeline, pipeline) ||
                !_rollbackPipelines.TryGetValue(actor, out var dependency) || dependency.Version != version ||
                !ReferenceEquals(dependency.Pipeline, pipeline))
                throw new InvalidOperationException("Locomotion requires the same surviving initialized actor Pipeline.");
            if (!_bindings.ContainsKey(actorId)) return null;
            if (!TryGetBinding(actorId, actor, version, pipeline, out var source))
                throw new InvalidOperationException("Locomotion binding differs from its surviving actor or Pipeline membership.");
            return source;
        }

        /// <summary>
        /// 提交已完整预检的槽位状态；缺失时构造纯骨架，补偿时接受原来源引用。
        /// 不调用正常创建、门禁、属性求值、Cancel 或任何玩法回调。
        /// </summary>
        internal LocomotionMotionSource RestoreSlotForRollback(int actorId, global::ActorEntity actor, int version,
            MotionPipeline pipeline, LocomotionMotionSource target, bool exists, MotionSourceSnapshot state)
        {
            var live = ValidateSlotForRollback(actorId, actor, version, pipeline);
            if (exists && target == null)
                target = live ?? new LocomotionMotionSource(state.Float0, (MotionInputSpace)state.Index, state.Priority);
            if (!exists) target = null;
            if (!ReferenceEquals(live, target))
            {
                if (live != null) pipeline.RemoveSource(live);
                _bindings.Remove(actorId);
                if (target != null)
                {
                    pipeline.AddSource(target);
                    _bindings.Add(actorId, new Binding { Actor = actor, Version = version, Pipeline = pipeline, Source = target });
                }
            }
            if (target != null) target.ImportSnapshot(state);
            _seen.Add(actorId);
            return target;
        }

        internal void EndFrame()
        {
            _removed.Clear();
            foreach (var pair in _bindings)
                if (!_seen.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (var id in _removed) Remove(id);
        }

        internal void Remove(int actorId)
        {
            if (!_bindings.TryGetValue(actorId, out var binding)) return;
            Detach(binding);
            _bindings.Remove(actorId);
        }

        private static void Detach(Binding binding)
        {
            /*
             * 已销毁 Pipeline 的列表为空；仍存活的旧 Pipeline 只撤销本服务自己的来源。
             */
            if (binding.Pipeline.SourceCount > 0) binding.Pipeline.RemoveSource(binding.Source);
        }

        /// <summary>
        /// 释放本世界的普通移动关联，不影响其他业务持有的来源。
        /// </summary>
        public void Dispose()
        {
            foreach (var binding in _bindings.Values) Detach(binding);
            _bindings.Clear();
            _seen.Clear();
            _removed.Clear();
            _rollbackPipelines = new ConditionalWeakTable<global::ActorEntity, PipelineDependency>();
        }
    }
}
