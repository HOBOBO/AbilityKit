using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Navigation
{
    /// <summary>
    /// 导航网格烘焙：从地图配置（WalkableAreas 边界）+ 已注册进碰撞世界的 World 层障碍，
    /// 采样生成 <see cref="NavigationGrid"/>。
    ///
    /// 单一可走性真相 = 同时满足「位于某个 WalkableArea 内」且「无 World 障碍重叠（按 agentRadius 膨胀）」。
    /// 复用 <see cref="ICollisionWorld.OverlapSphere"/> 的精确窄相，避免新写 point-in-shape。
    /// 一次性、按固定采样序执行，确定性。
    /// </summary>
    internal static class MobaNavigationBake
    {
        public static NavigationGrid Build(
            BattleMapMO map,
            ICollisionWorld collisionWorld,
            NavigationWorldOptions options)
        {
            var cellSize = options.CellSize;
            var agentRadius = options.AgentRadius;

            var center = map.Bounds.Center;
            var size = map.Bounds.Size;
            var halfX = size.X * 0.5f;
            var halfZ = size.Z * 0.5f;

            var origin = new Vec3(center.X - halfX, center.Y, center.Z - halfZ);
            var width = (int)System.Math.Ceiling(size.X / cellSize);
            var height = (int)System.Math.Ceiling(size.Z / cellSize);
            if (width < 1) width = 1;
            if (height < 1) height = 1;

            var blocked = new bool[width * height];
            var filter = new LayerFilter(MobaCollisionLayers.WorldMask);
            var overlapResults = new List<ColliderId>(16);

            for (var cz = 0; cz < height; cz++)
            {
                for (var cx = 0; cx < width; cx++)
                {
                    var cellCenter = new Vec3(
                        origin.X + (cx + 0.5f) * cellSize,
                        origin.Y,
                        origin.Z + (cz + 0.5f) * cellSize);

                    var isBlocked = !IsInsideWalkableArea(map, in cellCenter);
                    if (!isBlocked)
                    {
                        overlapResults.Clear();
                        var count = collisionWorld.OverlapSphere(
                            new Sphere(in cellCenter, agentRadius),
                            in filter,
                            overlapResults);
                        isBlocked = count > 0;
                    }

                    blocked[cz * width + cx] = isBlocked;
                }
            }

            return new NavigationGrid(origin, cellSize, width, height, blocked);
        }

        private static bool IsInsideWalkableArea(BattleMapMO map, in Vec3 position)
        {
            var areas = map.WalkableAreas;
            for (int i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                var halfX = area.Size.X * 0.5f;
                var halfZ = area.Size.Z * 0.5f;
                if (position.X >= area.Center.X - halfX
                    && position.X <= area.Center.X + halfX
                    && position.Z >= area.Center.Z - halfZ
                    && position.Z <= area.Center.Z + halfZ)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
