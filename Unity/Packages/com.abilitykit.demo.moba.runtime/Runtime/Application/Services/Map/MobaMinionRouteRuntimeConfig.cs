using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Minions;

namespace AbilityKit.Demo.Moba.Services.Map
{
    public interface IMobaMinionRouteRuntimeConfig : IService
    {
        bool IsPrepared { get; }
        int MapId { get; }
        MobaMinionRouteResource Resource { get; }
        MobaMinionRouteDefinition DefaultRoute { get; }
        void Prepare(BattleMapMO map);
    }

    /// <summary>
    /// Loads and validates the published lane configuration at map bootstrap.
    /// Gameplay systems consume this snapshot instead of reading text assets on
    /// their first tick.
    /// </summary>
    [WorldService(typeof(IMobaMinionRouteRuntimeConfig), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaMinionRouteRuntimeConfig), WorldLifetime.Scoped)]
    public sealed class MobaMinionRouteRuntimeConfig : IMobaMinionRouteRuntimeConfig, IDisposable
    {
        public const string ResourcePath = "moba/minion_routes.json";

        private readonly ITextAssetLoader _textAssetLoader;

        public MobaMinionRouteRuntimeConfig(ITextAssetLoader textAssetLoader)
        {
            _textAssetLoader = textAssetLoader ?? throw new ArgumentNullException(nameof(textAssetLoader));
        }

        public bool IsPrepared { get; private set; }
        public int MapId { get; private set; }
        public MobaMinionRouteResource Resource { get; private set; }
        public MobaMinionRouteDefinition DefaultRoute { get; private set; }

        public void Prepare(BattleMapMO map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            Dispose();
            if (map.Id <= 0)
                throw new InvalidOperationException($"Cannot prepare minion route config for invalid map. mapId={map.Id}");

            if (!_textAssetLoader.TryLoadText(ResourcePath, out var json) || string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"Published minion route resource was not found. mapId={map.Id}, path={ResourcePath}");
            }

            PrepareFromJson(map, json);
        }

        private void PrepareFromJson(BattleMapMO map, string json)
        {
            IsPrepared = false;
            MapId = 0;
            Resource = null;
            DefaultRoute = null;

            MobaMinionRouteResource resource;
            try
            {
                resource = MobaMinionRouteResourceLoader.LoadResource(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Published minion route resource is invalid. mapId={map.Id}, path={ResourcePath}", ex);
            }

            try
            {
                MobaMinionRouteMapValidator.ValidateResource(resource, map);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Published minion route resource failed map validation. mapId={map.Id}, path={ResourcePath}, resourceMapId={resource.MapId}, defaultRouteId={resource.DefaultRouteId}", ex);
            }

            MobaMinionRouteDefinition defaultRoute = null;
            for (var i = 0; i < resource.Routes.Count; i++)
            {
                if (resource.Routes[i].RouteId != resource.DefaultRouteId) continue;
                defaultRoute = resource.Routes[i];
                break;
            }

            if (defaultRoute == null)
            {
                throw new InvalidOperationException(
                    $"Published minion route resource default route was not found. mapId={map.Id}, path={ResourcePath}, routeId={resource.DefaultRouteId}");
            }

            Resource = resource;
            DefaultRoute = defaultRoute;
            MapId = map.Id;
            IsPrepared = true;
        }

        public void Dispose()
        {
            IsPrepared = false;
            MapId = 0;
            Resource = null;
            DefaultRoute = null;
        }
    }
}
