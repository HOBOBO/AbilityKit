using System;

namespace AbilityKit.Ability.World.Entitas
{
    public interface IEntitasContextsFactory
    {
        global::Entitas.IContexts Create();

        void Release(global::Entitas.IContexts contexts);
    }
}
