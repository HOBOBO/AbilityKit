using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// 保存按 Key 排序的状态提供者、必需载荷要求及可选的完整结构恢复顺序。
    /// 装配完成后封存；清空后可以重新装配。
    /// </summary>
    public sealed class RollbackRegistry
    {
        private readonly List<IRollbackStateProvider> _providers = new List<IRollbackStateProvider>(16);
        private readonly Dictionary<int, IRollbackStateProvider> _byKey = new Dictionary<int, IRollbackStateProvider>(16);

        private bool _sealed;
        private readonly List<int> _requiredStateKeys = new List<int>();
        private IReadOnlyList<int> _structureRestoreOrder;

        /// <summary>
        /// 获取按 Key 升序排列的提供者；捕获顺序不受结构恢复顺序影响。
        /// </summary>
        public IReadOnlyList<IRollbackStateProvider> Providers => _providers;
        /// <summary>
        /// 获取恢复前必须各有且仅有一份载荷的提供者键。
        /// </summary>
        public IReadOnlyList<int> RequiredStateKeys => _requiredStateKeys.AsReadOnly();

        internal IReadOnlyList<int> StructureRestoreOrder => _structureRestoreOrder;

        /// <summary>
        /// 要求指定的已注册提供者在任何导入前各有且仅有一份载荷。
        /// 未声明要求的项继续支持部分快照；封存后禁止修改。
        /// </summary>
        public void RequireState(int key)
        {
            if (_sealed) throw new InvalidOperationException("Rollback registry is sealed.");
            if (!_byKey.ContainsKey(key)) throw new InvalidOperationException($"Required rollback provider not registered: {key}");
            if (_requiredStateKeys.Contains(key)) return;
            _requiredStateKeys.Add(key);
            _requiredStateKeys.Sort();
        }

        /// <summary>
        /// 复制并声明全部结构提供者的完整执行顺序，与 Key 大小无关。
        /// 封存前允许相同配置重复声明，冲突配置或封存后修改均拒绝；
        /// 此处不隐含载荷必需性，完整性在封存时验证。
        /// </summary>
        public void SetStructureRestoreOrder(params int[] keys)
        {
            if (_sealed) throw new InvalidOperationException("Rollback registry is sealed.");
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (_structureRestoreOrder != null)
            {
                if (_structureRestoreOrder.Count != keys.Length)
                    throw new InvalidOperationException("Rollback structure restore order is already configured differently.");
                for (var i = 0; i < keys.Length; i++)
                    if (_structureRestoreOrder[i] != keys[i])
                        throw new InvalidOperationException("Rollback structure restore order is already configured differently.");
                return;
            }
            _structureRestoreOrder = Array.AsReadOnly((int[])keys.Clone());
        }

        /// <summary>
        /// 验证显式结构顺序完整覆盖所有结构提供者后封存注册表，可幂等调用。
        /// 验证失败时不封存；未配置显式顺序则保留输入条目内次序。
        /// </summary>
        public void Seal()
        {
            if (_sealed) return;
            if (_structureRestoreOrder != null)
            {
                var seen = new HashSet<int>();
                foreach (var key in _structureRestoreOrder)
                {
                    if (!seen.Add(key))
                        throw new InvalidOperationException($"Duplicate rollback structure restore key: {key}");
                    if (!_byKey.TryGetValue(key, out var provider) || !(provider is IRollbackStructureRestoreProvider))
                        throw new InvalidOperationException($"Rollback structure restore key is not a registered structural provider: {key}");
                }
                foreach (var provider in _providers)
                    if (provider is IRollbackStructureRestoreProvider && !seen.Contains(provider.Key))
                        throw new InvalidOperationException($"Rollback structure restore order is missing provider key: {provider.Key}");
            }
            _sealed = true;
        }

        /// <summary>
        /// 注册状态提供者并按 Key 升序保存；同一实例可重复注册，不允许键冲突或封存后注册。
        /// </summary>
        public void Register(IRollbackStateProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (_sealed)
            {
                throw new InvalidOperationException($"Rollback registry is sealed. Cannot register provider key={provider.Key}");
            }

            if (_byKey.TryGetValue(provider.Key, out var existing) && existing != null)
            {
                if (!ReferenceEquals(existing, provider))
                {
                    throw new InvalidOperationException($"Rollback provider key already registered: {provider.Key}");
                }

                return;
            }

            _byKey[provider.Key] = provider;

            var insertIndex = _providers.Count;
            for (int i = 0; i < _providers.Count; i++)
            {
                var p = _providers[i];
                if (p == null) continue;
                if (provider.Key < p.Key)
                {
                    insertIndex = i;
                    break;
                }
            }

            _providers.Insert(insertIndex, provider);
        }

        /// <summary>
        /// 尝试按稳定键查询已注册的状态提供者。
        /// </summary>
        public bool TryGet(int key, out IRollbackStateProvider provider)
        {
            return _byKey.TryGetValue(key, out provider);
        }

        /// <summary>
        /// 清除全部注册、载荷要求和显式顺序，并解除封存。
        /// </summary>
        public void Clear()
        {
            _providers.Clear();
            _byKey.Clear();
            _requiredStateKeys.Clear();
            _structureRestoreOrder = null;
            _sealed = false;
        }
    }
}
