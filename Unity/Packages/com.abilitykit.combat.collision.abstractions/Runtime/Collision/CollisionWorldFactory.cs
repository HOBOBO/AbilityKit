using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 碰撞世界配置选项
    /// </summary>
    public sealed class CollisionWorldOptions
    {
        /// <summary>
        /// 广相算法类型
        /// </summary>
        public BroadphaseType BroadphaseType { get; set; } = BroadphaseType.Naive;

        /// <summary>
        /// 空间划分网格大小（Grid 广相时生效）
        /// </summary>
        public float GridCellSize { get; set; } = 4f;

        /// <summary>
        /// 初始容量
        /// </summary>
        public int InitialCapacity { get; set; } = 64;
    }

    /// <summary>
    /// 碰撞世界工厂
    /// </summary>
    public static class CollisionWorldFactory
    {
        /// <summary>
        /// 创建碰撞世界
        /// </summary>
        public static ICollisionWorld Create(CollisionWorldOptions options = null)
        {
            options ??= new CollisionWorldOptions();

            return options.BroadphaseType switch
            {
                BroadphaseType.Naive => new NaiveCollisionWorld(),
                BroadphaseType.Grid => new GridCollisionWorld(options.GridCellSize, options.InitialCapacity),
                // NOTE: DynamicAabbTree 后端尚未实现（无 tree-backed world），暂等价 Grid；建 tree-world 另列。
                BroadphaseType.DynamicAabbTree => new GridCollisionWorld(options.GridCellSize, options.InitialCapacity),
                _ => new NaiveCollisionWorld()
            };
        }

        /// <summary>
        /// 创建朴素碰撞世界
        /// </summary>
        public static ICollisionWorld CreateNaive()
        {
            return new NaiveCollisionWorld();
        }

        /// <summary>
        /// 创建基于网格的碰撞世界
        /// </summary>
        public static ICollisionWorld CreateWithGrid(float cellSize = 4f, int capacity = 64)
        {
            return new GridCollisionWorld(cellSize, capacity);
        }

        /// <summary>
        /// 创建基于动态 AABB 树的碰撞世界。
        /// 注意：tree-backed world 尚未实现，当前等价返回 Grid 碰撞世界。
        /// </summary>
        public static ICollisionWorld CreateWithDynamicTree(int capacity = 64)
        {
            return new GridCollisionWorld(4f, capacity);
        }
    }
}
