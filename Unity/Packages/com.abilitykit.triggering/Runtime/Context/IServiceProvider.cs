using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 服务提供器接口
    /// 用于向 ActionContext 注入运行时服务
    /// </summary>
    public interface IServiceProvider
    {
        /// <summary>
        /// 获取服务实例
        /// </summary>
        object GetService(Type serviceType);

        /// <summary>
        /// 获取服务实例（泛型）
        /// </summary>
        T GetService<T>() where T : class;
    }

    /// <summary>
    /// 允许宿主在 Trigger Action 执行前后建立额外的作用域语义。
    /// </summary>
    public interface ITriggerActionExecutionScopeObserver
    {
        void EnterActionExecution(int actionIndex, long actionId);
        void ExitActionExecution(int actionIndex, long actionId, bool succeeded);
    }

    /// <summary>
    /// 服务提供器基类（简易实现）
    /// </summary>
    public abstract class ServiceProviderBase : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        protected void Register<TService>(TService instance) where TService : class
        {
            _services[typeof(TService)] = instance;
        }

        protected void Register(Type serviceType, object instance)
        {
            _services[serviceType] = instance;
        }

        public virtual object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return _services.TryGetValue(serviceType, out var service) ? service : null;
        }

        public T GetService<T>() where T : class
        {
            return (T)GetService(typeof(T));
        }
    }
}
