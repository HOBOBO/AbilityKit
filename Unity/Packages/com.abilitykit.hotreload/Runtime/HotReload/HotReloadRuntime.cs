using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.HotReload
{
    public static class HotReloadRuntime
    {
        private sealed class WorldState
        {
            public HotfixSystemProxy Proxy;
            public HotfixServiceOverlay Overlay;
            public IHotfixEntry CurrentEntry;
            public global::Entitas.Systems CurrentFeature;
        }

        private static readonly Dictionary<string, WorldState> States = new Dictionary<string, WorldState>(StringComparer.Ordinal);

        public static bool Apply(IEntitasWorld world, IHotfixEntry entry, out string error)
        {
            error = null;
            if (world == null)
            {
                error = "world is null";
                return false;
            }
            if (entry == null)
            {
                error = "entry is null";
                return false;
            }

            try { HotReloadStaticRegistry.ResetAll(); }
            catch { }

            var key = world.Id.Value ?? string.Empty;
            if (!States.TryGetValue(key, out var state) || state == null)
            {
                state = new WorldState();
                States[key] = state;
            }

            EnsureProxyAndOverlay(world, state);

            var contexts = world.Contexts;
            var systems = world.Systems;
            var services = world.Services;

            // Uninstall previous hotfix
            if (state.CurrentEntry != null)
            {
                try { state.CurrentEntry.Uninstall(contexts, systems, state.Overlay); }
                catch { }
            }

            // Build new hotfix feature and let entry install into it
            var feature = new global::Entitas.Systems();
            try
            {
                entry.Install(contexts, feature, state.Overlay);
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }

            state.Proxy.Swap(feature);

            state.CurrentEntry = entry;
            state.CurrentFeature = feature;
            return true;
        }

        private static void EnsureProxyAndOverlay(IEntitasWorld world, WorldState state)
        {
            if (state.Overlay == null)
            {
                state.Overlay = new HotfixServiceOverlay(world.Services);
            }

            if (state.Proxy == null)
            {
                // Insert a proxy system once; it will execute whatever feature is currently swapped in.
                state.Proxy = new HotfixSystemProxy(world.Contexts, state.Overlay);
                world.Systems.Add(state.Proxy);
                try { state.Proxy.Initialize(); }
                catch { }
            }
        }
    }
}
