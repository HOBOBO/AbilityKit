using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.Entitas
{
    public sealed class EntitasWorld : IEntitasWorld
    {
        private readonly WorldCreateOptions _options;
        private readonly IEntitasContextsFactory _contextsFactory;
        private WorldContainer _container;
        private WorldScope _scope;
        private bool _initialized;
        private IWorldClock _clock;

        private global::Entitas.IContexts _contexts;

        public EntitasWorld(WorldCreateOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _contextsFactory = _options.EntitasContextsFactory;
            if (_contextsFactory == null) throw new InvalidOperationException("[EntitasWorld] options.EntitasContextsFactory is required.");

            Id = _options.Id;
            WorldType = _options.WorldType;

            _contexts = _contextsFactory.Create();
            if (_contexts == null) throw new InvalidOperationException("[EntitasWorld] EntitasContextsFactory.Create() returned null.");
            Systems = new global::Entitas.Systems();
        }

        public WorldId Id { get; }
        public string WorldType { get; }

        public global::Entitas.IContexts Contexts => _contexts;
        public global::Entitas.Systems Systems { get; }

        public IWorldServices Services => _scope;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var builder = _options.ServiceBuilder ?? WorldServiceContainerFactory.CreateDefaultOnly();

            builder.RegisterInstance<WorldId>(Id);
            builder.RegisterInstance<string>(WorldType);
            builder.RegisterInstance<IWorld>(this);
            builder.RegisterInstance<IEntitasWorld>(this);

            builder.RegisterInstance<global::Entitas.IContexts>(Contexts);
            builder.RegisterInstance<global::Entitas.Systems>(Systems);

            builder.Register<IEntitasWorldContext>(WorldLifetime.Scoped, r => new EntitasWorldContext(Id, WorldType, Contexts, Systems, (IWorldServices)r));
            builder.Register<IWorldContext>(WorldLifetime.Scoped, r => r.Resolve<IEntitasWorldContext>());

            if (_options.Modules != null)
            {
                for (int i = 0; i < _options.Modules.Count; i++)
                {
                    builder.AddModule(_options.Modules[i]);
                }
            }

            _container = builder.Build();
            _scope = _container.CreateScope();

            if (_options.Modules != null)
            {
                for (int i = 0; i < _options.Modules.Count; i++)
                {
                    if (_options.Modules[i] is IEntitasSystemsInstaller installer)
                    {
                        installer.Install(Contexts, Systems, _scope);
                    }
                }
            }

            Systems.Initialize();
            _initialized = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            if (_clock == null)
            {
                _clock = _scope?.Get<IWorldClock>();
            }
            _clock?.Tick(deltaTime);

            Systems.Execute();
            Systems.Cleanup();
        }

        public void Dispose()
        {
            if (_container == null && _scope == null) return;

            try
            {
                Systems.TearDown();
            }
            catch
            {
            }

            _scope?.Dispose();
            _scope = null;

            _container?.Dispose();
            _container = null;

            try { _contextsFactory?.Release(_contexts); }
            catch { }
            _contexts = null;
        }
    }
}
