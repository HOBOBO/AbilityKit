using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Services.Map
{
    public interface IMobaMapRuntimeService : IService
    {
        BattleMapMO CurrentMap { get; }
        bool IsLoaded { get; }
        void Load(int mapId);
        void Unload();
        bool IsPositionWalkable(in Vec3 position, float radius = 0f);
        bool TryProjectToWalkable(in Vec3 position, float radius, out Vec3 projectedPosition);
        bool TryGetMapObject(ColliderId colliderId, out MapCollisionObjectMO mapObject);
        bool TryGetColliderId(int mapObjectId, out ColliderId colliderId);
        bool TryGetSpawnPointById(int spawnPointId, out MapSpawnPointMO spawnPoint);
        bool TryGetTeamSpawnPoint(int teamId, int spawnIndex, out MapSpawnPointMO spawnPoint);
    }

    [WorldService(typeof(IMobaMapRuntimeService), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaMapRuntimeService), WorldLifetime.Scoped)]
    public sealed class MobaMapRuntimeService : IMobaMapRuntimeService, IWorldDeinitializable
    {
        private const float DegreesToRadians = (float)(Math.PI / 180.0);
        private const float RotationEpsilon = 0.0001f;
        private const float SpawnRadius = 0.5f;

        private readonly MobaConfigDatabase _configs;
        private readonly ICollisionWorld _collisionWorld;
        private readonly Dictionary<int, ColliderId> _collidersByObjectId = new Dictionary<int, ColliderId>();
        private readonly Dictionary<ColliderId, MapCollisionObjectMO> _objectsByCollider = new Dictionary<ColliderId, MapCollisionObjectMO>();
        private readonly Dictionary<int, MapSpawnPointMO> _spawnPointsById = new Dictionary<int, MapSpawnPointMO>();

        public MobaMapRuntimeService(MobaConfigDatabase configs, ICollisionService collisionService)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _collisionWorld = collisionService?.World ?? throw new ArgumentNullException(nameof(collisionService));
        }

        public BattleMapMO CurrentMap { get; private set; }
        public bool IsLoaded => CurrentMap != null;

        public void Load(int mapId)
        {
            if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId), "Map id must be positive.");
            if (CurrentMap != null && CurrentMap.Id == mapId) return;
            if (!_configs.TryGetBattleMap(mapId, out var map) || map == null)
            {
                throw new InvalidOperationException($"Battle map config was not found. mapId={mapId}");
            }

            ValidateMap(map);
            Unload();

            try
            {
                RegisterSpawnPoints(map);
                RegisterCollisionObjects(map);
                CurrentMap = map;
            }
            catch
            {
                Unload();
                throw;
            }
        }

        public void Unload()
        {
            foreach (var pair in _collidersByObjectId)
            {
                _collisionWorld.Remove(pair.Value);
            }

            _collidersByObjectId.Clear();
            _objectsByCollider.Clear();
            _spawnPointsById.Clear();
            CurrentMap = null;
        }

        public bool IsPositionWalkable(in Vec3 position, float radius = 0f)
        {
            if (CurrentMap == null || radius < 0f) return false;

            var areas = CurrentMap.WalkableAreas;
            for (int i = 0; i < areas.Count; i++)
            {
                if (ContainsXZ(areas[i], in position, radius)) return true;
            }

            return false;
        }

        public bool TryProjectToWalkable(in Vec3 position, float radius, out Vec3 projectedPosition)
        {
            projectedPosition = position;
            if (CurrentMap == null || radius < 0f) return false;

            var found = false;
            var bestDistanceSquared = float.MaxValue;
            var areas = CurrentMap.WalkableAreas;
            for (int i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                var halfX = area.Size.X * 0.5f - radius;
                var halfZ = area.Size.Z * 0.5f - radius;
                if (halfX < 0f || halfZ < 0f) continue;

                var x = Clamp(position.X, area.Center.X - halfX, area.Center.X + halfX);
                var z = Clamp(position.Z, area.Center.Z - halfZ, area.Center.Z + halfZ);
                var dx = x - position.X;
                var dz = z - position.Z;
                var distanceSquared = dx * dx + dz * dz;
                if (found && distanceSquared >= bestDistanceSquared) continue;

                found = true;
                bestDistanceSquared = distanceSquared;
                projectedPosition = new Vec3(x, position.Y, z);
            }

            return found;
        }

        public bool TryGetMapObject(ColliderId colliderId, out MapCollisionObjectMO mapObject)
        {
            return _objectsByCollider.TryGetValue(colliderId, out mapObject);
        }

        public bool TryGetColliderId(int mapObjectId, out ColliderId colliderId)
        {
            return _collidersByObjectId.TryGetValue(mapObjectId, out colliderId);
        }

        public bool TryGetSpawnPointById(int spawnPointId, out MapSpawnPointMO spawnPoint)
        {
            return _spawnPointsById.TryGetValue(spawnPointId, out spawnPoint);
        }

        public bool TryGetTeamSpawnPoint(int teamId, int spawnIndex, out MapSpawnPointMO spawnPoint)
        {
            spawnPoint = null;
            if (CurrentMap == null || teamId <= 0 || spawnIndex < 0) return false;

            int currentIndex = 0;
            var spawnPoints = CurrentMap.SpawnPoints;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                var candidate = spawnPoints[i];
                if (candidate.TeamId != teamId) continue;
                if (currentIndex++ != spawnIndex) continue;

                spawnPoint = candidate;
                return true;
            }

            return false;
        }

        public void OnDeinit(IWorldResolver services)
        {
            Unload();
        }

        public void Dispose()
        {
            Unload();
        }

        private void RegisterSpawnPoints(BattleMapMO map)
        {
            for (int i = 0; i < map.SpawnPoints.Count; i++)
            {
                var spawnPoint = map.SpawnPoints[i];
                _spawnPointsById.Add(spawnPoint.Id, spawnPoint);
            }
        }

        private void RegisterCollisionObjects(BattleMapMO map)
        {
            for (int i = 0; i < map.CollisionObjects.Count; i++)
            {
                var mapObject = map.CollisionObjects[i];
                var rotation = CreateYawRotation(mapObject.RotationEuler.Y);
                var transform = new Transform3(mapObject.Position, rotation, Vec3.One);
                var shape = CreateShape(mapObject);
                var colliderId = _collisionWorld.Add(in transform, in shape, mapObject.CollisionLayer);
                _collidersByObjectId.Add(mapObject.Id, colliderId);
                _objectsByCollider.Add(colliderId, mapObject);
            }
        }

        private static ColliderShape CreateShape(MapCollisionObjectMO mapObject)
        {
            switch (mapObject.ShapeType)
            {
                case MapCollisionShapeType.Box:
                    var halfExtents = mapObject.Size * 0.5f;
                    return ColliderShape.CreateObb(Vec3.Zero, Quat.Identity, halfExtents);

                case MapCollisionShapeType.Sphere:
                    return ColliderShape.CreateSphere(Vec3.Zero, mapObject.Radius);

                case MapCollisionShapeType.Capsule:
                    var segmentHalfHeight = mapObject.Height * 0.5f - mapObject.Radius;
                    var a = new Vec3(0f, -segmentHalfHeight, 0f);
                    var b = new Vec3(0f, segmentHalfHeight, 0f);
                    return ColliderShape.CreateCapsule(a, b, mapObject.Radius);

                default:
                    throw new InvalidOperationException($"Unsupported map collision shape. objectId={mapObject.Id}, shape={mapObject.ShapeType}");
            }
        }

        private static Quat CreateYawRotation(float yawDegrees)
        {
            return Quat.FromAxisAngle(Vec3.Up, yawDegrees * DegreesToRadians);
        }

        private static void ValidateMap(BattleMapMO map)
        {
            if (map.Bounds.Size.X <= 0f || map.Bounds.Size.Z <= 0f)
            {
                throw new InvalidOperationException($"Battle map bounds must have positive XZ size. mapId={map.Id}, size={map.Bounds.Size}");
            }

            var areaIds = new HashSet<int>();
            for (int i = 0; i < map.WalkableAreas.Count; i++)
            {
                var area = map.WalkableAreas[i];
                if (area.Id <= 0 || !areaIds.Add(area.Id))
                {
                    throw new InvalidOperationException($"Battle map contains an invalid or duplicate walkable area id. mapId={map.Id}, areaId={area.Id}");
                }

                if (area.Size.X <= 0f || area.Size.Z <= 0f)
                {
                    throw new InvalidOperationException($"Battle map walkable area must have positive XZ size. mapId={map.Id}, areaId={area.Id}, size={area.Size}");
                }

                if (!ContainsXZ(map.Bounds.Center, map.Bounds.Size, area.Center, area.Size))
                {
                    throw new InvalidOperationException($"Battle map walkable area must be contained by map bounds. mapId={map.Id}, areaId={area.Id}");
                }
            }

            var objectIds = new HashSet<int>();
            for (int i = 0; i < map.CollisionObjects.Count; i++)
            {
                var mapObject = map.CollisionObjects[i];
                if (!objectIds.Add(mapObject.Id))
                {
                    throw new InvalidOperationException($"Battle map contains duplicate collision object id. mapId={map.Id}, objectId={mapObject.Id}");
                }

                if (mapObject.CollisionLayer < 0 || mapObject.CollisionLayer >= CollisionLayers.MaxLayers)
                {
                    throw new InvalidOperationException($"Battle map collision layer is out of range. mapId={map.Id}, objectId={mapObject.Id}, layer={mapObject.CollisionLayer}");
                }

                if (Math.Abs(mapObject.RotationEuler.X) > RotationEpsilon || Math.Abs(mapObject.RotationEuler.Z) > RotationEpsilon)
                {
                    throw new InvalidOperationException($"Battle map runtime currently supports yaw-only collision rotation. mapId={map.Id}, objectId={mapObject.Id}, rotation={mapObject.RotationEuler}");
                }

                ValidateShape(map.Id, mapObject);
            }

            var spawnPointIds = new HashSet<int>();
            for (int i = 0; i < map.SpawnPoints.Count; i++)
            {
                var spawnPoint = map.SpawnPoints[i];
                if (spawnPoint.Id <= 0 || !spawnPointIds.Add(spawnPoint.Id))
                {
                    throw new InvalidOperationException($"Battle map contains an invalid or duplicate spawn point id. mapId={map.Id}, spawnPointId={spawnPoint.Id}");
                }

                if (spawnPoint.TeamId <= 0)
                {
                    throw new InvalidOperationException($"Battle map spawn point team id must be positive. mapId={map.Id}, spawnPointId={spawnPoint.Id}, teamId={spawnPoint.TeamId}");
                }

                var isWalkable = false;
                var spawnPosition = spawnPoint.Position;
                for (int areaIndex = 0; areaIndex < map.WalkableAreas.Count; areaIndex++)
                {
                    if (!ContainsXZ(map.WalkableAreas[areaIndex], in spawnPosition, SpawnRadius)) continue;
                    isWalkable = true;
                    break;
                }

                if (!isWalkable)
                {
                    throw new InvalidOperationException($"Battle map spawn point must be inside a walkable area with actor clearance. mapId={map.Id}, spawnPointId={spawnPoint.Id}");
                }
            }
        }

        private static bool ContainsXZ(MapWalkableAreaMO area, in Vec3 position, float radius)
        {
            var halfX = area.Size.X * 0.5f - radius;
            var halfZ = area.Size.Z * 0.5f - radius;
            return halfX >= 0f
                && halfZ >= 0f
                && position.X >= area.Center.X - halfX
                && position.X <= area.Center.X + halfX
                && position.Z >= area.Center.Z - halfZ
                && position.Z <= area.Center.Z + halfZ;
        }

        private static bool ContainsXZ(Vec3 outerCenter, Vec3 outerSize, Vec3 innerCenter, Vec3 innerSize)
        {
            var outerHalfX = outerSize.X * 0.5f;
            var outerHalfZ = outerSize.Z * 0.5f;
            var innerHalfX = innerSize.X * 0.5f;
            var innerHalfZ = innerSize.Z * 0.5f;
            return innerCenter.X - innerHalfX >= outerCenter.X - outerHalfX
                && innerCenter.X + innerHalfX <= outerCenter.X + outerHalfX
                && innerCenter.Z - innerHalfZ >= outerCenter.Z - outerHalfZ
                && innerCenter.Z + innerHalfZ <= outerCenter.Z + outerHalfZ;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        private static void ValidateShape(int mapId, MapCollisionObjectMO mapObject)
        {
            switch (mapObject.ShapeType)
            {
                case MapCollisionShapeType.Box:
                    if (mapObject.Size.X <= 0f || mapObject.Size.Y <= 0f || mapObject.Size.Z <= 0f)
                    {
                        throw new InvalidOperationException($"Battle map box size must be positive. mapId={mapId}, objectId={mapObject.Id}, size={mapObject.Size}");
                    }
                    break;

                case MapCollisionShapeType.Sphere:
                    if (mapObject.Radius <= 0f)
                    {
                        throw new InvalidOperationException($"Battle map sphere radius must be positive. mapId={mapId}, objectId={mapObject.Id}, radius={mapObject.Radius}");
                    }
                    break;

                case MapCollisionShapeType.Capsule:
                    if (mapObject.Radius <= 0f || mapObject.Height < mapObject.Radius * 2f)
                    {
                        throw new InvalidOperationException($"Battle map capsule requires a positive radius and height >= diameter. mapId={mapId}, objectId={mapObject.Id}, radius={mapObject.Radius}, height={mapObject.Height}");
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Battle map collision shape is invalid. mapId={mapId}, objectId={mapObject.Id}, shape={mapObject.ShapeType}");
            }
        }
    }
}
