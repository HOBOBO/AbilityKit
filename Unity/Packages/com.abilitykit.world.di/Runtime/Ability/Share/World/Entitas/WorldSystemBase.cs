using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public abstract class WorldSystemBase : global::Entitas.IInitializeSystem, global::Entitas.IExecuteSystem, global::Entitas.ICleanupSystem, global::Entitas.ITearDownSystem
    {
        protected WorldSystemBase(global::Entitas.IContexts contexts, IWorldServices services)
        {
            Contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        protected global::Entitas.IContexts Contexts { get; }

        protected IWorldServices Services { get; }

        protected bool Enabled { get; set; } = true;

        public void Initialize()
        {
            if (!Enabled) return;
            OnInit();
        }

        public void Execute()
        {
            if (!Enabled) return;
            OnExecute();
        }

        public void Cleanup()
        {
            if (!Enabled) return;
            OnCleanup();
        }

        public void TearDown()
        {
            if (!Enabled) return;
            OnTearDown();
        }

        protected virtual void OnInit() { }

        protected virtual void OnExecute() { }

        protected virtual void OnCleanup() { }

        protected virtual void OnTearDown() { }
    }
}
