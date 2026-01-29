using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.HotReload
{
    public sealed class HotfixSystemProxy : global::Entitas.IInitializeSystem, global::Entitas.IExecuteSystem, global::Entitas.ICleanupSystem, global::Entitas.ITearDownSystem
    {
        private readonly global::Entitas.IContexts _contexts;
        private readonly IWorldServices _services;

        private global::Entitas.Systems _current;

        public HotfixSystemProxy(global::Entitas.IContexts contexts, IWorldServices services)
        {
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public global::Entitas.Systems Current => _current;

        public void Swap(global::Entitas.Systems next)
        {
            if (_current != null)
            {
                try { _current.TearDown(); }
                catch { }
            }

            _current = next;

            if (_current != null)
            {
                try { _current.Initialize(); }
                catch { }
            }
        }

        public void Initialize()
        {
            _current?.Initialize();
        }

        public void Execute()
        {
            _current?.Execute();
        }

        public void Cleanup()
        {
            _current?.Cleanup();
        }

        public void TearDown()
        {
            _current?.TearDown();
        }

        public global::Entitas.Systems CreateHotfixFeature(string name)
        {
            return new global::Entitas.Systems();
        }

        public object CreateSystemInstance(Type systemType)
        {
            return Activator.CreateInstance(systemType, _contexts, _services);
        }
    }
}
