#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic
{
    /// <summary>
    /// 示例注册表 - 自动扫描并注册所有带 [Sample] 标记的类型
    /// </summary>
    public sealed class SampleRegistry
    {
        public static SampleRegistry Instance { get; } = new();

        private readonly Dictionary<Type, SampleRegistration> _registrations = new();
        private bool _initialized = false;

        private SampleRegistry() { }

        /// <summary>
        /// 初始化注册表（扫描所有带 [Sample] 标记的类型）
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var assemblies = new[]
            {
                Assembly.GetExecutingAssembly(),
                typeof(SampleRegistry).Assembly
            };

            foreach (var assembly in assemblies)
            {
                ScanAssembly(assembly);
            }
        }

        /// <summary>
        /// 扫描程序集中的所有示例类型
        /// </summary>
        private void ScanAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                if (!typeof(ISample).IsAssignableFrom(type))
                    continue;

                var attr = type.GetCustomAttribute<SampleAttribute>();
                if (attr != null)
                {
                    Register(type, attr);
                }
            }
        }

        /// <summary>
        /// 注册示例类型
        /// </summary>
        private void Register(Type type, SampleAttribute attribute)
        {
            if (_registrations.ContainsKey(type))
                return;

            _registrations[type] = new SampleRegistration
            {
                Type = type,
                Priority = attribute.Priority,
                Tags = attribute.Tags
            };
        }

        /// <summary>
        /// 获取所有注册的示例类型
        /// </summary>
        public IReadOnlyList<Type> GetAllSampleTypes()
        {
            return _registrations.Values
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Type.Name)
                .Select(r => r.Type)
                .ToList();
        }

        /// <summary>
        /// 创建示例实例
        /// </summary>
        public ISample? CreateInstance(Type type)
        {
            if (!_registrations.ContainsKey(type))
                return null;

            try
            {
                return Activator.CreateInstance(type) as ISample;
            }
            catch
            {
                return null;
            }
        }

        private class SampleRegistration
        {
            public Type Type { get; init; } = null!;
            public int Priority { get; init; }
            public string[] Tags { get; init; } = Array.Empty<string>();
        }
    }
}
