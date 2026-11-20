using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Continuous;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Rollback
{
    internal sealed class MobaManagedMotionCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public string SourceType;
        [JsonProperty(Required = Required.Always)] public int ModifierSourceId;
        [JsonProperty(Required = Required.AllowNull)] public MobaFixedDeltaCheckpoint FixedDelta;

        internal static MobaManagedMotionCheckpoint Capture(MobaMotionContinuousRuntime runtime) =>
            new MobaManagedMotionCheckpoint
            {
                SourceType = runtime.LocalMotionSource?.GetType().FullName ?? "null",
                ModifierSourceId = runtime.ModifierSourceId,
                FixedDelta = runtime.LocalMotionSource is FixedDeltaMotionSource source
                    ? MobaFixedDeltaCheckpoint.From(source.CaptureExactState()) : null,
            };

        internal void Validate(MobaContinuousRuntimeBase runtime)
        {
            if (!(runtime is MobaMotionContinuousRuntime motion))
                throw new InvalidOperationException("Motion checkpoint belongs to a non-Motion Continuous.");
            motion.ValidateLocalMotionBinding();
            if (ModifierSourceId <= 0 || ModifierSourceId != motion.ModifierSourceId)
                throw new InvalidOperationException("Managed Motion checkpoint does not identify the same surviving Continuous instance.");
            if (SourceType != typeof(FixedDeltaMotionSource).FullName || FixedDelta == null)
                throw new InvalidOperationException($"Unsupported or missing managed Motion checkpoint: {SourceType}.");
            var state = FixedDelta.ToState();
            if (!state.Active) throw new InvalidOperationException("Managed Motion target source is not active.");
            ((FixedDeltaMotionSource)motion.LocalMotionSource).ValidateExactState(in state);
        }

        internal void Restore(MobaContinuousRuntimeBase runtime)
        {
            Validate(runtime);
            var state = FixedDelta.ToState();
            ((FixedDeltaMotionSource)((MobaMotionContinuousRuntime)runtime).LocalMotionSource).RestoreExactState(in state);
        }
    }

    /// <summary>
    /// 区分已批准的来源身份空间；未知来源不能参与精确恢复。
    /// </summary>
    internal enum MobaMotionSourceKind
    {
        /// <summary>
        /// 尚无恢复合同的来源。
        /// </summary>
        Unknown,
        /// <summary>
        /// 由存活 Continuous 持有的 FixedDelta 来源。
        /// </summary>
        ManagedFixedDelta,
        /// <summary>
        /// 由普通移动业务服务持有的来源。
        /// </summary>
        Locomotion,
        /// <summary>
        /// 由寻路服务独占的角色路径槽位。
        /// </summary>
        Path,
    }

    /// <summary>
    /// 用实体身份及领域来源键定位同会话内的存活来源，不序列化运行时引用。
    /// </summary>
    internal sealed class MobaMotionSourceIdentity
    {
        [JsonProperty(Required = Required.Always)] public int ActorId;
        [JsonProperty(Required = Required.Always)] public int EntityVersion;
        [JsonProperty(Required = Required.Always)] public MobaMotionSourceKind SourceKind;
        [JsonProperty(Required = Required.Always)] public int SourceId;
        internal (int, int, MobaMotionSourceKind, int) Key => (ActorId, EntityVersion, SourceKind, SourceId);
    }

    /// <summary>
    /// 保存每个受支持 Pipeline 的普通移动槽位存在性及有限值状态。
    /// </summary>
    internal sealed class MobaLocomotionCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public MobaMotionSourceIdentity Identity;
        /// <summary>
        /// 标明检查点是否占用该逻辑槽位；空槽仍必须显式列入完整名单。
        /// </summary>
        [JsonProperty(Required = Required.Always)] public bool Exists;
        [JsonProperty(Required = Required.Always)] public float X;
        [JsonProperty(Required = Required.Always)] public float Y;
        [JsonProperty(Required = Required.Always)] public float Z;
        [JsonProperty(Required = Required.Always)] public float Speed;
        [JsonProperty(Required = Required.Always)] public int Space;
        [JsonProperty(Required = Required.Always)] public int Priority;

        internal MotionSourceSnapshot ToSnapshot()
        {
            if (Identity == null || Identity.SourceKind != MobaMotionSourceKind.Locomotion || Identity.SourceId != 0 ||
                Identity.ActorId <= 0 || Identity.EntityVersion <= 0 ||
                !Finite(X) || !Finite(Y) || !Finite(Z) || !Finite(Speed) || Y != 0 ||
                Space != (int)MotionInputSpace.World && Space != (int)MotionInputSpace.Local ||
                !Exists && (X != 0 || Z != 0 || Speed != 0 || Space != 0 || Priority != 0))
                throw new InvalidOperationException("Invalid Locomotion identity or finite XZ source state.");
            return new MotionSourceSnapshot
            {
                GroupId = MotionGroups.Locomotion, Stacking = MotionStacking.Additive, IsActive = true,
                Vector0 = new Vec3(X, Y, Z), Float0 = Speed, Index = Space, Priority = Priority,
            };
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 保存一个 Actor 的唯一完整来源次序，以及独立于成员关系的路径槽位值。
    /// </summary>
    internal sealed class MobaMotionPipelineCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public int ActorId;
        [JsonProperty(Required = Required.Always)] public int EntityVersion;
        [JsonProperty(Required = Required.Always)] public MobaMotionSourceIdentity[] Sources;
        [JsonProperty(Required = Required.Always)] public MobaPathSlotCheckpoint Path;
    }

    /// <summary>
    /// 仅编码纯值。来源是否为成员由同一 Actor 的 Sources 决定，不再存第二份成员标志。
    /// </summary>
    internal sealed class MobaPathSlotCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public bool BindingExists;
        [JsonProperty(Required = Required.Always)] public float[] LastTarget;
        [JsonProperty(Required = Required.Always)] public int FramesSinceRepath;
        [JsonProperty(Required = Required.AllowNull)] public MobaPathSourceCheckpoint Source;

        internal static MobaPathSlotCheckpoint From(in MobaPathFollowingState state) => new MobaPathSlotCheckpoint
        {
            BindingExists = state.BindingExists,
            LastTarget = Pack(state.LastTarget), FramesSinceRepath = state.FramesSinceRepath,
            Source = state.Source.HasValue ? MobaPathSourceCheckpoint.From(state.Source.Value) : null,
        };

        internal MobaPathFollowingState ToState(int actorId, int version, bool member)
        {
            var state = new MobaPathFollowingState
            {
                ActorId = actorId, EntityVersion = version, BindingExists = BindingExists,
                LastTarget = Unpack(LastTarget), FramesSinceRepath = FramesSinceRepath,
                SourceExists = Source != null, IsPipelineMember = member, Source = Source?.ToState(),
            };
            MobaPathFollowingRuntimeService.ValidateValues(in state);
            return state;
        }

        internal static float[] Pack(Vec3 value) => new[] { value.X, value.Y, value.Z };
        internal static Vec3 Unpack(float[] value) => value == null || value.Length != 3
            ? throw new InvalidOperationException("Path position must have exactly three coordinates.")
            : new Vec3(value[0], value[1], value[2]);
    }

    /// <summary>
    /// 编码来源的推进参数与完整路径副本，不序列化来源引用或调试投影。
    /// </summary>
    internal sealed class MobaPathSourceCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public float[][] Points;
        [JsonProperty(Required = Required.Always)] public float Speed;
        [JsonProperty(Required = Required.Always)] public float ArriveEpsilon;
        [JsonProperty(Required = Required.Always)] public int Priority;
        [JsonProperty(Required = Required.Always)] public int GroupId;
        [JsonProperty(Required = Required.Always)] public MotionStacking Stacking;
        [JsonProperty(Required = Required.Always)] public int Index;
        [JsonProperty(Required = Required.Always)] public bool IsActive;

        internal static MobaPathSourceCheckpoint From(PathFollowerState state) => new MobaPathSourceCheckpoint
        {
            Points = Array.ConvertAll(state.Points, MobaPathSlotCheckpoint.Pack),
            Speed = state.Speed, ArriveEpsilon = state.ArriveEpsilon, Priority = state.Priority,
            GroupId = state.GroupId, Stacking = state.Stacking, Index = state.Index, IsActive = state.IsActive,
        };

        internal PathFollowerState ToState() => new PathFollowerState
        {
            Points = Array.ConvertAll(Points, MobaPathSlotCheckpoint.Unpack),
            Speed = Speed, ArriveEpsilon = ArriveEpsilon, Priority = Priority,
            GroupId = GroupId, Stacking = Stacking, Index = Index, IsActive = IsActive,
        };
    }

    public sealed partial class MobaSkillLifecycleRollbackPreparation
    {
        /// <summary>
        /// 按对象引用核对来源所有权，不让自定义相等实现合并两个运行时实例。
        /// </summary>
        private sealed class MotionSourceReferences : IEqualityComparer<IMotionSource>
        {
            internal static readonly MotionSourceReferences Instance = new MotionSourceReferences();
            /// <summary>
            /// 比较来源是否为同一运行时实例。
            /// </summary>
            public bool Equals(IMotionSource x, IMotionSource y) => ReferenceEquals(x, y);
            /// <summary>
            /// 获取仅供进程内引用索引使用的哈希，不写入快照。
            /// </summary>
            public int GetHashCode(IMotionSource obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private MobaMotionSourceIdentity MotionIdentity(int actorId, MobaMotionSourceKind kind, int id) =>
            new MobaMotionSourceIdentity
            { ActorId = actorId, EntityVersion = _actors.GetEntityVersion(actorId), SourceKind = kind, SourceId = id };

        private void ValidateCurrentMotionSourceCoverage()
        {
            BuildMotionPipelineRoster(capture: false);
        }

        private Dictionary<int, Dictionary<IMotionSource, MobaMotionSourceIdentity>> IndexMotionSources(IReadOnlyList<IContinuous> instances)
        {
            var result = new Dictionary<int, Dictionary<IMotionSource, MobaMotionSourceIdentity>>();
            foreach (var instance in instances)
            {
                if (!(instance is MobaMotionContinuousRuntime motion)) continue;
                if (!result.TryGetValue(motion.OwnerActorId, out var sources))
                    result.Add(motion.OwnerActorId, sources = new Dictionary<IMotionSource, MobaMotionSourceIdentity>(MotionSourceReferences.Instance));
                if (motion.LocalMotionSource == null || !sources.TryAdd(motion.LocalMotionSource,
                    MotionIdentity(motion.OwnerActorId, MobaMotionSourceKind.ManagedFixedDelta, motion.ModifierSourceId)))
                    throw new InvalidOperationException("Motion source ownership is absent or duplicated.");
            }
            return result;
        }

        private MobaMotionPipelineCheckpoint[] CaptureMotionPipelineOrders() =>
            BuildMotionPipelineRoster(capture: true);

        /*
         * 清点每个管线，不把来源是否活跃当作所有权。普通注册表保留已有的
         * 受管 Motion 诊断导出，预测注册表和恢复预检不接纳未知来源。
         */
        private MobaMotionPipelineCheckpoint[] BuildMotionPipelineRoster(bool capture)
        {
            var managed = IndexMotionSources(_continuous.GetAllContinuous());
            var rows = new List<MobaMotionPipelineCheckpoint>();
            var pipelines = new HashSet<MotionPipeline>();
            var seenPath = new HashSet<int>();
            foreach (var actorId in _actors.CopyActorIdsInOrder())
            {
                if (!_actors.TryGet(actorId, out var actor) || !actor.hasMotion || actor.motion.Pipeline == null) continue;
                var pipeline = actor.motion.Pipeline;
                if (!pipelines.Add(pipeline)) throw new InvalidOperationException("Multiple actors share one Motion Pipeline.");
                var version = _actors.GetEntityVersion(actorId);
                var path = new MobaPathFollowingState { ActorId = actorId, EntityVersion = version };
                PathFollowerMotionSource pathSource = null;
                if (_path != null && _path.TryGet(actorId, out _))
                {
                    if (!_restorePathSlots) throw new InvalidOperationException($"Motion rollback actor {actorId} has Path outside declared recovery ownership (prediction navigation is required).");
                    var pathCapture = _path.CaptureState(actorId, false);
                    path = pathCapture.State;
                    pathSource = pathCapture.Source;
                    seenPath.Add(actorId);
                }
                LocomotionMotionSource loco = null;
                _locomotion?.TryGetBinding(actorId, actor, version, pipeline, out loco);
                managed.TryGetValue(actorId, out var owners);
                var order = pipeline.CaptureSourceOrder();
                var diagnosticOnly = capture && !_restorePathSlots && owners != null;
                if (!diagnosticOnly)
                {
                    try { pipeline.ValidateSourceOrder(order); }
                    catch (InvalidOperationException ex)
                    {
                        var types = Array.ConvertAll(order, source => source?.GetType().FullName ?? "null");
                        throw new InvalidOperationException(
                            $"Motion rollback actor {actorId} has sources outside declared recovery ownership: [{string.Join(", ", types)}]. {ex.Message}", ex);
                    }
                }
                var identities = capture ? new MobaMotionSourceIdentity[order.Length] : null;
                var managedCount = 0;
                for (var index = 0; index < order.Length; index++)
                {
                    var source = order[index];
                    MobaMotionSourceIdentity identity;
                    if (owners != null && owners.TryGetValue(source, out identity)) managedCount++;
                    else if (source != null && ReferenceEquals(source, loco)) identity = MotionIdentity(actorId, MobaMotionSourceKind.Locomotion, 0);
                    else if (source != null && ReferenceEquals(source, pathSource)) identity = MotionIdentity(actorId, MobaMotionSourceKind.Path, 0);
                    else if (diagnosticOnly)
                        identity = MotionIdentity(actorId, MobaMotionSourceKind.Unknown, 0);
                    else throw new InvalidOperationException($"Motion rollback actor {actorId} has sources outside declared recovery ownership: [{source.GetType().FullName}].");
                    if (capture) identities[index] = identity;
                }
                if (!diagnosticOnly && owners != null && managedCount != owners.Count)
                    throw new InvalidOperationException("Managed Motion owner is missing from its Pipeline.");
                managed.Remove(actorId);
                if (capture) rows.Add(new MobaMotionPipelineCheckpoint
                { ActorId = actorId, EntityVersion = version, Sources = identities, Path = MobaPathSlotCheckpoint.From(in path) });
            }
            if (managed.Count != 0) throw new InvalidOperationException("Managed Motion owner has no surviving Pipeline.");
            if (_path != null)
                foreach (var actorId in _path.BoundActorIds)
                    if (!seenPath.Contains(actorId))
                        throw new InvalidOperationException($"Path binding {actorId} has no surviving actor Pipeline.");
            return rows.ToArray();
        }

        private MobaLocomotionCheckpoint[] CaptureLocomotionLocal()
        {
            var result = new List<MobaLocomotionCheckpoint>();
            foreach (var pair in _actors.Entries)
            {
                var actor = pair.Value;
                if (actor == null || !actor.hasMotion || actor.motion.Pipeline == null) continue;
                if (_locomotion == null) throw new InvalidOperationException("Locomotion rollback ownership service is missing.");
                var version = _actors.GetEntityVersion(pair.Key);
                _locomotion.RememberPipelineForRollback(actor, version, actor.motion.Pipeline);
                _locomotion.TryGetBinding(pair.Key, actor, version, actor.motion.Pipeline, out var loco);
                result.Add(new MobaLocomotionCheckpoint
                {
                    Identity = MotionIdentity(pair.Key, MobaMotionSourceKind.Locomotion, 0), Exists = loco != null,
                    X = loco?.Input.X ?? 0, Y = loco?.Input.Y ?? 0, Z = loco?.Input.Z ?? 0,
                    Speed = loco?.Speed ?? 0, Space = loco == null ? 0 : (int)loco.Space, Priority = loco?.Priority ?? 0,
                });
            }
            result.Sort((a, b) => a.Identity.ActorId.CompareTo(b.Identity.ActorId));
            return result.ToArray();
        }

        /// <summary>
        /// 持有通过预检的既有引用和目标值，仅用于本次静默恢复及局部补偿。
        /// </summary>
        private sealed class PreparedMotionPipeline
        {
            internal int ActorId, Version;
            internal global::ActorEntity Actor;
            internal MotionPipeline Pipeline;
            internal IMotionSource[] Sources;
            internal LocomotionMotionSource Locomotion;
            internal int LocomotionIndex = -1;
            internal MotionSourceSnapshot LocomotionState;
            internal int PathIndex = -1;
            internal MobaPathFollowingRuntimeService.LocalCapture LivePath;
            internal MobaPathFollowingState PathState;
            internal PathFollowerMotionSource RentedPath;
        }

        private List<PreparedMotionPipeline> PrepareMotionPipelineOrders(
            MobaContinuousLocalCheckpoint[] order, IReadOnlyList<IContinuous> instances, MobaLocomotionCheckpoint[] local,
            MobaMotionPipelineCheckpoint[] roster, int[] actorIds, int? restoreFrame = null)
        {
            var locals = new Dictionary<int, MobaLocomotionCheckpoint>();
            var previousActor = 0;
            foreach (var entry in local)
            {
                if (entry == null) throw new InvalidOperationException("Missing Locomotion entry.");
                entry.ToSnapshot();
                if (entry.Identity.ActorId <= previousActor || !locals.TryAdd(entry.Identity.ActorId, entry))
                    throw new InvalidOperationException("Locomotion slots must be unique and sorted by actor identity.");
                previousActor = entry.Identity.ActorId;
            }
            var savedOrders = new Dictionary<int, MobaMotionPipelineCheckpoint>();
            previousActor = 0;
            foreach (var saved in roster)
            {
                if (saved == null || saved.ActorId <= previousActor || saved.EntityVersion <= 0 || saved.Sources == null || saved.Path == null)
                    throw new InvalidOperationException("Motion Pipeline rows must be complete, unique and sorted by actor identity.");
                savedOrders.Add(saved.ActorId, saved);
                previousActor = saved.ActorId;
            }
            var index = Index(instances);
            var priorities = new Dictionary<IMotionSource, int>(MotionSourceReferences.Instance);
            foreach (var entry in order)
            {
                if (entry.Motion == null) continue;
                if (!index.TryGetValue(entry.Key, out var instance) || !(instance is MobaMotionContinuousRuntime motion))
                    throw new InvalidOperationException("Motion Pipeline checkpoint has no surviving owner.");
                entry.Motion.Validate(motion);
                if (!priorities.TryAdd(motion.LocalMotionSource, entry.Motion.FixedDelta.Priority))
                    throw new InvalidOperationException("Duplicate managed Motion source.");
            }
            var byActor = IndexMotionSources(instances);
            if (_restorePathSlots)
            {
                var targetIds = new HashSet<int>(actorIds);
                foreach (var id in _path.BoundActorIds)
                    if (!targetIds.Contains(id)) throw new InvalidOperationException("Path requires a surviving target actor Pipeline.");
            }
            var result = new List<PreparedMotionPipeline>();
            var pipelines = new HashSet<MotionPipeline>();
            foreach (var actorId in actorIds)
            {
                /*
                 * Projectile/Launcher 的结构投影继续由原合同处理；有管线的目标必须同代存活。
                 */
                _actors.TryGet(actorId, out var actor);
                var componentOwner = actor;
                if (actor == null && restoreFrame.HasValue)
                    _actors.TryGetRetainedForRecovery(actorId, restoreFrame.Value, out componentOwner, out actor);
                var pipeline = componentOwner != null && componentOwner.hasMotion ? componentOwner.motion.Pipeline : null;
                if (pipeline == null)
                {
                    if (locals.ContainsKey(actorId) || savedOrders.ContainsKey(actorId) || byActor.ContainsKey(actorId))
                        throw new InvalidOperationException("Motion target requires a surviving actor Pipeline.");
                    continue;
                }
                if (!locals.TryGetValue(actorId, out var slot) || !savedOrders.TryGetValue(actorId, out var saved) ||
                    _locomotion == null || saved.EntityVersion != slot.Identity.EntityVersion ||
                    saved.EntityVersion != _actors.GetEntityVersion(actorId) || !pipelines.Add(pipeline))
                    throw new InvalidOperationException("Motion target is missing a slot or has a different actor/Pipeline identity.");
                locals.Remove(actorId);
                savedOrders.Remove(actorId);
                var owned = _locomotion.ValidateSlotForRollback(actorId, actor, saved.EntityVersion, pipeline, componentOwner);
                var livePath = _restorePathSlots ? _path.CaptureState(actorId, false, restoreFrame) : null;
                var byId = new Dictionary<(int, int, MobaMotionSourceKind, int), IMotionSource>();
                var current = new List<IMotionSource>();
                if (byActor.TryGetValue(actorId, out var sources))
                    foreach (var pair in sources)
                    {
                        if (!byId.TryAdd(pair.Value.Key, pair.Key)) throw new InvalidOperationException("Duplicate Motion identity.");
                        current.Add(pair.Key);
                    }
                byActor.Remove(actorId);
                if (owned != null) current.Add(owned);
                if (livePath != null && livePath.State.IsPipelineMember) current.Add(livePath.Source);
                pipeline.ValidateSourceOrder(current);
                var prepared = new PreparedMotionPipeline
                {
                    ActorId = actorId, Version = saved.EntityVersion, Actor = actor,
                    Pipeline = pipeline, Sources = new IMotionSource[saved.Sources.Length],
                    Locomotion = owned, LocomotionState = slot.ToSnapshot(), LivePath = livePath,
                };
                for (var position = 0; position < saved.Sources.Length; position++)
                {
                    var id = saved.Sources[position];
                    if (id == null || id.ActorId != saved.ActorId || id.EntityVersion != prepared.Version)
                        throw new InvalidOperationException("Motion Pipeline order contains a mismatched identity.");
                    switch (id.SourceKind)
                    {
                        case MobaMotionSourceKind.Locomotion when id.SourceId == 0 && slot.Exists && prepared.LocomotionIndex < 0:
                            prepared.LocomotionIndex = position;
                            break;
                        case MobaMotionSourceKind.Path when id.SourceId == 0 && _restorePathSlots && prepared.PathIndex < 0:
                            prepared.PathIndex = position;
                            break;
                        case MobaMotionSourceKind.ManagedFixedDelta when id.SourceId > 0 && byId.TryGetValue(id.Key, out var source):
                            byId.Remove(id.Key);
                            prepared.Sources[position] = source;
                            break;
                        default:
                            throw new InvalidOperationException("Motion Pipeline order contains an unknown, duplicate, or absent source identity.");
                    }
                }
                prepared.PathState = saved.Path.ToState(actorId, saved.EntityVersion, prepared.PathIndex >= 0);
                if (!_restorePathSlots && prepared.PathState.BindingExists)
                    throw new InvalidOperationException("Path rollback requires frozen prediction navigation.");
                var previousPriority = int.MaxValue;
                for (var position = 0; position < prepared.Sources.Length; position++)
                {
                    var priority = position == prepared.LocomotionIndex ? prepared.LocomotionState.Priority :
                        position == prepared.PathIndex ? prepared.PathState.Source.Value.Priority : priorities[prepared.Sources[position]];
                    if (previousPriority < priority) throw new InvalidOperationException("Invalid target Motion priority order.");
                    previousPriority = priority;
                }
                if (byId.Count != 0 || slot.Exists != (prepared.LocomotionIndex >= 0))
                    throw new InvalidOperationException("Motion target order does not cover the complete source membership.");
                result.Add(prepared);
            }
            if (byActor.Count != 0 || locals.Count != 0 || savedOrders.Count != 0)
                throw new InvalidOperationException("Motion Pipeline or Locomotion target membership is incomplete.");
            return result;
        }

        private void RestoreMotionPipelines(List<PreparedMotionPipeline> pipelines, bool compensate)
        {
            foreach (var pipeline in pipelines)
            {
                var source = _locomotion.RestoreSlotForRollback(pipeline.ActorId, pipeline.Actor, pipeline.Version,
                    pipeline.Pipeline, pipeline.Locomotion, pipeline.LocomotionIndex >= 0, pipeline.LocomotionState);
                if (pipeline.LocomotionIndex >= 0) pipeline.Sources[pipeline.LocomotionIndex] = source;
                var path = pipeline.LivePath;
                if (path != null)
                {
                    /*
                     * 来源存在性不同才建立骨架。旧绑定与租用保持原样，成功前不归池；
                     * 补偿使用捕获时的原引用，不能把新对象伪装成旧对象。
                     */
                    var binding = compensate || path.State.SourceExists == pipeline.PathState.SourceExists ? path.Binding : null;
                    _path.RestoreWorldSlot(path, in pipeline.PathState, binding, ref pipeline.RentedPath);
                    if (pipeline.PathIndex >= 0)
                    {
                        _path.TryGet(pipeline.ActorId, out var current);
                        pipeline.Sources[pipeline.PathIndex] = current.Source;
                    }
                }
                pipeline.Pipeline.RestoreSourceMembership(pipeline.Sources);
            }
            if (_restorePathSlots) _path.RefreshRollbackDebug();
        }

        private void ReleaseDetachedPathSources(List<PreparedMotionPipeline> pipelines, bool compensate)
        {
            foreach (var pipeline in pipelines)
            {
                var detached = compensate ? pipeline.RentedPath : pipeline.LivePath?.Source;
                if (detached == null || pipeline.Pipeline.ContainsSource(detached)) continue;
                _path.TryGet(pipeline.ActorId, out var binding);
                if (!ReferenceEquals(detached, binding?.Source)) PathFollowerMotionSource.Release(detached);
            }
        }
    }

    internal sealed class MobaFixedDeltaCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public long DurationRaw;
        [JsonProperty(Required = Required.Always)] public long TimeLeftRaw;
        [JsonProperty(Required = Required.Always)] public bool Active;
        [JsonProperty(Required = Required.Always)] public float X;
        [JsonProperty(Required = Required.Always)] public float Y;
        [JsonProperty(Required = Required.Always)] public float Z;
        [JsonProperty(Required = Required.Always)] public int GroupId;
        [JsonProperty(Required = Required.Always)] public int Priority;
        [JsonProperty(Required = Required.Always)] public int Stacking;
        [JsonProperty(Required = Required.Always)] public bool HasCollisionPolicy;
        [JsonProperty(Required = Required.Always)] public MobaMotionCollisionCheckpoint CollisionPolicy;
        [JsonProperty(Required = Required.Always)] public bool HasCompletionCollisionPolicy;
        [JsonProperty(Required = Required.Always)] public MobaMotionCollisionCheckpoint CompletionCollisionPolicy;

        internal static MobaFixedDeltaCheckpoint From(FixedDeltaMotionExactState state) => new MobaFixedDeltaCheckpoint
        {
            DurationRaw = state.DurationRaw, TimeLeftRaw = state.TimeLeftRaw, Active = state.Active,
            X = state.Velocity.X, Y = state.Velocity.Y, Z = state.Velocity.Z,
            GroupId = state.GroupId, Priority = state.Priority, Stacking = (int)state.Stacking,
            HasCollisionPolicy = state.HasCollisionPolicy, CollisionPolicy = MobaMotionCollisionCheckpoint.From(state.CollisionPolicy),
            HasCompletionCollisionPolicy = state.HasCompletionCollisionPolicy,
            CompletionCollisionPolicy = MobaMotionCollisionCheckpoint.From(state.CompletionCollisionPolicy),
        };

        internal FixedDeltaMotionExactState ToState() => new FixedDeltaMotionExactState(
            DurationRaw, TimeLeftRaw, Active, new Vec3(X, Y, Z), GroupId, Priority, (MotionStacking)Stacking,
            HasCollisionPolicy, CollisionPolicy.ToValue(), HasCompletionCollisionPolicy, CompletionCollisionPolicy.ToValue());
    }

    internal sealed class MobaMotionCollisionCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public bool Enable;
        [JsonProperty(Required = Required.Always)] public bool AllowPassThrough;
        [JsonProperty(Required = Required.Always)] public int EndOverlapPolicy;
        [JsonProperty(Required = Required.Always)] public float Radius;
        [JsonProperty(Required = Required.Always)] public float Skin;
        [JsonProperty(Required = Required.Always)] public int ObstacleMask;
        [JsonProperty(Required = Required.Always)] public int IgnoreMask;
        [JsonProperty(Required = Required.Always)] public int SelectionKey;
        [JsonProperty(Required = Required.Always)] public bool SlideAlongWalls;
        [JsonProperty(Required = Required.Always)] public int MaxSlideIterations;
        [JsonProperty(Required = Required.Always)] public float WallSlideSpeedRecovery;

        internal static MobaMotionCollisionCheckpoint From(MotionCollisionConstraints value) => new MobaMotionCollisionCheckpoint
        {
            Enable = value.Enable, AllowPassThrough = value.AllowPassThrough, EndOverlapPolicy = (int)value.EndOverlapPolicy,
            Radius = value.Radius, Skin = value.Skin, ObstacleMask = value.ObstacleMask, IgnoreMask = value.IgnoreMask,
            SelectionKey = value.SelectionKey, SlideAlongWalls = value.SlideAlongWalls,
            MaxSlideIterations = value.MaxSlideIterations, WallSlideSpeedRecovery = value.WallSlideSpeedRecovery,
        };

        internal MotionCollisionConstraints ToValue()
        {
            if (!Enum.IsDefined(typeof(MotionEndOverlapPolicy), EndOverlapPolicy) ||
                !Finite(Radius) || Radius < 0 || !Finite(Skin) || Skin < 0 ||
                !Finite(WallSlideSpeedRecovery) || WallSlideSpeedRecovery < 0 || WallSlideSpeedRecovery > 1 ||
                MaxSlideIterations < 0 || SlideAlongWalls && MaxSlideIterations == 0)
                throw new InvalidOperationException("Invalid managed Motion collision constraints.");
            // Preserve default(struct), including zero iterations; the constructor normally clamps it.
            if (MaxSlideIterations == 0)
            {
                if (Enable || AllowPassThrough || EndOverlapPolicy != 0 || Radius != 0 || Skin != 0 ||
                    ObstacleMask != 0 || IgnoreMask != 0 || SelectionKey != 0 || SlideAlongWalls || WallSlideSpeedRecovery != 0)
                    throw new InvalidOperationException("Invalid default Motion collision constraints.");
                return default;
            }
            return new MotionCollisionConstraints(Enable, AllowPassThrough, (MotionEndOverlapPolicy)EndOverlapPolicy,
                Radius, Skin, ObstacleMask, IgnoreMask, SlideAlongWalls, MaxSlideIterations, WallSlideSpeedRecovery, SelectionKey);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
