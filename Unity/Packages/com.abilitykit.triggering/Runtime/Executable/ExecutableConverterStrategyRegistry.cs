using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Executable 转换策略注册表
    /// 管理所有可用的转换策略
    /// </summary>
    public sealed class ExecutableConverterStrategyRegistry
    {
        public static ExecutableConverterStrategyRegistry Default { get; } = new ExecutableConverterStrategyRegistry();

        private readonly Dictionary<int, IExecutableConverterStrategy> _byTypeId = new Dictionary<int, IExecutableConverterStrategy>();
        private readonly Dictionary<string, IExecutableConverterStrategy> _byTypeName = new Dictionary<string, IExecutableConverterStrategy>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IExecutableConverterStrategy> _inferenceStrategies = new List<IExecutableConverterStrategy>();

        private ExecutableConverterStrategyRegistry()
        {
            // 注册内置策略
            Register(new SequenceExecutableStrategy());
            Register(new SelectorExecutableStrategy());
            Register(new ParallelExecutableStrategy());
            Register(new RandomSelectorExecutableStrategy());
            Register(new RepeatExecutableStrategy());
            Register(new IfExecutableStrategy());
            Register(new IfElseExecutableStrategy());
            Register(new SwitchExecutableStrategy());
            Register(new ActionCallExecutableStrategy());
            Register(new DelayExecutableStrategy());
            Register(new ScheduleExecutableStrategy());

            // 推断型策略放在最后
            Register(new InferenceExecutableStrategy());
        }

        /// <summary>
        /// 注册转换策略
        /// </summary>
        public void Register(IExecutableConverterStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            if (strategy.TypeId > 0)
            {
                _byTypeId[strategy.TypeId] = strategy;
            }

            if (!string.IsNullOrEmpty(strategy.TypeName))
            {
                _byTypeName[strategy.TypeName] = strategy;
            }

            if (strategy is InferenceExecutableStrategy)
            {
                _inferenceStrategies.Add(strategy);
            }
        }

        /// <summary>
        /// 根据 TypeId 查找策略
        /// </summary>
        public bool TryGetByTypeId(int typeId, out IExecutableConverterStrategy strategy)
        {
            return _byTypeId.TryGetValue(typeId, out strategy);
        }

        /// <summary>
        /// 根据 TypeName 查找策略
        /// </summary>
        public bool TryGetByTypeName(string typeName, out IExecutableConverterStrategy strategy)
        {
            return _byTypeName.TryGetValue(typeName, out strategy);
        }

        /// <summary>
        /// 为配置找到合适的策略
        /// </summary>
        public IExecutableConverterStrategy FindStrategy(ExecutableConfig config)
        {
            // 1. 优先通过 TypeId 查找
            if (config.TypeId > 0)
            {
                if (TryGetByTypeId(config.TypeId, out var strategy))
                    return strategy;
            }

            // 2. 通过 TypeName 查找
            if (!string.IsNullOrEmpty(config.TypeName))
            {
                if (TryGetByTypeName(config.TypeName, out var strategy))
                    return strategy;
            }

            // 3. 使用推断策略
            foreach (var strategy in _inferenceStrategies)
            {
                if (strategy.CanHandle(config))
                    return strategy;
            }

            return null;
        }

        /// <summary>
        /// 获取所有注册的策略
        /// </summary>
        public IEnumerable<IExecutableConverterStrategy> GetAllStrategies()
        {
            foreach (var strategy in _byTypeId.Values)
                yield return strategy;
            foreach (var strategy in _inferenceStrategies)
                yield return strategy;
        }
    }
}
