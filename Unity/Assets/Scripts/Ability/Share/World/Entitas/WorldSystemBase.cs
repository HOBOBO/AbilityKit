using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public abstract class WorldSystemBase : global::Entitas.IInitializeSystem, global::Entitas.IExecuteSystem, global::Entitas.ICleanupSystem
    {
        protected WorldSystemBase(global::Contexts contexts, IWorldServices services)
        {
            Contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        protected global::Contexts Contexts { get; }

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

        protected virtual void OnInit() { }

        protected virtual void OnExecute() { }

        protected virtual void OnCleanup() { }
    }
}
