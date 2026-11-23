using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(MobaGameplayConfigService), WorldLifetime.Scoped)]
    public sealed class MobaGameplayConfigService : IService
    {
        [WorldInject(required: false)] private MobaConfigDatabase _configs = null;
        [WorldInject(required: false)] private MobaGameplayConfigSettings _settings = null;
        [WorldInject(required: false)] private IWorldResolver _services = null;

        public int ResolveDefaultGameplayId()
        {
            if (_settings == null)
            {
                throw new System.InvalidOperationException("MobaGameplayConfigSettings is required to resolve default gameplay id.");
            }

            return _settings.ResolveDefaultGameplayId();
        }

        public GameplayMO GetDefaultGameplay()
        {
            return GetGameplay(ResolveDefaultGameplayId());
        }

        public GameplayMO GetGameplay(int gameplayId)
        {
            if (TryGetGameplay(gameplayId, out var gameplay))
            {
                return gameplay;
            }

            Log.Warning($"[MobaGameplayConfigService] gameplay config not found. gameplayId={gameplayId}");
            return null;
        }

        public bool TryGetGameplay(int gameplayId, out GameplayMO gameplay)
        {
            gameplay = null;
            if (gameplayId <= 0)
            {
                return false;
            }

            var configs = ResolveConfigs();
            return configs != null && configs.TryGetGameplay(gameplayId, out gameplay);
        }

        private MobaConfigDatabase ResolveConfigs()
        {
            if (_configs != null)
            {
                return _configs;
            }

            if (_services != null && _services.TryResolve<MobaConfigDatabase>(out var configs) && configs != null)
            {
                _configs = configs;
            }

            return _configs;
        }

        public void Dispose()
        {
        }
    }

    [WorldService(typeof(MobaGameplayConfigSettings), WorldLifetime.Scoped)]
    public sealed class MobaGameplayConfigSettings : IService
    {
        public int DefaultGameplayId { get; set; }

        public int ResolveDefaultGameplayId()
        {
            if (DefaultGameplayId <= 0)
            {
                throw new System.InvalidOperationException($"DefaultGameplayId must be configured with a positive value. DefaultGameplayId={DefaultGameplayId}");
            }

            return DefaultGameplayId;
        }

        public void Dispose()
        {
        }
    }
}
