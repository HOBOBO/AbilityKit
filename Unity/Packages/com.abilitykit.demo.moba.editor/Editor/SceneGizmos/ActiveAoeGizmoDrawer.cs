using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Diagnostics.DebugDraw;
using AbilityKit.Diagnostics.Editor.DebugDraw;
using AbilityKit.Game.Battle;
using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// Draws the actual active AOE instances owned by the logic world.
    /// This is intentionally separate from skill cast range previews: an AOE can
    /// move, expire, or be spawned by a delayed action after the cast preview is gone.
    /// </summary>
    [InitializeOnLoad]
    public static class ActiveAoeGizmoDrawer
    {
        static ActiveAoeGizmoDrawer()
        {
            DebugDrawSceneViewDriver.Register(Contributor.Instance);
        }

        private sealed class Contributor : IDebugDrawContributor
        {
            public static readonly Contributor Instance = new Contributor();
            private readonly List<MobaAreaRuntimeInfo> _areas = new List<MobaAreaRuntimeInfo>(128);

            public DebugDrawMask Mask => new DebugDrawMask(
                DebugDrawEditorSettings.Masks.Targeting.Value | MobaSceneGizmoSettings.ActiveAoeBit);

            public void Draw(in DebugDrawContext ctx, IDebugDraw draw)
            {
                if (!MobaSceneGizmoSettings.IsActiveAoeEnabled()) return;

                var session = BattleLogicSessionHost.Current;
                if (session == null || !session.TryGetWorld(out var world) || world == null) return;
                var services = world.Services;
                if (services == null || !services.TryResolve<MobaAreaRuntimeService>(out var areas) || areas == null) return;

                _areas.Clear();
                if (!areas.TryGetAreas(_areas)) return;

                var max = MobaSceneGizmoSettings.MaxActiveAoes;
                if (max <= 0) max = 128;
                var count = _areas.Count < max ? _areas.Count : max;
                var style = new DebugDrawStyle(MobaSceneGizmoSettings.ActiveAoeColor);

                for (var i = 0; i < count; i++)
                {
                    var area = _areas[i];
                    if (area.Radius <= 0f) continue;

                    var center = area.Center;
                    MobaGizmoGeometry.DrawGroundCircle(draw, in center, area.Radius, in style);

                    // A short cross makes a zero-height wire sphere readable from a top-down view.
                    var arm = area.Radius * 0.18f;
                    var x0 = new Vec3(center.X - arm, center.Y, center.Z);
                    var x1 = new Vec3(center.X + arm, center.Y, center.Z);
                    var z0 = new Vec3(center.X, center.Y, center.Z - arm);
                    var z1 = new Vec3(center.X, center.Y, center.Z + arm);
                    draw.DrawLine(in x0, in x1, in style);
                    draw.DrawLine(in z0, in z1, in style);
                }

                _areas.Clear();
            }
        }
    }
}
