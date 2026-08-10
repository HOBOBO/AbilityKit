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

        private static readonly BuffRuntimeIndex s_index = new BuffRuntimeIndex();

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
            s_runtimeListPool.Release(target.buffs.Active);
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
            s_index.MarkDirty();
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
            index = s_index.TryGetIndex(list, in key);
            if (index < 0)
            {
                runtime = null;
                return false;
            }

            runtime = list[index];
            return runtime != null;
        }

        private sealed class BuffRuntimeIndex
        {
            private int _version;
            private int _cachedVersion = -1;
            private int[] _indices = System.Array.Empty<int>();

            public void MarkDirty()
            {
                unchecked
                {
                    _version++;
                }
            }

            public void RebuildIfNeeded(List<BuffRuntime> list)
            {
                if (list == null) return;
                if (_cachedVersion == _version && _indices.Length == list.Count) return;
                _indices = new int[list.Count];
                for (var i = 0; i < list.Count; i++) _indices[i] = i;
                _cachedVersion = _version;
            }

            public int TryGetIndex(List<BuffRuntime> list, in BuffRuntimeKey key)
            {
                if (list == null) return -1;
                RebuildIfNeeded(list);
                for (var i = 0; i < _indices.Length; i++)
                {
                    var index = _indices[i];
                    if (index < 0 || index >= list.Count) continue;
                    if (key.Matches(list[index])) return index;
                }

                return -1;
            }
        }
    }
}
