#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Markers;

namespace AbilityKit.Samples.Logic.Samples.Pipeline
{
    /// <summary>
    /// 标记配置类型对应的执行器
    /// 浣跨敤姝?Attribute 鏍囪閰嶇疆绫伙紝鑷姩寤虹珛鏄犲皠
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExecutorForAttribute : MarkerAttribute
    {
        public Type ExecutorType { get; }

        public ExecutorForAttribute(Type executorType)
        {
            ExecutorType = executorType;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (registry is ExecutorForRegistry typedRegistry && ExecutorType != null)
            {
                typedRegistry.Register(implType, ExecutorType);
            }
        }
    }

    /// <summary>
    /// 閰嶇疆鈫掓墽琛屽櫒鏄犲皠娉ㄥ唽琛?    /// Key: 閰嶇疆绫诲瀷, Value: 鎵ц鍣ㄧ被鍨?    /// </summary>
    public sealed class ExecutorForRegistry : KeyedMarkerRegistry<Type, ExecutorForAttribute>
    {
        public static ExecutorForRegistry Instance { get; } = new();

        private ExecutorForRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(ExecutorForRegistry).Assembly;
            MarkerScanner<ExecutorForAttribute>.Scan(new[] { assembly }, this);
        }

        /// <summary>
        /// 根据配置类型获取对应的执行器类型
        /// </summary>
        public Type GetExecutorType(Type configType)
        {
            if (TryGet(configType, out var executorType))
            {
                return executorType;
            }

            // 尝试查找基类
            var baseType = configType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (TryGet(baseType, out executorType))
                {
                    return executorType;
                }
                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// 获取所有配置→执行器的映射
        /// </summary>
        public IEnumerable<(Type ConfigType, Type ExecutorType)> GetAllMappings()
        {
            return Keys.Select(key => (key, TryGet(key, out var executorType) ? executorType : null))
                       .Where(tuple => tuple.Item2 != null)
                       .Select(tuple => (tuple.key, tuple.Item2!));
        }
    }
}
