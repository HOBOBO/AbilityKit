using System.Collections.Generic;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 提供碰撞体的包围范围，沿用形状重叠查询的边界扩张规则。
    /// </summary>
    public interface IColliderBoundsSource
    {
        /// <summary>
        /// 将碰撞体的边界写入调用方提供的列表。
        /// </summary>
        void CopyColliderBounds(List<ColliderBounds> results);
    }

    /// <summary>
    /// 描述单个碰撞体的包围范围及其所属碰撞层。
    /// </summary>
    public readonly struct ColliderBounds
    {
        /// <summary>
        /// 碰撞体标识。
        /// </summary>
        public readonly ColliderId Collider;

        /// <summary>
        /// 包围范围的世界坐标中心。
        /// </summary>
        public readonly Vec3 Center;

        /// <summary>
        /// 包围半径，单位与碰撞世界一致。
        /// </summary>
        public readonly float Radius;

        /// <summary>
        /// 碰撞层标识。
        /// </summary>
        public readonly int LayerId;

        /// <summary>
        /// 以原样提供的标识、中心、半径和层创建边界值。
        /// </summary>
        public ColliderBounds(ColliderId collider, Vec3 center, float radius, int layerId)
        {
            Collider = collider;
            Center = center;
            Radius = radius;
            LayerId = layerId;
        }
    }
}
