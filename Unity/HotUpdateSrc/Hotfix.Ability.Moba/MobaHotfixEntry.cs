using AbilityKit.Ability.HotReload;
using AbilityKit.Ability.World.DI;

namespace Hotfix.Ability.Moba
{
    public sealed class MobaHotfixEntry : IHotfixEntry
    {
        public string Name => "moba";

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            var logger = services.TryGet<IHotfixLogger>(out var l) ? l : null;
            logger?.Log("[Hotfix] Install called");

            if (services is HotfixServiceOverlay overlay)
            {
                overlay.Set(typeof(AbilityKit.Ability.Share.Impl.Moba.Services.IMobaLobbyInputHotfixRouter), new MobaLobbyInputHotfixRouter_Example());
                logger?.Log("[Hotfix] IMobaLobbyInputHotfixRouter installed");
            }

            systems.Add(new HotfixLogTickSystem(logger));
        }

        public void Uninstall(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            var logger = services.TryGet<IHotfixLogger>(out var l) ? l : null;
            logger?.Log("[Hotfix] Uninstall called");

            if (services is HotfixServiceOverlay overlay)
            {
                overlay.Set(typeof(AbilityKit.Ability.Share.Impl.Moba.Services.IMobaLobbyInputHotfixRouter), null);
                logger?.Log("[Hotfix] IMobaLobbyInputHotfixRouter uninstalled");
            }
        }

        private sealed class HotfixLogTickSystem : global::Entitas.IExecuteSystem
        {
            private readonly IHotfixLogger _logger;
            private int _tick;

            public HotfixLogTickSystem(IHotfixLogger logger)
            {
                _logger = logger;
                _tick = 0;
            }

            public void Execute()
            {
                _tick++;
                if (_tick % 60 == 0)
                {
                    _logger?.Log($"[Hotfix] Tick={_tick}");
                }
            }
        }
    }
}
