using System;

namespace AbilityKit.Ability.World.Entitas
{
    [Obsolete("EntitasContextsPool is schema-specific and is no longer used by world.di. Use WorldCreateOptions.EntitasContextsFactory instead.")]
    public static class EntitasContextsPool
    {
        public static int MaxSize
        {
            get => 0;
            set { }
        }

        public static global::Entitas.IContexts Rent()
        {
            throw new NotSupportedException("EntitasContextsPool is obsolete. Use WorldCreateOptions.EntitasContextsFactory.");
        }

        public static void Return(global::Entitas.IContexts contexts)
        {
        }

        public static void Clear(Action<global::Entitas.IContexts> onRemove = null)
        {
        }
    }
}
