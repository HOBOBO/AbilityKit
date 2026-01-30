using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldContainerBuilder
    {
        private readonly Dictionary<Type, WorldServiceDescriptor> _map = new Dictionary<Type, WorldServiceDescriptor>();

        public WorldContainerBuilder AddModule(IWorldModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            module.Configure(this);
            return this;
        }

        public WorldContainerBuilder Register(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _map[serviceType] = new WorldServiceDescriptor(serviceType, lifetime, factory);
            return this;
        }

        public WorldContainerBuilder TryRegister(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!_map.ContainsKey(serviceType))
            {
                _map[serviceType] = new WorldServiceDescriptor(serviceType, lifetime, factory);
            }
            return this;
        }

        public WorldContainerBuilder Register<TService>(WorldLifetime lifetime, Func<IWorldResolver, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Register(typeof(TService), lifetime, r => factory(r));
        }

        public WorldContainerBuilder TryRegister<TService>(WorldLifetime lifetime, Func<IWorldResolver, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return TryRegister(typeof(TService), lifetime, r => factory(r));
        }

        public WorldContainerBuilder RegisterInstance<TService>(TService instance)
        {
            return Register(typeof(TService), WorldLifetime.Singleton, _ => instance);
        }

        public WorldContainerBuilder Register<TService>(Func<IWorldResolver, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Register(typeof(TService), WorldLifetime.Scoped, r => factory(r));
        }

        public WorldContainerBuilder TryRegister<TService>(Func<IWorldResolver, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return TryRegister(typeof(TService), WorldLifetime.Scoped, r => factory(r));
        }

        public WorldContainerBuilder RegisterType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return Register(typeof(TService), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
        }

        public WorldContainerBuilder RegisterType<TService, TImpl>()
            where TImpl : TService
        {
            return RegisterType<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder RegisterType(Type serviceType, Type implType, WorldLifetime lifetime)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (implType == null) throw new ArgumentNullException(nameof(implType));
            return Register(serviceType, lifetime, r => WorldActivator.Create(implType, r));
        }

        public WorldContainerBuilder RegisterType(Type serviceType, Type implType)
        {
            return RegisterType(serviceType, implType, WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return TryRegister(typeof(TService), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
        }

        public WorldContainerBuilder TryRegisterType<TService, TImpl>()
            where TImpl : TService
        {
            return TryRegisterType<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterType(Type serviceType, Type implType, WorldLifetime lifetime)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (implType == null) throw new ArgumentNullException(nameof(implType));
            return TryRegister(serviceType, lifetime, r => WorldActivator.Create(implType, r));
        }

        public WorldContainerBuilder TryRegisterType(Type serviceType, Type implType)
        {
            return TryRegisterType(serviceType, implType, WorldLifetime.Scoped);
        }

        public WorldContainerBuilder RegisterService<TService, TImpl>(WorldLifetime lifetime)
            where TService : class, IService
            where TImpl : class, TService
        {
            return RegisterType<TService, TImpl>(lifetime);
        }

        public WorldContainerBuilder RegisterService<TService, TImpl>()
            where TService : class, IService
            where TImpl : class, TService
        {
            return RegisterService<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterService<TService, TImpl>(WorldLifetime lifetime)
            where TService : class, IService
            where TImpl : class, TService
        {
            return TryRegisterType<TService, TImpl>(lifetime);
        }

        public WorldContainerBuilder TryRegisterService<TService, TImpl>()
            where TService : class, IService
            where TImpl : class, TService
        {
            return TryRegisterService<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder RegisterServiceAlias<TService, TImpl>(WorldLifetime lifetime)
            where TService : class, IService
            where TImpl : class, TService
        {
            return Register(typeof(TService), lifetime, r => r.Resolve<TImpl>());
        }

        public WorldContainerBuilder RegisterServiceAlias<TService, TImpl>()
            where TService : class, IService
            where TImpl : class, TService
        {
            return RegisterServiceAlias<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterServiceAlias<TService, TImpl>(WorldLifetime lifetime)
            where TService : class, IService
            where TImpl : class, TService
        {
            return TryRegister(typeof(TService), lifetime, r => r.Resolve<TImpl>());
        }

        public WorldContainerBuilder TryRegisterServiceAlias<TService, TImpl>()
            where TService : class, IService
            where TImpl : class, TService
        {
            return TryRegisterServiceAlias<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder RegisterServiceType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return RegisterType<TService, TImpl>(lifetime);
        }

        public WorldContainerBuilder RegisterServiceType<TService, TImpl>()
            where TImpl : TService
        {
            return RegisterServiceType<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterServiceType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return TryRegisterType<TService, TImpl>(lifetime);
        }

        public WorldContainerBuilder TryRegisterServiceType<TService, TImpl>()
            where TImpl : TService
        {
            return TryRegisterServiceType<TService, TImpl>(WorldLifetime.Scoped);
        }

        public WorldContainer Build()
        {
            return new WorldContainer(_map.Values);
        }
    }
}
