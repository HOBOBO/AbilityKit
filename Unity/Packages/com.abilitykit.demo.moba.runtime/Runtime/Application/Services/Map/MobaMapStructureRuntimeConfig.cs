using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Gameplay.Structures;

namespace AbilityKit.Demo.Moba.Services.Map
{
    public interface IMobaMapStructureRuntimeConfig : IService
    {
        bool IsPrepared { get; }
        MobaMapStructureResource Resource { get; }
        void Prepare(BattleMapMO map);
    }

    [WorldService(typeof(IMobaMapStructureRuntimeConfig), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaMapStructureRuntimeConfig), WorldLifetime.Scoped)]
    public sealed class MobaMapStructureRuntimeConfig : IMobaMapStructureRuntimeConfig, IDisposable
    {
        public const string ResourcePath = "moba/map_structures.json";
        private readonly ITextAssetLoader _assets;

        public MobaMapStructureRuntimeConfig(ITextAssetLoader assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public bool IsPrepared { get; private set; }
        public MobaMapStructureResource Resource { get; private set; }

        public void Prepare(BattleMapMO map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            Dispose();
            if (!_assets.TryLoadText(ResourcePath, out var json) || string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException($"Published map structure resource was not found. path={ResourcePath}");
            var resource = MobaMapStructureResourceLoader.LoadResource(json);
            if (resource.MapId != map.Id)
                throw new InvalidOperationException($"Map structure resource map mismatch. mapId={map.Id}, resourceMapId={resource.MapId}");
            for (var i = 0; i < resource.Structures.Count; i++)
            {
                var p = resource.Structures[i].Position;
                var point = new AbilityKit.Core.Mathematics.Vec3(p?.X ?? 0f, p?.Y ?? 0f, p?.Z ?? 0f);
                if (point.X < map.Bounds.Center.X - map.Bounds.Size.X * 0.5f ||
                    point.X > map.Bounds.Center.X + map.Bounds.Size.X * 0.5f ||
                    point.Z < map.Bounds.Center.Z - map.Bounds.Size.Z * 0.5f ||
                    point.Z > map.Bounds.Center.Z + map.Bounds.Size.Z * 0.5f)
                    throw new InvalidOperationException($"Map structure is outside map bounds. id={resource.Structures[i].Id}");
            }
            Resource = resource;
            IsPrepared = true;
        }

        public void Dispose()
        {
            IsPrepared = false;
            Resource = null;
        }
    }
}
