using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// 为单份快照提供只读载荷索引、输入次序的 Provider 视图及有效结构执行计划，
    /// 供跨 Provider 在导入前核对身份、依赖和实际阶段次序。
    /// </summary>
    public sealed class RollbackSnapshotPreflightContext
    {
        private readonly IReadOnlyDictionary<int, ReadOnlyMemory<byte>> _payloads;

        /// <summary>
        /// 复制条目索引和 Provider 映射；拒绝重复条目，按输入条目中的结构项生成有效计划。
        /// </summary>
        public RollbackSnapshotPreflightContext(
            FrameIndex frame,
            IReadOnlyList<WorldRollbackSnapshotEntry> entries,
            IReadOnlyDictionary<int, IRollbackStateProvider> providers)
            : this(frame, entries, providers, null)
        {
        }

        internal RollbackSnapshotPreflightContext(
            FrameIndex frame,
            IReadOnlyList<WorldRollbackSnapshotEntry> entries,
            IReadOnlyDictionary<int, IRollbackStateProvider> providers,
            IReadOnlyList<int> structureRestoreOrder)
        {
            Frame = frame;
            var payloads = new Dictionary<int, ReadOnlyMemory<byte>>();
            var providerCopy = new Dictionary<int, IRollbackStateProvider>();
            if (providers != null)
                foreach (var provider in providers)
                    providerCopy.Add(provider.Key, provider.Value);
            var providerOrder = new List<int>();
            var entryIndices = new Dictionary<int, int>();
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!payloads.TryAdd(entry.Key, entry.Payload ?? Array.Empty<byte>()))
                        throw new InvalidOperationException($"Rollback snapshot contains duplicate provider key: {entry.Key}");
                    entryIndices.Add(entry.Key, i);
                    if (providerCopy.ContainsKey(entry.Key)) providerOrder.Add(entry.Key);
                }
            }
            _payloads = new ReadOnlyDictionary<int, ReadOnlyMemory<byte>>(payloads);
            foreach (var key in providerCopy.Keys)
                if (!payloads.ContainsKey(key)) providerOrder.Add(key);
            Providers = new SnapshotProviderView(providerCopy, providerOrder);

            /*
             * 键视图和导入索引来自同一次计划生成，避免预检与执行各自推断顺序。
             * 显式顺序仅选择本次已有载荷的结构项，不将顺序声明当作载荷必需性。
             */
            var structureKeys = new List<int>();
            var structureIndices = new List<int>();
            foreach (var key in structureRestoreOrder ?? providerOrder)
            {
                if (!entryIndices.TryGetValue(key, out var index) ||
                    !providerCopy.TryGetValue(key, out var provider) ||
                    !(provider is IRollbackStructureRestoreProvider)) continue;
                structureKeys.Add(key);
                structureIndices.Add(index);
            }
            StructureRestoreKeys = structureKeys.AsReadOnly();
            StructureRestoreEntryIndices = structureIndices.AsReadOnly();
        }

        /// <summary>
        /// 保留快照成员的枚举次序，避免依赖特定运行时 Dictionary 的插入顺序。
        /// </summary>
        private sealed class SnapshotProviderView : IReadOnlyDictionary<int, IRollbackStateProvider>
        {
            private readonly Dictionary<int, IRollbackStateProvider> _providers;
            private readonly List<int> _order;

            internal SnapshotProviderView(Dictionary<int, IRollbackStateProvider> providers, List<int> order)
            { _providers = providers; _order = order; }

            /// <summary>
            /// 获取索引成员数。
            /// </summary>
            public int Count => _providers.Count;
            /// <summary>
            /// 按稳定键查询 Provider。
            /// </summary>
            public IRollbackStateProvider this[int key] => _providers[key];
            /// <summary>
            /// 按快照次序枚举键，未出现在快照中的注册项随后枚举。
            /// </summary>
            public IEnumerable<int> Keys => _order.AsReadOnly();
            /// <summary>
            /// 按键枚举的相同次序返回 Provider。
            /// </summary>
            public IEnumerable<IRollbackStateProvider> Values
            {
                get { foreach (var key in _order) yield return _providers[key]; }
            }
            /// <summary>
            /// 检查键是否已注册。
            /// </summary>
            public bool ContainsKey(int key) => _providers.ContainsKey(key);
            /// <summary>
            /// 尝试按键查询 Provider。
            /// </summary>
            public bool TryGetValue(int key, out IRollbackStateProvider value) => _providers.TryGetValue(key, out value);
            /// <summary>
            /// 按保留的输入次序枚举只读键值项。
            /// </summary>
            public IEnumerator<KeyValuePair<int, IRollbackStateProvider>> GetEnumerator()
            {
                foreach (var key in _order) yield return new KeyValuePair<int, IRollbackStateProvider>(key, _providers[key]);
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// 获取目标完整帧末状态的帧号。
        /// </summary>
        public FrameIndex Frame { get; }

        /// <summary>
        /// 提供只读 Provider 索引；快照内成员按输入条目次序枚举，其余注册项随后枚举。
        /// </summary>
        public IReadOnlyDictionary<int, IRollbackStateProvider> Providers { get; }

        /// <summary>
        /// 获取本次恢复实际执行的结构提供者键顺序；仅含本快照已提供的结构项。
        /// 协调器在全部预检通过后按此不可变计划导入，随后按输入次序导入普通项。
        /// </summary>
        public IReadOnlyList<int> StructureRestoreKeys { get; }

        internal IReadOnlyList<int> StructureRestoreEntryIndices { get; }

        /// <summary>
        /// 尝试查询当前快照中的只读载荷，不推断未提供的状态。
        /// </summary>
        public bool TryGetPayload(int key, out ReadOnlyMemory<byte> payload)
        {
            return _payloads.TryGetValue(key, out payload);
        }

        /// <summary>
        /// 获取必须存在的目标载荷；缺失时在导入前明确报错。
        /// </summary>
        public ReadOnlyMemory<byte> RequirePayload(int key)
        {
            if (!_payloads.TryGetValue(key, out var payload))
                throw new InvalidOperationException($"Rollback snapshot is missing provider payload: {key}");
            return payload;
        }
    }
}
