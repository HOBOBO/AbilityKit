#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Action
{
    /// <summary>
    /// 动作执行上下文的默认实现。
    /// </summary>
    public sealed class ActionContext : IActionContext
    {
        public IActionExecutor Executor { get; }
        public object Source { get; }
        public object Target { get; }
        public IReadOnlyDictionary<string, object> Args { get; }
        public long ElapsedMs { get; internal set; }

        private readonly Dictionary<string, object> _data = new();

        public ActionContext(
            IActionExecutor executor,
            object source,
            object target = null,
            IReadOnlyDictionary<string, object> args = null)
        {
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Source = source;
            Target = target;
            Args = args ?? EmptyArgs;
            ElapsedMs = 0;
        }

        public T GetArg<T>(string key, T defaultValue = default)
        {
            if (Args.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetData(string key, object value)
        {
            _data[key] = value;
        }

        public T GetData<T>(string key)
        {
            if (_data.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        private static readonly IReadOnlyDictionary<string, object> EmptyArgs =
            new Dictionary<string, object>();

        private static readonly Dictionary<string, object> EmptyData = new();
    }

    /// <summary>
    /// 动作执行器的简单实现，基于字典服务。
    /// </summary>
    public sealed class SimpleActionExecutor : IActionExecutor
    {
        private readonly Dictionary<Type, object> _services = new();

        public SimpleActionExecutor()
        {
        }

        public SimpleActionExecutor(params (Type type, object service)[] services)
        {
            foreach (var (type, service) in services)
            {
                _services[type] = service;
            }
        }

        public void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public T GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service) ? service as T : null;
        }
    }
}
