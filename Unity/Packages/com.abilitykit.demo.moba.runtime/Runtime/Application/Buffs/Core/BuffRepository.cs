using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Demo.Moba.Services.Buffs.Core
{
    /// <summary>
    /// Buff 运行时仓库：统一管理 BuffRuntime/List 的对象池和列表索引失效。
    /// 注意：实例加入和释放由生命周期执行器负责，这里只负责容器与查找语义。
    /// </summary>
    internal sealed class BuffRepository
    {
        private static readonly ObjectPool<List<BuffRuntime>> s_runtimeListPool = Pools.GetPool(
            createFunc: () => new List<BuffRuntime>(8),
            onRelease: list => list.Clear(),
            defaultCapacity: 32,
            maxSize: 256,
            collectionCheck: false);

        private static readonly ObjectPool<BuffRuntime> s_runtimePool = Pools.GetPool(
            createFunc: () => new BuffRuntime(),
            onRelease: runtime => new BuffRuntimeView(runtime).ClearRuntimeBindings(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private static readonly ConditionalWeakTable<List<BuffRuntime>, BuffRuntimeIndex> s_indices =
            new ConditionalWeakTable<List<BuffRuntime>, BuffRuntimeIndex>();

        public List<BuffRuntime> GetOrCreateList(global::ActorEntity target)
        {
            var list = target != null && target.hasBuffs ? target.buffs.Active : null;
            if (list != null) return list;
            list = s_runtimeListPool.Get();
            target?.ReplaceBuffs(list);
            return list;
        }

        public static BuffRuntime RentRuntime()
        {
            return s_runtimePool.Get();
        }

        public static void ReleaseRuntime(BuffRuntime runtime)
        {
            if (runtime == null) return;
            s_runtimePool.Release(runtime);
        }

        public static void ReleaseList(global::ActorEntity target)
        {
            if (target == null || !target.hasBuffs || target.buffs.Active == null) return;
            var list = target.buffs.Active;
            s_indices.Remove(list);
            s_runtimeListPool.Release(list);
            target.ReplaceBuffs(null);
        }

        public static int FindExistingBuffIndex(List<BuffRuntime> list, in BuffRuntimeKey key)
        {
            return TryGetIndexedRuntime(list, in key, out _, out var index) ? index : -1;
        }

        public static bool TryGetRuntime(List<BuffRuntime> list, in BuffRuntimeKey key, out BuffRuntime runtime, out int index)
        {
            return TryGetIndexedRuntime(list, in key, out runtime, out index);
        }

        /// <summary>
        /// 注册运行时变更。调用方已把 runtime 加入列表，这里只标记索引需要刷新，避免重复添加。
        /// </summary>
        public static void RegisterRuntime(List<BuffRuntime> list, BuffRuntime runtime)
        {
            if (list == null || runtime == null) return;
            MarkDirty(list);
        }

        public static void MarkDirty(List<BuffRuntime> list)
        {
            if (list == null) return;
            s_indices.GetOrCreateValue(list).MarkDirty();
        }

        /// <summary>
        /// 从列表移除指定运行时，但不释放对象；释放必须留给 EndRuntime 的清理顺序统一处理。
        /// </summary>
        public static bool RemoveAt(List<BuffRuntime> list, int index, BuffRuntime expectedRuntime)
        {
            if (list == null) return false;
            if (index < 0 || index >= list.Count) return false;
            if (expectedRuntime != null && !ReferenceEquals(list[index], expectedRuntime)) return false;
            list.RemoveAt(index);
            MarkDirty(list);
            return true;
        }

        /// <summary>
        /// 原子替换指定槽位。引用校验失败时列表保持不变。
        /// </summary>
        public static bool ReplaceAt(List<BuffRuntime> list, int index, BuffRuntime expectedRuntime, BuffRuntime replacement)
        {
            if (list == null || replacement == null) return false;
            if (index < 0 || index >= list.Count) return false;
            if (expectedRuntime != null && !ReferenceEquals(list[index], expectedRuntime)) return false;
            list[index] = replacement;
            MarkDirty(list);
            return true;
        }

        private static bool TryGetIndexedRuntime(List<BuffRuntime> list, in BuffRuntimeKey key, out BuffRuntime runtime, out int index)
        {
            if (list == null)
            {
                runtime = null;
                index = -1;
                return false;
            }

            index = s_indices.GetOrCreateValue(list).TryGetIndex(list, in key);
            if (index < 0 || index >= list.Count)
            {
                runtime = null;
                return false;
            }

            runtime = list[index];
            return runtime != null && key.Matches(runtime);
        }

        private sealed class BuffRuntimeIndex
        {
            private readonly Dictionary<int, int> _byBuff = new Dictionary<int, int>();
            private readonly Dictionary<BuffSourceKey, int> _byBuffAndSource = new Dictionary<BuffSourceKey, int>();
            private readonly Dictionary<BuffContextKey, int> _byBuffAndContext = new Dictionary<BuffContextKey, int>();
            private readonly Dictionary<BuffInstanceKey, int> _byInstance = new Dictionary<BuffInstanceKey, int>();
            private bool _dirty = true;
            private int _indexedCount = -1;

            public void MarkDirty()
            {
                _dirty = true;
            }

            public int TryGetIndex(List<BuffRuntime> list, in BuffRuntimeKey key)
            {
                RebuildIfNeeded(list);

                if (key.SourceContextId != 0L)
                {
                    if (key.SourceActorId > 0)
                    {
                        return _byInstance.TryGetValue(
                            new BuffInstanceKey(key.BuffId, key.SourceActorId, key.SourceContextId),
                            out var instanceIndex)
                            ? instanceIndex
                            : -1;
                    }

                    return _byBuffAndContext.TryGetValue(
                        new BuffContextKey(key.BuffId, key.SourceContextId),
                        out var contextIndex)
                        ? contextIndex
                        : -1;
                }

                if (key.SourceActorId > 0)
                {
                    return _byBuffAndSource.TryGetValue(
                        new BuffSourceKey(key.BuffId, key.SourceActorId),
                        out var sourceIndex)
                        ? sourceIndex
                        : -1;
                }

                return _byBuff.TryGetValue(key.BuffId, out var buffIndex) ? buffIndex : -1;
            }

            private void RebuildIfNeeded(List<BuffRuntime> list)
            {
                if (!_dirty && _indexedCount == list.Count) return;

                _byBuff.Clear();
                _byBuffAndSource.Clear();
                _byBuffAndContext.Clear();
                _byInstance.Clear();
                for (var i = 0; i < list.Count; i++)
                {
                    var runtime = list[i];
                    if (runtime == null) continue;

                    if (!_byBuff.ContainsKey(runtime.BuffId))
                    {
                        _byBuff.Add(runtime.BuffId, i);
                    }

                    var sourceKey = new BuffSourceKey(runtime.BuffId, runtime.SourceId);
                    if (!_byBuffAndSource.ContainsKey(sourceKey))
                    {
                        _byBuffAndSource.Add(sourceKey, i);
                    }

                    var contextKey = new BuffContextKey(runtime.BuffId, runtime.SourceContextId);
                    if (!_byBuffAndContext.ContainsKey(contextKey))
                    {
                        _byBuffAndContext.Add(contextKey, i);
                    }

                    var instanceKey = new BuffInstanceKey(runtime.BuffId, runtime.SourceId, runtime.SourceContextId);
                    if (!_byInstance.ContainsKey(instanceKey))
                    {
                        _byInstance.Add(instanceKey, i);
                    }
                }

                _indexedCount = list.Count;
                _dirty = false;
            }
        }

        private readonly struct BuffSourceKey : System.IEquatable<BuffSourceKey>
        {
            private readonly int _buffId;
            private readonly int _sourceActorId;

            public BuffSourceKey(int buffId, int sourceActorId)
            {
                _buffId = buffId;
                _sourceActorId = sourceActorId;
            }

            public bool Equals(BuffSourceKey other) =>
                _buffId == other._buffId && _sourceActorId == other._sourceActorId;

            public override bool Equals(object obj) => obj is BuffSourceKey other && Equals(other);
            public override int GetHashCode() => (_buffId * 397) ^ _sourceActorId;
        }

        private readonly struct BuffContextKey : System.IEquatable<BuffContextKey>
        {
            private readonly int _buffId;
            private readonly long _sourceContextId;

            public BuffContextKey(int buffId, long sourceContextId)
            {
                _buffId = buffId;
                _sourceContextId = sourceContextId;
            }

            public bool Equals(BuffContextKey other) =>
                _buffId == other._buffId && _sourceContextId == other._sourceContextId;

            public override bool Equals(object obj) => obj is BuffContextKey other && Equals(other);
            public override int GetHashCode() => (_buffId * 397) ^ _sourceContextId.GetHashCode();
        }

        private readonly struct BuffInstanceKey : System.IEquatable<BuffInstanceKey>
        {
            private readonly int _buffId;
            private readonly int _sourceActorId;
            private readonly long _sourceContextId;

            public BuffInstanceKey(int buffId, int sourceActorId, long sourceContextId)
            {
                _buffId = buffId;
                _sourceActorId = sourceActorId;
                _sourceContextId = sourceContextId;
            }

            public bool Equals(BuffInstanceKey other) =>
                _buffId == other._buffId &&
                _sourceActorId == other._sourceActorId &&
                _sourceContextId == other._sourceContextId;

            public override bool Equals(object obj) => obj is BuffInstanceKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (_buffId * 397) ^ _sourceActorId;
                    return (hash * 397) ^ _sourceContextId.GetHashCode();
                }
            }
        }
    }
}
