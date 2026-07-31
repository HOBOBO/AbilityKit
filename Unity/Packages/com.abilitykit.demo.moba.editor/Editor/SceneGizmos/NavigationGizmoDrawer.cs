using System.Collections.Generic;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Debugging;
using AbilityKit.Core.Editor.Debugging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Navigation;
using AbilityKit.Game.Battle;
using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// Scene 视图绘制导航网格（free=绿/blocked=红）与各 actor 的活跃寻路路径线。
    /// 从 <see cref="NavigationDebugState"/> 读取运行时数据。
    /// </summary>
    [InitializeOnLoad]
    public static class NavigationGizmoDrawer
    {
        static NavigationGizmoDrawer()
        {
            DebugDrawSceneViewDriver.Register(NavigationContributor.Instance);
        }

        private sealed class NavigationContributor : IDebugDrawContributor
        {
            public static readonly NavigationContributor Instance = new NavigationContributor();

            public DebugDrawMask Mask => new DebugDrawMask(
                DebugDrawEditorSettings.Masks.Targeting.Value
                | MobaSceneGizmoSettings.NavigationBit
                | MobaSceneGizmoSettings.PathBit);

            public void Draw(in DebugDrawContext ctx, IDebugDraw draw)
            {
                var session = BattleLogicSessionHost.Current;
                if (session == null || !session.TryGetWorld(out var world) || world == null) return;

                var services = world.Services;
                if (services == null) return;

                if (!services.TryResolve<NavigationDebugState>(out var debug) || debug == null) return;

                var drawNav = MobaSceneGizmoSettings.IsNavigationEnabled();
                var drawPath = MobaSceneGizmoSettings.IsPathEnabled();
                if (!drawNav && !drawPath) return;

                if (drawNav && debug.Grid != null)
                {
                    DrawGridCells(draw, debug.Grid, debug.Options);
                }

                if (drawPath && debug.ActivePaths.Count > 0)
                {
                    DrawPaths(draw, debug.ActivePaths);
                }
            }

            private static void DrawGridCells(IDebugDraw draw, NavigationGrid grid, NavigationWorldOptions options)
            {
                var freeStyle = new DebugDrawStyle(MobaSceneGizmoSettings.NavFreeColor);
                var blockedStyle = new DebugDrawStyle(MobaSceneGizmoSettings.NavBlockedColor);

                var maxCells = MobaSceneGizmoSettings.MaxNavCells;
                if (maxCells <= 0) maxCells = 2048;

                var cell = grid.CellSize;
                var half = cell * 0.45f;
                var thickness = cell * 0.02f;

                var drawn = 0;
                for (var cz = 0; cz < grid.Height; cz++)
                {
                    for (var cx = 0; cx < grid.Width; cx++)
                    {
                        if (drawn >= maxCells) return;

                        var center = grid.CellCenter(cx, cz);
                        center = new Vec3(center.X, center.Y + thickness, center.Z);
                        var size = new Vec3(cell * 0.9f, thickness, cell * 0.9f);

                        var style = grid.IsBlocked(cx, cz) ? blockedStyle : freeStyle;
                        draw.DrawWireAabb(in center, in size, in style);
                        drawn++;
                    }
                }
            }

            private static void DrawPaths(IDebugDraw draw, List<ActivePathEntry> paths)
            {
                var style = new DebugDrawStyle(MobaSceneGizmoSettings.PathColor);

                for (var p = 0; p < paths.Count; p++)
                {
                    var entry = paths[p];
                    var pts = entry.Waypoints;
                    if (pts == null || pts.Length < 2) continue;

                    for (var i = 1; i < pts.Length; i++)
                    {
                        draw.DrawLine(in pts[i - 1], in pts[i], in style);
                    }

                    // 画目标点小球标记
                    var target = entry.Target;
                    var half = 0.15f;
                    var markerSize = new Vec3(half * 2f, half * 2f, half * 2f);
                    draw.DrawWireAabb(in target, in markerSize, in style);
                }
            }
        }
    }
}
