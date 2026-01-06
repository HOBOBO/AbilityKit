using System;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.World.Entitas
{
    public sealed class EntitasWorld : IEntitasWorld
    {
        private readonly WorldCreateOptions _options;
        private WorldContainer _container;
        private WorldScope _scope;
        private bool _initialized;
        private IWorldClock _clock;
        private ITriggerActionRunner _triggerActions;

        public EntitasWorld(WorldCreateOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            Id = _options.Id;
            WorldType = _options.WorldType;

            Contexts = new global::Contexts();
            Systems = new global::Feature(WorldType);
        }

        public WorldId Id { get; }
        public string WorldType { get; }

        public global::Contexts Contexts { get; }
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

            builder.RegisterInstance<global::Contexts>(Contexts);
            builder.RegisterInstance<global::Entitas.Systems>(Systems);

            builder.Register<IEntitasWorldContext>(WorldLifetime.Scoped, r => new EntitasWorldContext(Id, WorldType, Contexts, Systems, (IWorldServices)r));
            builder.Register<IWorldContext>(WorldLifetime.Scoped, r => r.Resolve<IEntitasWorldContext>());

            builder.AddModule(new EntitasEcsWorldModule());

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

            if (_triggerActions == null)
            {
                _triggerActions = _scope?.Get<ITriggerActionRunner>();
            }
            _triggerActions?.Tick(deltaTime);

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

            try
            {
                Contexts.Reset();
            }
            catch
            {
            }
        }
    }
}
