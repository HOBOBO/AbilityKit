using System;
using System.Collections.Generic;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 策略执行扩展方法
    // ============================================================================

    /// <summary>
    /// 策略执行扩展方法
    /// 提供便捷的策略执行API
    /// </summary>
    public static class StrategyExtensions
    {
        /// <summary>
        /// 创建并注册默认策略注册表
        /// </summary>
        public static IStrategyRegistry CreateDefaultRegistry()
        {
            var registry = new StrategyRegistry();

            // 注册内置策略
            registry.RegisterRange(new IStrategy[]
            {
                // 数值策略
                new NumericAddStrategy(),
                new NumericMultStrategy(),
                new NumericOverrideStrategy(),
                new NumericPercentStrategy(),

                // 状态策略
                new StateSetStrategy(),
                new StateRestoreStrategy(),

                // 标签策略
                new TagAddStrategy(),
                new TagRemoveStrategy(),
            });

            return registry;
        }

        /// <summary>
        /// 应用策略（使用注册表查找策略）
        /// </summary>
        public static StrategyApplyResult Apply(
            this IStrategyRegistry registry,
            object target,
            in StrategyData data,
            out StrategyInstance instance)
        {
            var repository = new StrategyRepository(registry);
            return repository.Apply(target, data, out instance);
        }

        /// <summary>
        /// 应用策略（使用注册表查找策略）
        /// </summary>
        public static StrategyApplyResult Apply(
            this IStrategyRegistry registry,
            object target,
            in StrategyData data)
        {
            return Apply(registry, target, data, out _);
        }

        /// <summary>
        /// 执行数值策略
        /// </summary>
        public static float ExecuteNumeric(
            this IStrategyRegistry registry,
            float baseValue,
            string strategyId,
            StrategyOperationKind op,
            float value,
            long ownerKey = 0,
            int sourceId = 0,
            float level = 1f)
        {
            var data = StrategyData.Numeric(
                strategyId,
                op,
                targetKey: string.Empty,
                value,
                ownerKey,
                sourceId: sourceId,
                level: level
            );

            var repository = new StrategyRepository(registry);
            return repository.Calculate(baseValue, in data);
        }

        /// <summary>
        /// 应用状态修改
        /// </summary>
        public static StrategyApplyResult ApplyState(
            this IStrategyRegistry registry,
            object target,
            string stateKey,
            object newValue,
            long ownerKey,
            int sourceId = 0)
        {
            var data = StrategyData.State(
                "state.set",
                StrategyOperationKind.SaveAndSet,
                stateKey,
                newValue,
                ownerKey,
                sourceId: sourceId
            );

            var repository = new StrategyRepository(registry);
            return repository.Apply(target, data, out _);
        }

        /// <summary>
        /// 应用标签添加
        /// </summary>
        public static StrategyApplyResult ApplyTag(
            this IStrategyRegistry registry,
            object target,
            string tag,
            long ownerKey,
            int sourceId = 0)
        {
            var data = StrategyData.Tag(
                "tag.add",
                StrategyOperationKind.ListAdd,
                tag,
                ownerKey,
                sourceId
            );

            var repository = new StrategyRepository(registry);
            return repository.Apply(target, data, out _);
        }

        /// <summary>
        /// 按 OwnerKey 还原所有策略
        /// </summary>
        public static void RevertByOwner(
            this IStrategyRegistry registry,
            object target,
            long ownerKey)
        {
            var repository = new StrategyRepository(registry);
            repository.RevertByOwner(target, ownerKey);
        }
    }

    // ============================================================================
    // 策略仓储扩展
    // ============================================================================

    /// <summary>
    /// 策略仓储扩展方法
    /// </summary>
    public static class StrategyRepositoryExtensions
    {
        /// <summary>
        /// 计算数值策略对基础值的影响
        /// </summary>
        public static float Calculate(
            this IStrategyRepository repository,
            float baseValue,
            in StrategyData data)
        {
            if (data.StrategyId == "numeric.add")
            {
                var value = data.Value is float f ? f : Convert.ToSingle(data.Value);
                return baseValue + value;
            }
            if (data.StrategyId == "numeric.mult")
            {
                var value = data.Value is float f ? f : Convert.ToSingle(data.Value);
                return baseValue * value;
            }
            if (data.StrategyId == "numeric.override")
            {
                return data.Value is float f ? f : Convert.ToSingle(data.Value);
            }
            if (data.StrategyId == "numeric.percent")
            {
                var value = data.Value is float f ? f : Convert.ToSingle(data.Value);
                return baseValue * (1f + value);
            }
            return baseValue;
        }

        /// <summary>
        /// 创建策略仓储
        /// </summary>
        public static IStrategyRepository CreateRepository(this IStrategyRegistry registry)
        {
            return new StrategyRepository(registry);
        }
    }
}
