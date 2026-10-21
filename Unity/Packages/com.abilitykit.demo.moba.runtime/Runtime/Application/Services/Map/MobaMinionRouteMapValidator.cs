using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Minions;

namespace AbilityKit.Demo.Moba.Services.Map
{
    public static class MobaMinionRouteMapValidator
    {
        public static void ValidateResource(MobaMinionRouteResource resource, BattleMapMO map)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (resource.MapId != map.Id)
                throw new InvalidOperationException(
                    $"Minion route resource map does not match the active map. resourceMapId={resource.MapId}, mapId={map.Id}, version={resource.Version}");
            for (var i = 0; i < resource.Routes.Count; i++)
                Validate(resource.Routes[i], map);
        }

        public static void Validate(MobaMinionRouteDefinition route, BattleMapMO map)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (route.AcceptanceRadius <= 0f || float.IsNaN(route.AcceptanceRadius) || float.IsInfinity(route.AcceptanceRadius))
                throw new InvalidOperationException($"Minion route acceptance radius is invalid. routeId={route.RouteId}");
            if (map.Bounds == null || map.Bounds.Size.X <= 0f || map.Bounds.Size.Z <= 0f)
                throw new InvalidOperationException($"Battle map bounds must have positive XZ size. mapId={map.Id}");
            for (var areaIndex = 0; areaIndex < map.WalkableAreas.Count; areaIndex++)
            {
                var area = map.WalkableAreas[areaIndex];
                if (area == null || area.Size.X <= 0f || area.Size.Z <= 0f)
                    throw new InvalidOperationException($"Battle map walkable area must have positive XZ size. mapId={map.Id}, areaIndex={areaIndex}");
            }
            if (route.Waypoints == null || route.Waypoints.Count < 2)
                throw new InvalidOperationException($"Minion route must contain at least two waypoints. routeId={route.RouteId}");

            for (var i = 0; i < route.Waypoints.Count; i++)
            {
                var point = route.Waypoints[i];
                if (!ContainsXZ(map.Bounds, in point))
                    throw new InvalidOperationException($"Minion route waypoint is outside map bounds. mapId={map.Id}, routeId={route.RouteId}, waypoint={i}");

                var walkable = false;
                for (var areaIndex = 0; areaIndex < map.WalkableAreas.Count; areaIndex++)
                {
                    if (!ContainsXZ(map.WalkableAreas[areaIndex], in point)) continue;
                    walkable = true;
                    break;
                }

                if (!walkable)
                    throw new InvalidOperationException($"Minion route waypoint is outside walkable areas. mapId={map.Id}, routeId={route.RouteId}, waypoint={i}");
            }

            var teamOneSpawn = FindFirstSpawn(map, 1);
            var teamTwoSpawn = FindFirstSpawn(map, 2);
            if (teamOneSpawn == null || teamTwoSpawn == null)
                throw new InvalidOperationException($"Minion route requires spawn points for teams 1 and 2. mapId={map.Id}, routeId={route.RouteId}");

            if (!WithinRadius(route.Waypoints[0], teamOneSpawn.Position, route.AcceptanceRadius))
                throw new InvalidOperationException($"Minion route start must match team 1 spawn point. mapId={map.Id}, routeId={route.RouteId}, spawnPointId={teamOneSpawn.Id}");
            if (!WithinRadius(route.Waypoints[route.Waypoints.Count - 1], teamTwoSpawn.Position, route.AcceptanceRadius))
                throw new InvalidOperationException($"Minion route end must match team 2 spawn point. mapId={map.Id}, routeId={route.RouteId}, spawnPointId={teamTwoSpawn.Id}");
        }

        private static MapSpawnPointMO FindFirstSpawn(BattleMapMO map, int teamId)
        {
            for (var i = 0; i < map.SpawnPoints.Count; i++)
                if (map.SpawnPoints[i].TeamId == teamId) return map.SpawnPoints[i];
            return null;
        }

        private static bool ContainsXZ(MapBoundsMO bounds, in Vec3 point)
        {
            var halfX = bounds.Size.X * 0.5f;
            var halfZ = bounds.Size.Z * 0.5f;
            return point.X >= bounds.Center.X - halfX && point.X <= bounds.Center.X + halfX &&
                   point.Z >= bounds.Center.Z - halfZ && point.Z <= bounds.Center.Z + halfZ;
        }

        private static bool ContainsXZ(MapWalkableAreaMO area, in Vec3 point)
        {
            var halfX = area.Size.X * 0.5f;
            var halfZ = area.Size.Z * 0.5f;
            return point.X >= area.Center.X - halfX && point.X <= area.Center.X + halfX &&
                   point.Z >= area.Center.Z - halfZ && point.Z <= area.Center.Z + halfZ;
        }

        private static bool WithinRadius(in Vec3 left, in Vec3 right, float radius)
        {
            var dx = left.X - right.X;
            var dz = left.Z - right.Z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
