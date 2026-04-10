using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Marker;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 调度行为工厂接口 - 框架扩展点
    /// 业务包实现此接口来创建具体的调度行为
    /// </summary>
    public interface IScheduledExecutableFactory
    {
        /// <summary>
        /// 工厂名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 优先级，数字越大优先级越高
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 创建调度行为实例
        /// </summary>
        IScheduledExecutable Create(ScheduleFactoryConfig config, ActionRegistry actions, object context);
    }

    /// <summary>
    /// 标记调度行为工厂的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ScheduledExecutableFactoryAttribute : MarkerAttribute
    {
        public string ScheduleMode { get; }
        public int Priority { get; }

        public ScheduledExecutableFactoryAttribute(string scheduleMode, int priority = 0)
        {
            ScheduleMode = scheduleMode ?? throw new ArgumentNullException(nameof(scheduleMode));
            Priority = priority;
        }
    }

    /// <summary>
    /// 调度行为工厂注册表
    /// </summary>
    public sealed class ScheduledExecutableFactoryRegistry
    {
        private readonly Dictionary<string, IScheduledExecutableFactory> _factories = new();
        private readonly List<IScheduledExecutableFactory> _orderedFactories = new();

        public void Register(IScheduledExecutableFactory factory)
        {
            if (factory == null) return;
            _factories[factory.Name.ToLowerInvariant()] = factory;
            RebuildOrderedList();
        }

        public IScheduledExecutable Create(string scheduleMode, ScheduleFactoryConfig config, ActionRegistry actions, object context)
        {
            if (string.IsNullOrEmpty(scheduleMode))
                return null;

            var key = scheduleMode.ToLowerInvariant();
            if (_factories.TryGetValue(key, out var factory))
            {
                return factory.Create(config, actions, context);
            }

            foreach (var orderedFactory in _orderedFactories)
            {
                var result = orderedFactory.Create(config, actions, context);
                if (result != null) return result;
            }

            return null;
        }

        public IScheduledExecutable CreateDefault(ScheduleFactoryConfig config, ActionRegistry actions, object context)
        {
            return Create("external", config, actions, context);
        }

        private void RebuildOrderedList()
        {
            _orderedFactories.Clear();
            foreach (var pair in _factories)
                _orderedFactories.Add(pair.Value);
            _orderedFactories.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static ScheduledExecutableFactoryRegistry Default { get; } = new();
    }

    /// <summary>
    /// 工厂使用的调度配置
    /// </summary>
    public class ScheduleFactoryConfig
    {
        public string Mode { get; set; }
        public float DurationMs { get; set; } = -1;
        public float PeriodMs { get; set; } = 1000;
        public int MaxExecutions { get; set; } = -1;
        public bool CanBeInterrupted { get; set; } = true;
    }
}
