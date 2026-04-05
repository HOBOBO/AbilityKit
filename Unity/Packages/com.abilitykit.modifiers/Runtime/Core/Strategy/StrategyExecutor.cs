using System;
using System.Collections.Generic;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 策略执行器 — 执行策略的核心组件
    // ============================================================================

    /// <summary>
    /// 策略执行器。
    /// 负责根据策略数据执行策略，并管理策略的生命周期。
    ///
    /// 设计说明：
    /// - 策略执行器本身无状态，可复用
    /// - 策略实例由外部存储，执行器只负责调用
    /// - 支持按 OwnerKey 批量还原
    /// </summary>
    public sealed class StrategyExecutor
    {
        private readonly IStrategyRegistry _registry;

        public StrategyExecutor(IStrategyRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 执行单个策略
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="data">策略数据</param>
        /// <returns>执行结果</returns>
        public StrategyApplyResult Execute(object target, in StrategyData data)
        {
            if (!_registry.TryGet(data.StrategyId, out var strategy))
            {
                return StrategyApplyResult.Failed($"Strategy '{data.StrategyId}' not found in registry");
            }

            var context = data.ToContext();
            return strategy.Apply(target, context);
        }

        /// <summary>
        /// 执行单个策略（使用实例）
        /// </summary>
        public StrategyApplyResult Execute(object target, in StrategyInstance instance)
        {
            if (instance.Strategy == null)
            {
                if (!_registry.TryGet(instance.StrategyId, out var strategy))
                {
                    return StrategyApplyResult.Failed($"Strategy '{instance.StrategyId}' not found in registry");
                }
                instance.Strategy = strategy;
            }

            return instance.Strategy.Apply(target, instance.ToContext());
        }

        /// <summary>
        /// 批量执行策略
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="datas">策略数据数组</param>
        /// <returns>执行结果列表</returns>
        public List<StrategyApplyResult> ExecuteBatch(object target, ReadOnlySpan<StrategyData> datas)
        {
            var results = new List<StrategyApplyResult>(datas.Length);
            for (int i = 0; i < datas.Length; i++)
            {
                results.Add(Execute(target, in datas[i]));
            }
            return results;
        }

        /// <summary>
        /// 还原单个策略
        /// </summary>
        public StrategyRevertResult Revert(object target, in StrategyData data)
        {
            if (!_registry.TryGet(data.StrategyId, out var strategy))
            {
                return StrategyRevertResult.Failed($"Strategy '{data.StrategyId}' not found in registry");
            }

            var context = data.ToContext();
            return strategy.Revert(target, context);
        }

        /// <summary>
        /// 还原单个策略（使用实例）
        /// </summary>
        public StrategyRevertResult Revert(object target, in StrategyInstance instance)
        {
            if (instance.Strategy == null)
            {
                if (!_registry.TryGet(instance.StrategyId, out var strategy))
                {
                    return StrategyRevertResult.Failed($"Strategy '{instance.StrategyId}' not found in registry");
                }
                instance.Strategy = strategy;
            }

            return instance.Strategy.Revert(target, instance.ToContext());
        }

        /// <summary>
        /// 批量还原策略
        /// </summary>
        public List<StrategyRevertResult> RevertBatch(object target, ReadOnlySpan<StrategyData> datas)
        {
            var results = new List<StrategyRevertResult>(datas.Length);
            for (int i = 0; i < datas.Length; i++)
            {
                results.Add(Revert(target, in datas[i]));
            }
            return results;
        }

        /// <summary>
        /// 计算策略对数值的影响
        /// </summary>
        public T Calculate<T>(T baseValue, in StrategyData data)
        {
            if (!_registry.TryGet(data.StrategyId, out var strategy))
            {
                return baseValue;
            }

            var context = data.ToContext();
            return strategy.Calculate(baseValue, context);
        }
    }

    // ============================================================================
    // 策略仓储 — 管理实体的策略实例
    // ============================================================================

    /// <summary>
    /// 策略仓储接口
    /// 管理实体的策略实例，支持按 OwnerKey 批量还原
    /// </summary>
    public interface IStrategyRepository
    {
        /// <summary>
        /// 应用策略
        /// </summary>
        StrategyApplyResult Apply(object target, in StrategyData data, out StrategyInstance instance);

        /// <summary>
        /// 还原策略
        /// </summary>
        StrategyRevertResult Revert(object target, in StrategyInstance instance);

        /// <summary>
        /// 按 OwnerKey 还原所有策略
        /// </summary>
        void RevertByOwner(object target, long ownerKey);

        /// <summary>
        /// 获取指定目标的所有策略实例
        /// </summary>
        IReadOnlyList<StrategyInstance> GetByTarget(object target);

        /// <summary>
        /// 获取指定拥有者的所有策略实例
        /// </summary>
        IReadOnlyList<StrategyInstance> GetByOwner(long ownerKey);

        /// <summary>
        /// 检查是否拥有指定策略
        /// </summary>
        bool HasStrategy(object target, string strategyId);

        /// <summary>
        /// 清空目标的所有策略
        /// </summary>
        void Clear(object target);

        /// <summary>
        /// 清空所有策略
        /// </summary>
        void ClearAll();
    }

    /// <summary>
    /// 策略仓储实现
    /// </summary>
    public sealed class StrategyRepository : IStrategyRepository
    {
        private readonly IStrategyRegistry _registry;
        private readonly StrategyExecutor _executor;

        // Target -> List<StrategyInstance>
        private readonly Dictionary<object, List<StrategyInstance>> _byTarget = new(new ObjectReferenceComparer());

        // OwnerKey -> List<StrategyInstance>
        private readonly Dictionary<long, List<StrategyInstance>> _byOwner = new();

        // InstanceId -> StrategyInstance
        private readonly Dictionary<int, StrategyInstance> _byId = new();

        private int _nextInstanceId = 1;

        public StrategyRepository(IStrategyRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _executor = new StrategyExecutor(registry);
        }

        public StrategyApplyResult Apply(object target, in StrategyData data, out StrategyInstance instance)
        {
            // 查找策略
            if (!_registry.TryGet(data.StrategyId, out var strategy))
            {
                instance = default;
                return StrategyApplyResult.Failed($"Strategy '{data.StrategyId}' not found in registry");
            }

            // 创建实例
            instance = new StrategyInstance
            {
                Id = _nextInstanceId++,
                Data = data,
                Strategy = strategy
            };

            // 执行策略
            var result = strategy.Apply(target, instance.ToContext());
            if (!result.Success)
            {
                return result;
            }

            // 存储实例
            // 按目标存储
            if (!_byTarget.TryGetValue(target, out var targetList))
            {
                targetList = new List<StrategyInstance>();
                _byTarget[target] = targetList;
            }
            targetList.Add(instance);

            // 按拥有者存储
            if (!_byOwner.TryGetValue(data.OwnerKey, out var ownerList))
            {
                ownerList = new List<StrategyInstance>();
                _byOwner[data.OwnerKey] = ownerList;
            }
            ownerList.Add(instance);

            // 按ID存储
            _byId[instance.Id] = instance;

            return result;
        }

        public StrategyRevertResult Revert(object target, in StrategyInstance instance)
        {
            if (instance.Strategy == null)
            {
                if (!_registry.TryGet(instance.StrategyId, out var strategy))
                {
                    return StrategyRevertResult.Failed($"Strategy '{instance.StrategyId}' not found in registry");
                }
                instance.Strategy = strategy;
            }

            var result = instance.Strategy.Revert(target, instance.ToContext());

            if (result.Success)
            {
                // 从存储中移除
                RemoveInstance(instance);
            }

            return result;
        }

        public void RevertByOwner(object target, long ownerKey)
        {
            if (!_byOwner.TryGetValue(ownerKey, out var instances))
            {
                return;
            }

            // 复制列表以避免在迭代时修改
            var instancesToRevert = new List<StrategyInstance>(instances);

            foreach (var instance in instancesToRevert)
            {
                // 查找此实例关联的目标
                // 注意：一个实例可能关联多个目标
                if (_byTarget.TryGetValue(target, out var targetList))
                {
                    var index = targetList.FindIndex(i => i.Id == instance.Id);
                    if (index >= 0)
                    {
                        instance.Strategy?.Revert(target, instance.ToContext());
                        targetList.RemoveAt(index);
                    }
                }

                // 从拥有者列表中移除
                instances.Remove(instance);

                // 从ID索引中移除
                _byId.Remove(instance.Id);
            }

            // 清理空列表
            if (instances.Count == 0)
            {
                _byOwner.Remove(ownerKey);
            }
        }

        public IReadOnlyList<StrategyInstance> GetByTarget(object target)
        {
            if (_byTarget.TryGetValue(target, out var list))
            {
                return list;
            }
            return Array.Empty<StrategyInstance>();
        }

        public IReadOnlyList<StrategyInstance> GetByOwner(long ownerKey)
        {
            if (_byOwner.TryGetValue(ownerKey, out var list))
            {
                return list;
            }
            return Array.Empty<StrategyInstance>();
        }

        public bool HasStrategy(object target, string strategyId)
        {
            if (_byTarget.TryGetValue(target, out var list))
            {
                foreach (var instance in list)
                {
                    if (instance.Data.StrategyId == strategyId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Clear(object target)
        {
            if (_byTarget.TryGetValue(target, out var list))
            {
                foreach (var instance in list)
                {
                    // 还原策略
                    instance.Strategy?.Revert(target, instance.ToContext());

                    // 从拥有者列表中移除
                    if (_byOwner.TryGetValue(instance.OwnerKey, out var ownerList))
                    {
                        ownerList.RemoveAll(i => i.Id == instance.Id);
                        if (ownerList.Count == 0)
                        {
                            _byOwner.Remove(instance.OwnerKey);
                        }
                    }

                    // 从ID索引中移除
                    _byId.Remove(instance.Id);
                }

                _byTarget.Remove(target);
            }
        }

        public void ClearAll()
        {
            _byTarget.Clear();
            _byOwner.Clear();
            _byId.Clear();
        }

        private void RemoveInstance(StrategyInstance instance)
        {
            // 从目标列表中移除
            foreach (var list in _byTarget.Values)
            {
                list.RemoveAll(i => i.Id == instance.Id);
            }

            // 从拥有者列表中移除
            if (_byOwner.TryGetValue(instance.OwnerKey, out var ownerList))
            {
                ownerList.RemoveAll(i => i.Id == instance.Id);
                if (ownerList.Count == 0)
                {
                    _byOwner.Remove(instance.OwnerKey);
                }
            }

            // 从ID索引中移除
            _byId.Remove(instance.Id);
        }

        /// <summary>
        /// 对象引用比较器
        /// </summary>
        private sealed class ObjectReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
