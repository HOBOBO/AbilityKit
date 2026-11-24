#nullable enable
using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    /// <summary>
    /// 在各世界容器内创建时间服务，并识别属于当前模块安装周期的实例。
    /// 模块不保存世界或服务的引用；调用方只查询仍由管理器登记的世界。
    /// </summary>
    internal sealed class HostedWorldService<TService> where TService : class
    {
        /// <summary>
        /// 将装配标识与服务一起保存在世界容器中，不承担服务的释放责任。
        /// </summary>
        private sealed class Binding
        {
            internal Binding(object installation, TService service)
            {
                Installation = installation;
                Service = service;
            }

            internal object Installation { get; }
            internal TService Service { get; }
        }

        private readonly Func<TService> _create;
        private object _installation = new object();

        internal HostedWorldService(Func<TService> create)
        {
            _create = create;
        }

        internal void Register(WorldCreateOptions options)
        {
            var builder = options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
            var installation = _installation;
            var create = _create;
            builder.Register<Binding>(WorldLifetime.Singleton, _ => new Binding(installation, create()));
            builder.Register<TService>(WorldLifetime.Singleton, resolver => resolver.Resolve<Binding>().Service);
        }

        internal bool TryGet(IWorld world, out TService service)
        {
            var resolver = world.Services;
            if (resolver != null && resolver.TryResolve<Binding>(out var binding) && binding != null &&
                ReferenceEquals(binding.Installation, _installation) &&
                resolver.TryResolve<TService>(out var current) && ReferenceEquals(current, binding.Service))
            {
                service = binding.Service;
                return true;
            }

            service = null!;
            return false;
        }

        internal void Detach()
        {
            _installation = new object();
        }
    }
}
