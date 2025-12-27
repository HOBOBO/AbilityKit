using System;
using System.Collections.Generic;

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
            _map[serviceType] = new WorldServiceDescriptor(serviceType, lifetime, factory);
            return this;
        }

        public WorldContainerBuilder TryRegister(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            if (!_map.ContainsKey(serviceType))
            {
                _map[serviceType] = new WorldServiceDescriptor(serviceType, lifetime, factory);
            }
            return this;
        }

        public WorldContainerBuilder Register<TService>(WorldLifetime lifetime, Func<IWorldResolver, TService> factory)
        {
            return Register(typeof(TService), lifetime, r => factory(r));
        }

        public WorldContainerBuilder TryRegister<TService>(WorldLifetime lifetime, Func<IWorldResolver, TService> factory)
        {
            return TryRegister(typeof(TService), lifetime, r => factory(r));
        }

        public WorldContainerBuilder RegisterInstance<TService>(TService instance)
        {
            return Register(typeof(TService), WorldLifetime.Singleton, _ => instance);
        }

        public WorldContainerBuilder RegisterType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return Register(typeof(TService), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
        }

        public WorldContainerBuilder TryRegisterType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return TryRegister(typeof(TService), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
        }

        public WorldContainer Build()
        {
            return new WorldContainer(_map.Values);
        }
    }
}
