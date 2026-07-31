using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 寻路结果状态。
    /// </summary>
    public enum PathStatus
    {
        /// <summary>找到完整路径到目标。</summary>
        Found = 0,

        /// <summary>目标不可达，返回到最近可行点的部分路径。</summary>
        Partial = 1,

        /// <summary>无可行路径。</summary>
        Failed = 2,
    }

    /// <summary>
    /// 寻路结果：世界空间路径点序列 + 状态。
    /// </summary>
    public readonly struct NavigationPath
    {
        public readonly Vec3[] Waypoints;
        public readonly PathStatus Status;

        public NavigationPath(Vec3[] waypoints, PathStatus status)
        {
            Waypoints = waypoints ?? Array.Empty<Vec3>();
            Status = status;
        }

        public int Length => Waypoints.Length;
        public bool HasPath => Status != PathStatus.Failed && Waypoints.Length > 0;

        public static NavigationPath Failed => new NavigationPath(Array.Empty<Vec3>(), PathStatus.Failed);
    }
}
