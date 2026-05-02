using System.Collections.Generic;
using AbilityKit.Trace;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 溯源注册表提供者接口
    /// 用于在编辑器中获取 TraceTreeRegistry 实例
    /// 业务层可以实现此接口来自定义如何获取注册表
    /// </summary>
    public interface ITraceRegistryProvider
    {
        /// <summary>
        /// 获取所有可用的溯源注册表
        /// </summary>
        IEnumerable<TraceTreeRegistryBase> GetRegistries();
    }

    /// <summary>
    /// 默认的溯源注册表提供者
    /// 通过反射自动查找所有 TraceTreeRegistry 实例
    /// </summary>
    public sealed class DefaultTraceRegistryProvider : ITraceRegistryProvider
    {
        private static DefaultTraceRegistryProvider _instance;

        public static DefaultTraceRegistryProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultTraceRegistryProvider();
                }
                return _instance;
            }
        }

        public IEnumerable<TraceTreeRegistryBase> GetRegistries()
        {
            var result = new List<TraceTreeRegistryBase>();
            var baseType = typeof(TraceTreeRegistryBase);

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (baseType.IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        {
                            // 尝试获取 Instance 属性
                            var instanceProp = type.GetProperty("Instance",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (instanceProp != null)
                            {
                                var value = instanceProp.GetValue(null);
                                if (value is TraceTreeRegistryBase registry)
                                {
                                    result.Add(registry);
                                    continue;
                                }
                            }

                            // 尝试获取 Singleton 属性
                            var singletonProp = type.GetProperty("Singleton",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (singletonProp != null)
                            {
                                var value = singletonProp.GetValue(null);
                                if (value is TraceTreeRegistryBase registry)
                                {
                                    result.Add(registry);
                                }
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                    // 忽略无法加载的程序集
                }
            }

            return result;
        }

        private DefaultTraceRegistryProvider() { }
    }
}
