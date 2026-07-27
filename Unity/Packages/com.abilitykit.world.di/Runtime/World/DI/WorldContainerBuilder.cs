using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldContainerBuilder
    {
        private readonly Dictionary<Type, WorldServiceDescriptor> _map = new Dictionary<Type, WorldServiceDescriptor>();
        private readonly Dictionary<Type, Type> _registrationSources = new Dictionary<Type, Type>();
        private readonly List<WorldServiceRegistration> _registrations = new List<WorldServiceRegistration>();
        private Type _currentSourceModuleType;

        public IReadOnlyList<WorldServiceRegistration> Registrations => _registrations;

        public WorldContainerBuilder AddModule(IWorldModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));

            var previousSource = _currentSourceModuleType;
            _currentSourceModuleType = module.GetType();
            try
            {
                module.Configure(this);
            }
            finally
            {
                _currentSourceModuleType = previousSource;
            }

            return this;
        }

        public WorldContainerBuilder Register(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            return Register(
                serviceType,
                serviceType,
                lifetime,
                factory,
                WorldServiceRegistrationPolicy.Replace);
        }

        public WorldContainerBuilder Register(Type serviceType, Type implType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            return Register(
                serviceType,
                implType,
                lifetime,
                factory,
                WorldServiceRegistrationPolicy.Replace);
        }

        public WorldContainerBuilder Register(
            Type serviceType,
            Type implType,
            WorldLifetime lifetime,
            Func<IWorldResolver, object> factory,
            WorldServiceRegistrationPolicy policy)
        {
            return RegisterCore(
                serviceType,
                implType,
                lifetime,
                factory,
                WorldServiceOwnership.Container,
                policy);
        }

        public WorldContainerBuilder TryRegister(Type serviceType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            return Register(
                serviceType,
                serviceType,
                lifetime,
                factory,
                WorldServiceRegistrationPolicy.KeepExisting);
        }

        public WorldContainerBuilder TryRegister(Type serviceType, Type implType, WorldLifetime lifetime, Func<IWorldResolver, object> factory)
        {
            return Register(
                serviceType,
                implType,
                lifetime,
                factory,
                WorldServiceRegistrationPolicy.KeepExisting);
        }

        public WorldContainerBuilder Register<TService>(WorldLifetime lifetime, Func<IWorldResolver, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Register(typeof(TService), lifetime, r => factory(r));
        }

        public WorldContainerBuilder Register<TService>(
            WorldLifetime lifetime,
            Func<IWorldResolver, TService> factory,
            WorldServiceRegistrationPolicy policy)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Register(typeof(TService), typeof(TService), lifetime, r => factory(r), policy);
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

        public WorldContainerBuilder RegisterExternalInstance(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!serviceType.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"External instance type '{instance.GetType().FullName}' is not assignable to service type '{serviceType.FullName}'.",
                    nameof(instance));
            }

            return RegisterCore(
                serviceType,
                instance.GetType(),
                WorldLifetime.Singleton,
                _ => instance,
                WorldServiceOwnership.External,
                WorldServiceRegistrationPolicy.Replace);
        }

        public WorldContainerBuilder RegisterExternalInstance<TService>(TService instance)
        {
            return RegisterExternalInstance(typeof(TService), instance);
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
            return Register(typeof(TService), typeof(TImpl), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
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
            return Register(serviceType, implType, lifetime, r => WorldActivator.Create(implType, r));
        }

        public WorldContainerBuilder RegisterType(Type serviceType, Type implType)
        {
            return RegisterType(serviceType, implType, WorldLifetime.Scoped);
        }

        public WorldContainerBuilder TryRegisterType<TService, TImpl>(WorldLifetime lifetime)
            where TImpl : TService
        {
            return TryRegister(typeof(TService), typeof(TImpl), lifetime, r => WorldActivator.Create(typeof(TImpl), r));
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
            return TryRegister(serviceType, implType, lifetime, r => WorldActivator.Create(implType, r));
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

        private WorldContainerBuilder RegisterCore(
            Type serviceType,
            Type implType,
            WorldLifetime lifetime,
            Func<IWorldResolver, object> factory,
            WorldServiceOwnership ownership,
            WorldServiceRegistrationPolicy policy)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (implType == null) throw new ArgumentNullException(nameof(implType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _map.TryGetValue(serviceType, out var previous);
            _registrationSources.TryGetValue(serviceType, out var previousSource);

            var outcome = WorldServiceRegistrationOutcome.Added;
            if (previous != null)
            {
                switch (policy)
                {
                    case WorldServiceRegistrationPolicy.Replace:
                        outcome = WorldServiceRegistrationOutcome.Replaced;
                        break;
                    case WorldServiceRegistrationPolicy.KeepExisting:
                        outcome = WorldServiceRegistrationOutcome.KeptExisting;
                        break;
                    case WorldServiceRegistrationPolicy.Reject:
                        outcome = WorldServiceRegistrationOutcome.Rejected;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
                }
            }

            _registrations.Add(new WorldServiceRegistration(
                _registrations.Count,
                serviceType,
                implType,
                lifetime,
                ownership,
                policy,
                outcome,
                _currentSourceModuleType,
                previous?.ImplType,
                previous?.Ownership,
                previousSource));

            if (outcome == WorldServiceRegistrationOutcome.Rejected)
            {
                throw new InvalidOperationException(
                    $"World service registration rejected: service={serviceType.FullName}, " +
                    $"implementation={implType.FullName}, source={FormatSource(_currentSourceModuleType)}, " +
                    $"existingImplementation={previous.ImplType.FullName}, existingSource={FormatSource(previousSource)}");
            }

            if (outcome != WorldServiceRegistrationOutcome.KeptExisting)
            {
                _map[serviceType] = new WorldServiceDescriptor(
                    serviceType,
                    implType,
                    lifetime,
                    factory,
                    ownership);
                _registrationSources[serviceType] = _currentSourceModuleType;
            }

            return this;
        }

        private static string FormatSource(Type sourceModuleType)
        {
            return sourceModuleType?.FullName ?? "<root>";
        }
    }
}
