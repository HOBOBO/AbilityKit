using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public enum MapCollisionShapeType
    {
        Box = 0,
        Sphere = 1,
        Capsule = 2,
    }

    public enum MapProjectileResponse
    {
        Default = 0,
        Block = 1,
        Ignore = 2,
        HitOnly = 3,
        Pierce = 4,
    }

    public sealed class BattleMapMO
    {
        public int Id { get; }
        public string Name { get; }
        public MapBoundsMO Bounds { get; }
        public IReadOnlyList<MapWalkableAreaMO> WalkableAreas { get; }
        public IReadOnlyList<MapSpawnPointMO> SpawnPoints { get; }
        public IReadOnlyList<MapCollisionObjectMO> CollisionObjects { get; }

        public BattleMapMO(BattleMapDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.Id <= 0) throw new ArgumentOutOfRangeException(nameof(dto.Id), "Map id must be positive.");

            Id = dto.Id;
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"Map_{dto.Id}" : dto.Name;
            Bounds = new MapBoundsMO(dto.Bounds);
            WalkableAreas = dto.WalkableAreas == null || dto.WalkableAreas.Length == 0
                ? new[] { MapWalkableAreaMO.FromBounds(dto.Id, Bounds) }
                : Convert(dto.WalkableAreas, item => new MapWalkableAreaMO(item));
            SpawnPoints = Convert(dto.SpawnPoints, item => new MapSpawnPointMO(item));
            CollisionObjects = Convert(dto.CollisionObjects, item => new MapCollisionObjectMO(item));
        }

        private static IReadOnlyList<TOutput> Convert<TInput, TOutput>(TInput[] source, Func<TInput, TOutput> converter)
        {
            if (source == null || source.Length == 0) return Array.Empty<TOutput>();

            var result = new TOutput[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = converter(source[i]);
            }

            return result;
        }
    }

    public sealed class MapBoundsMO
    {
        public Vec3 Center { get; }
        public Vec3 Size { get; }

        public MapBoundsMO(MapBoundsDTO dto)
        {
            Center = MapConfigValue.ToVec3(dto?.Center);
            Size = MapConfigValue.ToVec3(dto?.Size);
        }
    }

    public sealed class MapWalkableAreaMO
    {
        public int Id { get; }
        public string Name { get; }
        public Vec3 Center { get; }
        public Vec3 Size { get; }

        public MapWalkableAreaMO(MapWalkableAreaDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"WalkableArea_{dto.Id}" : dto.Name;
            Center = MapConfigValue.ToVec3(dto.Center);
            Size = MapConfigValue.ToVec3(dto.Size);
        }

        private MapWalkableAreaMO(int id, string name, Vec3 center, Vec3 size)
        {
            Id = id;
            Name = name;
            Center = center;
            Size = size;
        }

        internal static MapWalkableAreaMO FromBounds(int mapId, MapBoundsMO bounds)
        {
            return new MapWalkableAreaMO(mapId, "Map Bounds", bounds.Center, bounds.Size);
        }
    }

    public sealed class MapSpawnPointMO
    {
        public int Id { get; }
        public int TeamId { get; }
        public Vec3 Position { get; }
        public float YawDegrees { get; }

        public MapSpawnPointMO(MapSpawnPointDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            TeamId = dto.TeamId;
            Position = MapConfigValue.ToVec3(dto.Position);
            YawDegrees = dto.YawDegrees;
        }
    }

    public sealed class MapCollisionObjectMO
    {
        public int Id { get; }
        public string Name { get; }
        public string ObjectType { get; }
        public MapCollisionShapeType ShapeType { get; }
        public Vec3 Position { get; }
        public Vec3 RotationEuler { get; }
        public Vec3 Size { get; }
        public float Radius { get; }
        public float Height { get; }
        public int CollisionLayer { get; }
        public bool BlocksMovement { get; }
        public MapProjectileResponse ProjectileResponse { get; }
        public bool GenerateView { get; }

        public MapCollisionObjectMO(MapCollisionObjectDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.Id <= 0) throw new ArgumentOutOfRangeException(nameof(dto.Id), "Map object id must be positive.");

            Id = dto.Id;
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"MapObject_{dto.Id}" : dto.Name;
            ObjectType = string.IsNullOrWhiteSpace(dto.ObjectType) ? "StaticObstacle" : dto.ObjectType;
            ShapeType = MapConfigValue.ParseEnum(dto.ShapeType, MapCollisionShapeType.Box);
            Position = MapConfigValue.ToVec3(dto.Position);
            RotationEuler = MapConfigValue.ToVec3(dto.RotationEuler);
            Size = MapConfigValue.ToVec3(dto.Size);
            Radius = dto.Radius;
            Height = dto.Height;
            CollisionLayer = dto.CollisionLayer;
            BlocksMovement = dto.BlocksMovement;
            ProjectileResponse = MapConfigValue.ParseEnum(dto.ProjectileResponse, MapProjectileResponse.Default);
            GenerateView = dto.GenerateView;
        }
    }

    internal static class MapConfigValue
    {
        public static Vec3 ToVec3(MapVector3DTO value)
        {
            return value == null ? default : new Vec3(value.X, value.Y, value.Z);
        }

        public static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out T parsed)
                ? parsed
                : fallback;
        }
    }
}
