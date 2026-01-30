using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Entitas
{
    public static class WorldCreateOptionsEntitasExtensions
    {
        private sealed class EntitasContextsFactoryBox
        {
            public IEntitasContextsFactory Factory;
        }

        public static WorldCreateOptions SetEntitasContextsFactory(this WorldCreateOptions options, IEntitasContextsFactory factory)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            options.Extensions[typeof(EntitasContextsFactoryBox)] = new EntitasContextsFactoryBox { Factory = factory };
            return options;
        }

        public static bool TryGetEntitasContextsFactory(this WorldCreateOptions options, out IEntitasContextsFactory factory)
        {
            factory = null;
            if (options == null) return false;

            if (options.Extensions.TryGetValue(typeof(EntitasContextsFactoryBox), out var boxed) && boxed is EntitasContextsFactoryBox box && box.Factory != null)
            {
                factory = box.Factory;
                return true;
            }

            return false;
        }

        public static IEntitasContextsFactory GetEntitasContextsFactoryOrThrow(this WorldCreateOptions options)
        {
            if (TryGetEntitasContextsFactory(options, out var factory) && factory != null) return factory;
            throw new InvalidOperationException("[EntitasWorld] EntitasContextsFactory is required. Set it via WorldCreateOptions.SetEntitasContextsFactory(...).");
        }
    }
}
