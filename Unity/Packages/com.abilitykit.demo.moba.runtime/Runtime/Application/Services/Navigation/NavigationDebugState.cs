using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Navigation
{
    /// <summary>
    /// 导航调试状态服务：供编辑器 Gizmo 读取当前导航网格与各 actor 的活跃寻路路径。
    /// 每帧由 <see cref="MobaNavigationService"/> 更新网格、<see cref="Systems.Motion.MobaPathFollowingSystem"/> 更新路径。
    /// [WorldService] 自动发现。
    /// </summary>
    [WorldService(typeof(NavigationDebugState), WorldLifetime.Scoped)]
    public sealed class NavigationDebugState : IService
    {
        public NavigationGrid Grid { get; set; }
        public NavigationWorldOptions Options { get; set; }

        public readonly List<ActivePathEntry> ActivePaths = new List<ActivePathEntry>(32);

        public void SetPaths(List<ActivePathEntry> entries)
        {
            ActivePaths.Clear();
            if (entries != null && entries.Count > 0)
            {
                ActivePaths.AddRange(entries);
            }
        }

        public void Clear()
        {
            Grid = null;
            Options = null;
            ActivePaths.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 单条活跃寻路路径的快照。
    /// </summary>
    public readonly struct ActivePathEntry
    {
        public readonly int ActorId;
        public readonly Vec3[] Waypoints;
        public readonly Vec3 Target;
        public readonly Vec3 OwnerPosition;

        public ActivePathEntry(int actorId, Vec3[] waypoints, in Vec3 target, in Vec3 ownerPosition)
        {
            ActorId = actorId;
            Waypoints = waypoints;
            Target = target;
            OwnerPosition = ownerPosition;
        }
    }
}
