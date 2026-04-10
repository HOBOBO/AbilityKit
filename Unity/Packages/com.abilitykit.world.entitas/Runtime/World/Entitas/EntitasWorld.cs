using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Log;

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
            _contextsFactory = _options.GetEntitasContextsFactoryOrThrow();

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

        public IWorldResolver Services => _scope;

        internal void SetComposition(WorldContainer container, WorldScope scope)
        {
            _container = container;
            _scope = scope;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                EntitasWorldComposer.Compose(this, _options);
            }
            catch
            {
                try { _scope?.Dispose(); }
                catch (Exception ex) { Log.Exception(ex); }
                _scope = null;

                try { _container?.Dispose(); }
                catch (Exception ex) { Log.Exception(ex); }
                _container = null;

                _initialized = false;
                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            Systems.Execute();
            Systems.Cleanup();

            if (_clock == null)
            {
                _clock = _scope?.Resolve<IWorldClock>();
            }
            _clock?.Tick(deltaTime);
        }

        public void Dispose()
        {
            if (_container == null && _scope == null) return;

            try
            {
                Systems.TearDown();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }

            _scope?.Dispose();
            _scope = null;

            _container?.Dispose();
            _container = null;

            try { _contextsFactory?.Release(_contexts); }
            catch (Exception ex) { Log.Exception(ex); }
            _contexts = null;
        }
    }
}
