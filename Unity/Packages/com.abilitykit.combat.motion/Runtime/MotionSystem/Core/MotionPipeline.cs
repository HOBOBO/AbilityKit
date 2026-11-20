using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Events;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Core
{
    public sealed class MotionPipeline : IDisposable
    {
        private List<IMotionSource> _sources;
        private Dictionary<int, int> _bestIndexByGroup;
        private List<int> _suppressedGroups;
        private bool _disposed;

        // 主导源碰撞策略的 group 优先级（高→低）：硬控 > 主动技能 > 寻路 > 基础移动。
        // 固定数组序保证选取确定性（不依赖字典迭代序）。
        private static readonly int[] PolicyGroupPrecedence =
        {
            MotionGroups.Control,
            MotionGroups.Ability,
            MotionGroups.Path,
            MotionGroups.Locomotion,
            MotionGroups.PassiveDisplacement,
        };

        public MotionPipeline()
        {
            _sources = MotionPipelinePool.RentSourceList();
            _bestIndexByGroup = MotionPipelinePool.RentBestIndexDictionary();
            _suppressedGroups = MotionPipelinePool.RentIntList();
        }

        public IMotionSolver Solver { get; set; } = NoMotionSolver.Instance;
        public IMotionEventSink Events { get; set; }

        public MotionPipelinePolicy Policy { get; set; }

        public int SourceCount => _sources?.Count ?? 0;

        /// <summary>Reference membership only; does not activate, reorder or replace a source.</summary>
        public bool ContainsSource(IMotionSource source)
        {
            EnsureNotDisposed();
            return source != null && _sources.Contains(source);
        }

        /// <summary>
        /// 返回当前来源次序的独立副本，仅供持有稳定领域身份的恢复所有者建立数据映射。
        /// 返回的运行时引用不能直接写入持久载荷，也不会改变来源的活动状态。
        /// </summary>
        public IMotionSource[] CaptureSourceOrder()
        {
            EnsureNotDisposed();
            return _sources.ToArray();
        }

        /// <summary>
        /// 只读校验目标次序是否为当前来源引用的完整无重复排列，不接受增删或替换来源。
        /// 预检调用此入口不会排序、取消或推进任何来源。
        /// </summary>
        public void ValidateSourceOrder(IReadOnlyList<IMotionSource> order)
        {
            EnsureNotDisposed();
            if (order == null || order.Count != _sources.Count)
                throw new InvalidOperationException("Motion source order requires the complete current membership.");
            var remaining = new HashSet<IMotionSource>(SourceReferenceComparer.Instance);
            foreach (var source in _sources)
                if (source == null || !remaining.Add(source))
                    throw new InvalidOperationException("Motion source membership is null or duplicated.");
            for (var i = 0; i < order.Count; i++)
                if (order[i] == null || !remaining.Remove(order[i]))
                    throw new InvalidOperationException("Motion source order contains an unknown or repeated reference.");
        }

        /// <summary>
        /// 经完整预检后静默恢复已有来源的精确次序，不经 AddSource，不改变正常排序和仲裁规则。
        /// 调用方负责稳定身份、检查点合法性和所属业务状态的补偿。
        /// </summary>
        public void RestoreSourceOrder(IReadOnlyList<IMotionSource> order)
        {
            ValidateSourceOrder(order);
            for (var i = 0; i < order.Count; i++) _sources[i] = order[i];
        }

        /// <summary>
        /// 静默应用领域所有者已预检的完整成员名单；不排序、不执行来源回调。
        /// 所有者负责核对当前成员及处理移除来源的生命周期。
        /// </summary>
        public void RestoreSourceMembership(IReadOnlyList<IMotionSource> order)
        {
            EnsureNotDisposed();
            if (order == null) throw new ArgumentNullException(nameof(order));
            var unique = new HashSet<IMotionSource>(SourceReferenceComparer.Instance);
            var priority = int.MaxValue;
            for (var i = 0; i < order.Count; i++)
            {
                var source = order[i];
                if (source == null || !unique.Add(source) || source.Priority > priority)
                    throw new InvalidOperationException("Invalid target Motion source membership or priority order.");
                priority = source.Priority;
            }
            if (_sources.Capacity < order.Count) _sources.Capacity = order.Count;
            _sources.Clear();
            for (var i = 0; i < order.Count; i++) _sources.Add(order[i]);
        }

        private sealed class SourceReferenceComparer : IEqualityComparer<IMotionSource>
        {
            internal static readonly SourceReferenceComparer Instance = new SourceReferenceComparer();

            public bool Equals(IMotionSource x, IMotionSource y) => ReferenceEquals(x, y);

            public int GetHashCode(IMotionSource obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public void AddSource(IMotionSource source)
        {
            EnsureNotDisposed();
            if (source == null) throw new ArgumentNullException(nameof(source));
            _sources.Add(source);
            _sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public bool RemoveSource(IMotionSource source)
        {
            EnsureNotDisposed();
            if (source == null) return false;
            return _sources.Remove(source);
        }

        public void ClearSources()
        {
            EnsureNotDisposed();
            _sources.Clear();
        }

        public void Reset()
        {
            EnsureNotDisposed();
            _sources.Clear();
            _bestIndexByGroup.Clear();
            _suppressedGroups.Clear();
            Solver = NoMotionSolver.Instance;
            Events = null;
            Policy = null;
        }

        public MotionSolveResult Tick(int id, ref MotionState state, float dt, ref MotionOutput output)
        {
            EnsureNotDisposed();
            output.Clear();

            var desired = Vec3.Zero;

            _bestIndexByGroup.Clear();
            _suppressedGroups.Clear();

            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null)
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                if (!s.IsActive)
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                if (s.Stacking == MotionStacking.Additive) continue;

                var gid = s.GroupId;
                if (_bestIndexByGroup.TryGetValue(gid, out var bestIdx))
                {
                    if (_sources[bestIdx].Priority < s.Priority)
                    {
                        _bestIndexByGroup[gid] = i;
                    }
                }
                else
                {
                    _bestIndexByGroup.Add(gid, i);
                }
            }

            if (Policy != null)
            {
                foreach (var kv in _bestIndexByGroup)
                {
                    var bestIdx = kv.Value;
                    if (bestIdx < 0 || bestIdx >= _sources.Count) continue;

                    var s = _sources[bestIdx];
                    if (s == null) continue;
                    if (!s.IsActive) continue;

                    if (s.Stacking != MotionStacking.OverrideLowerPriority) continue;

                    if (Policy.TryGetSuppressedGroups(s.GroupId, out var suppressed) && suppressed != null)
                    {
                        for (int j = 0; j < suppressed.Length; j++)
                        {
                            AddSuppressed(suppressed[j]);
                        }
                    }
                }
            }

            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null) continue;
                if (!s.IsActive) continue;

                if (IsSuppressed(s.GroupId)) continue;

                if (s.Stacking != MotionStacking.Additive)
                {
                    if (_bestIndexByGroup.TryGetValue(s.GroupId, out var bestIdx) && bestIdx != i)
                    {
                        continue;
                    }
                }

                s.Tick(id, ref state, dt, ref desired);

                if (!s.IsActive)
                {
                    NotifyFinished(id, in state, s);
                }
            }

            // 选主导贡献源的碰撞策略（按 group 优先级，命中可选 IMotionCollisionPolicySource 才透传）。
            output.HasDominantCollisionPolicy = false;
            for (int pi = 0; pi < PolicyGroupPrecedence.Length; pi++)
            {
                var gid = PolicyGroupPrecedence[pi];
                if (IsSuppressed(gid)) continue;

                for (int i = 0; i < _sources.Count; i++)
                {
                    var source = _sources[i];
                    if (source == null || source.GroupId != gid) continue;
                    if (source.Stacking != MotionStacking.Additive &&
                        (!_bestIndexByGroup.TryGetValue(gid, out var bestIdx) || bestIdx != i))
                    {
                        continue;
                    }

                    if (!source.IsActive &&
                        source is IMotionCompletionCollisionPolicySource completionPolicySource &&
                        completionPolicySource.HasCompletionCollisionPolicy)
                    {
                        output.DominantCollisionPolicy = completionPolicySource.CompletionCollisionPolicy;
                        output.CollisionSource = source;
                        output.HasDominantCollisionPolicy = true;
                        break;
                    }

                    if (source.IsActive &&
                        source is IMotionCollisionPolicySource policySource &&
                        policySource.HasCollisionPolicy)
                    {
                        output.DominantCollisionPolicy = policySource.CollisionPolicy;
                        output.CollisionSource = source;
                        output.HasDominantCollisionPolicy = true;
                        break;
                    }
                }

                if (output.HasDominantCollisionPolicy) break;
            }

            output.DesiredDelta = desired;

            var solver = Solver ?? NoMotionSolver.Instance;
            var result = solver.Solve(id, state, output, dt);

            output.AppliedDelta = result.AppliedDelta;
            output.NewVelocity = dt > 0f ? result.AppliedDelta / dt : Vec3.Zero;
            output.NewForward = state.Forward;

            state.Position = new Vec3(state.Position.X + result.AppliedDelta.X, state.Position.Y + result.AppliedDelta.Y, state.Position.Z + result.AppliedDelta.Z);
            state.Velocity = output.NewVelocity;
            // state.Time 是公共状态字段（快照/调试读），IEEE 加法位一致；源级计时（FixedDelta/Trajectory）已是 raw 定点。
            state.Time += dt;

            if (result.Hit.Hit)
            {
                Events?.OnHit(id, in state, in result.Hit);
            }

            return result;
        }

        private void NotifyFinished(int id, in MotionState state, IMotionSource source)
        {
            if (Events == null) return;

            var evt = MotionFinishEvent.Arrive;
            if (source is IMotionFinishEventSource finish)
            {
                evt = finish.FinishEvent;
            }

            switch (evt)
            {
                case MotionFinishEvent.Expired:
                    Events.OnExpired(id, in state);
                    break;
                case MotionFinishEvent.Arrive:
                    Events.OnArrive(id, in state);
                    break;
            }
        }

        private void AddSuppressed(int groupId)
        {
            for (int i = 0; i < _suppressedGroups.Count; i++)
            {
                if (_suppressedGroups[i] == groupId) return;
            }

            _suppressedGroups.Add(groupId);
        }

        private bool IsSuppressed(int groupId)
        {
            for (int i = 0; i < _suppressedGroups.Count; i++)
            {
                if (_suppressedGroups[i] == groupId) return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            MotionPipelinePool.ReleaseSourceList(_sources);
            MotionPipelinePool.ReleaseBestIndexDictionary(_bestIndexByGroup);
            MotionPipelinePool.ReleaseIntList(_suppressedGroups);
            _sources = null;
            _bestIndexByGroup = null;
            _suppressedGroups = null;
            Solver = NoMotionSolver.Instance;
            Events = null;
            Policy = null;
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MotionPipeline));
        }
    }
}
