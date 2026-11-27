using System;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Combat.MotionSystem.Generic
{
    /// <summary>
    /// 持有一份完整路径状态，通过无状态推进器向运动管线贡献位移。
    /// </summary>
    /// <remarks>
    /// 本类型不管理帧历史、管线成员或导航依赖。实例及外部状态数组不支持并发修改。
    /// 完整状态捕获和恢复复制路径数组；普通 Tick 不分配路径或状态对象。
    /// </remarks>
    public sealed class PathFollowerMotionSource : IMotionSource, IMotionFinishEventSource, IMotionSnapshotSource
    {
        private static readonly ObjectPool<PathFollowerMotionSource> Pool = Pools.GetPool(
            createFunc: () => new PathFollowerMotionSource(),
            onRelease: s => s.Reset(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private PathFollowerState _state;
        private float _nextTickSpeedLimit = float.PositiveInfinity;

        /// <summary>
        /// 限制下一次推进的速度，不改变规划速度和快照。每帧驱动者应在 Tick 前刷新；
        /// 若本帧来源被抑制，可在下一帧用新值覆盖。正无穷表示不限制。
        /// </summary>
        public void LimitNextTickSpeed(float speed)
        {
            if (float.IsNaN(speed) || speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            _nextTickSpeedLimit = speed;
        }

        private PathFollowerMotionSource()
        {
            Reset();
        }

        /// <summary>
        /// 创建来源并复制路径；空引用作为空路径，非正速度和阈值沿用普通配置的默认修正。
        /// </summary>
        public PathFollowerMotionSource(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            Configure(points, speed, arriveEpsilon, priority, groupId, stacking);
        }

        /// <summary>
        /// 从对象池取得来源并完整重新配置；所有者须先从管线解绑，再归还一次。
        /// </summary>
        public static PathFollowerMotionSource Rent(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            var source = Pool.Get();
            source.Configure(points, speed, arriveEpsilon, priority, groupId, stacking);
            return source;
        }

        /// <summary>
        /// 将已解绑来源重置并归池；空引用无操作，不检查重复归还。
        /// </summary>
        public static void Release(PathFollowerMotionSource source)
        {
            if (source == null) return;
            Pool.Release(source);
        }

        /// <summary>
        /// 从纯值恢复一个尚未挂接管线的来源，不执行正常配置或激活。
        /// 恢复失败时归还本次租用的骨架。
        /// </summary>
        public static PathFollowerMotionSource RentForRestore(in PathFollowerState state)
        {
            var source = Pool.Get();
            try
            {
                source.RestoreState(in state);
                return source;
            }
            catch
            {
                Pool.Release(source);
                throw;
            }
        }

        /// <summary>
        /// 复制并替换路径，从首点重新激活；非正速度设为 0.0001，非正阈值设为 0.01。
        /// </summary>
        /// <remarks>
        /// 保留普通配置对特殊数值的既有行为；仅显式完整状态验证和恢复采用严格准入。
        /// </remarks>
        public void Configure(Vec3[] points, float speed, float arriveEpsilon = 0.05f, int priority = 10, int groupId = MotionGroups.Path, MotionStacking stacking = MotionStacking.ExclusiveHighestPriority)
        {
            var ownedPoints = CopyPoints(points);
            _nextTickSpeedLimit = float.PositiveInfinity;
            _state = new PathFollowerState
            {
                Points = ownedPoints,
                Speed = speed <= 0f ? 0.0001f : speed,
                ArriveEpsilon = arriveEpsilon <= 0f ? 0.01f : arriveEpsilon,
                Priority = priority,
                GroupId = groupId,
                Stacking = stacking,
                Index = 0,
                IsActive = ownedPoints.Length > 0,
            };
        }

        /// <summary>
        /// 清除路径和活动状态，恢复 Path 组、优先级 10 与阈值 0.05 的默认状态。
        /// </summary>
        public void Reset()
        {
            _nextTickSpeedLimit = float.PositiveInfinity;
            _state = new PathFollowerState
            {
                Points = Array.Empty<Vec3>(),
                Speed = 0.0001f,
                ArriveEpsilon = 0.05f,
                GroupId = MotionGroups.Path,
                Stacking = MotionStacking.ExclusiveHighestPriority,
                Priority = 10,
                Index = 0,
                IsActive = false,
            };
        }

        /// <summary>
        /// 当前来源组编号。
        /// </summary>
        public int GroupId => _state.GroupId;

        /// <summary>
        /// 当前管线贡献叠加方式。
        /// </summary>
        public MotionStacking Stacking => _state.Stacking;

        /// <summary>
        /// 正常推进结束时由管线发出到达通知。
        /// </summary>
        public MotionFinishEvent FinishEvent => MotionFinishEvent.Arrive;

        /// <summary>
        /// 当前管线仲裁优先级。
        /// </summary>
        public int Priority => _state.Priority;

        /// <summary>
        /// 来源是否仍参与推进。
        /// </summary>
        public bool IsActive => _state.IsActive;

        /// <summary>
        /// 来源是否已不再活动，包含正常结束与取消。
        /// </summary>
        public bool IsFinished => !_state.IsActive;

        /// <summary>
        /// 下一个待处理的路径点索引，允许等于路径长度。
        /// </summary>
        public int CurrentIndex => _state.Index;

        /// <summary>
        /// 使用显式状态推进，累加期望位移并更新朝向，不直接写入位置或发送通知。
        /// </summary>
        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            var speed = Math.Min(_state.Speed, _nextTickSpeedLimit);
            _nextTickSpeedLimit = float.PositiveInfinity;
            PathFollowerStepper.Tick(ref _state, ref state, dt, speed, ref outDesiredDelta);
        }

        /// <summary>
        /// 仅撤销活动标志，保留路径、游标和冻结参数。
        /// </summary>
        public void Cancel()
        {
            _state.IsActive = false;
        }

        /// <summary>
        /// 返回含完整路径的独立数据副本，不推进或规范化特殊数值。
        /// </summary>
        public PathFollowerState CaptureState()
        {
            var captured = _state;
            captured.Points = CopyPoints(_state.Points);
            return captured;
        }

        /// <summary>
        /// 只读验证完整状态；非法字段抛出参数异常，不执行配置、推进或恢复。
        /// </summary>
        /// <exception cref="ArgumentException">
        /// 路径、数值、叠加方式、索引或活动组合不符合完整恢复合同。
        /// </exception>
        public static void ValidateState(in PathFollowerState state)
        {
            if (state.Points == null)
                throw new ArgumentException("PathFollowerState.Points must not be null.", nameof(state));
            for (var i = 0; i < state.Points.Length; i++)
            {
                var point = state.Points[i];
                if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
                    throw new ArgumentException($"PathFollowerState.Points[{i}] must have finite coordinates.", nameof(state));
            }
            if (!IsFinite(state.Speed) || state.Speed <= 0f)
                throw new ArgumentException("PathFollowerState.Speed must be finite and positive.", nameof(state));
            if (!IsFinite(state.ArriveEpsilon) || state.ArriveEpsilon <= 0f)
                throw new ArgumentException("PathFollowerState.ArriveEpsilon must be finite and positive.", nameof(state));
            if (state.Stacking != MotionStacking.Additive &&
                state.Stacking != MotionStacking.ExclusiveHighestPriority &&
                state.Stacking != MotionStacking.OverrideLowerPriority)
                throw new ArgumentException("PathFollowerState.Stacking is undefined.", nameof(state));
            if (state.Index < 0 || state.Index > state.Points.Length)
                throw new ArgumentException("PathFollowerState.Index must be within the complete path.", nameof(state));
            if (state.Points.Length == 0 && state.IsActive)
                throw new ArgumentException("PathFollowerState.IsActive must be false for an empty path.", nameof(state));
        }

        /// <summary>
        /// 验证并复制完整状态后一次提交；准备失败保留原状态，成功不推进或发出通知。
        /// </summary>
        /// <remarks>
        /// 不增删管线成员或重排来源。调用方负责成员身份、管线次序及其它所有者的数据一致性。
        /// 调用期间不得并发修改输入数组；返回后调用方可独立修改输入数据。
        /// </remarks>
        public void RestoreState(in PathFollowerState state)
        {
            ValidateState(in state);
            var restored = state;
            restored.Points = CopyPoints(state.Points);
            _state = restored;
            _nextTickSpeedLimit = float.PositiveInfinity;
        }

        /// <summary>
        /// 导出依赖当前实例原路径的兼容局部快照，不包含路径点。
        /// </summary>
        public bool ExportSnapshot(out MotionSourceSnapshot snapshot)
        {
            snapshot = new MotionSourceSnapshot
            {
                GroupId = _state.GroupId,
                Priority = _state.Priority,
                Stacking = _state.Stacking,
                IsActive = _state.IsActive,
                Index = _state.Index,
                Float0 = _state.Speed,
                Float1 = _state.ArriveEpsilon,
            };
            return true;
        }

        /// <summary>
        /// 向当前原路径导入兼容局部快照，夹取索引并保留非正数值对应的旧参数。
        /// </summary>
        /// <remarks>
        /// 此入口保留原有宽松规则，不调用完整状态验证，也不取得独立路径恢复资格。
        /// </remarks>
        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _nextTickSpeedLimit = float.PositiveInfinity;
            _state.GroupId = snapshot.GroupId;
            _state.Priority = snapshot.Priority;
            _state.Stacking = snapshot.Stacking;
            _state.IsActive = snapshot.IsActive && _state.Points.Length > 0;
            _state.Index = snapshot.Index;
            if (_state.Index < 0) _state.Index = 0;
            if (_state.Index > _state.Points.Length) _state.Index = _state.Points.Length;
            _state.Speed = snapshot.Float0 <= 0f ? _state.Speed : snapshot.Float0;
            _state.ArriveEpsilon = snapshot.Float1 <= 0f ? _state.ArriveEpsilon : snapshot.Float1;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static Vec3[] CopyPoints(Vec3[] points)
        {
            if (points == null || points.Length == 0) return Array.Empty<Vec3>();
            var copy = new Vec3[points.Length];
            Array.Copy(points, copy, points.Length);
            return copy;
        }
    }
}
