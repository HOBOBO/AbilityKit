using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Diagnostics.DebugDraw;
using AbilityKit.Core.Mathematics;
using AbilityKit.Diagnostics.Editor.DebugDraw;
using AbilityKit.Game.Battle;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [InitializeOnLoad]
    public static class CollisionWorldGizmoDrawer
    {
        private static readonly List<CollisionWorldDebugShape> s_shapes = new List<CollisionWorldDebugShape>(2048);

        static CollisionWorldGizmoDrawer()
        {
            DebugDrawSceneViewDriver.Register(CollisionWorldContributor.Instance);
        }

        private sealed class CollisionWorldContributor : IDebugDrawContributor
        {
            public static readonly CollisionWorldContributor Instance = new CollisionWorldContributor();

            public DebugDrawMask Mask => DebugDrawEditorSettings.Masks.Collision;

            public void Draw(in DebugDrawContext ctx, IDebugDraw draw)
            {
                s_shapes.Clear();
                var session = BattleLogicSessionHost.Current;
                if (session == null) return;
                if (!session.TryGetWorld(out var world) || world == null) return;

                var services = world.Services;
                if (services == null) return;

                if (!services.TryResolve<ICollisionService>(out var collisionSvc) || collisionSvc == null) return;
                if (collisionSvc.World is not ICollisionWorldDebugView debugView) return;

                try
                {
                    var copied = debugView.CopyWorldShapes(s_shapes);
                    if (copied <= 0) return;
                }
                catch
                {
                    s_shapes.Clear();
                    return;
                }

                if (s_shapes.Count == 0) return;

                var max = DebugDrawEditorSettings.MaxItemsPerContributor;
                if (max <= 0) max = 2048;

                var mask = DebugDrawEditorSettings.CollisionLayerMask;

                var count = s_shapes.Count;
                if (count > max) count = max;

                for (int i = 0; i < count; i++)
                {
                    var s = s_shapes[i];
                    if (mask != 0 && s.LayerId >= 0 && s.LayerId < 32 && ((1 << s.LayerId) & mask) == 0) continue;

                    var style = new DebugDrawStyle(ResolveLayerColor(s.LayerId));
                    var shape = s.WorldShape;
                    switch (shape.Type)
                    {
                        case ColliderShapeType.Sphere:
                            draw.DrawWireSphere(in shape.Sphere.Center, shape.Sphere.Radius, in style);
                            break;
                        case ColliderShapeType.Capsule:
                            draw.DrawWireCapsule(in shape.Capsule.A, in shape.Capsule.B, shape.Capsule.Radius, in style);
                            break;
                        case ColliderShapeType.Aabb:
                        {
                            var size = shape.Aabb.Extents * 2f;
                            var center = shape.Aabb.Center;
                            draw.DrawWireAabb(in center, in size, in style);
                            break;
                        }
                        case ColliderShapeType.OBB:
                            DrawObb(draw, shape.Obb, in style);
                            break;
                    }
                }
            }

            private static DebugDrawColor ResolveLayerColor(int layerId)
            {
                // Keep the common MOBA layers visually distinct while preserving the
                // user-configured collision color for unknown/custom layers.
                switch (layerId)
                {
                    case 0: return new DebugDrawColor(80, 220, 255, 220); // unit
                    case 1: return new DebugDrawColor(255, 170, 64, 230); // projectile
                    case 2: return new DebugDrawColor(140, 170, 255, 230); // world
                    default: return DebugDrawEditorSettings.CollisionColor;
                }
            }

            private static void DrawObb(IDebugDraw draw, in Obb obb, in DebugDrawStyle style)
            {
                obb.GetCorners(
                    out var c0, out var c1, out var c2, out var c3,
                    out var c4, out var c5, out var c6, out var c7);

                DrawEdge(draw, in c0, in c1, in style);
                DrawEdge(draw, in c0, in c2, in style);
                DrawEdge(draw, in c1, in c3, in style);
                DrawEdge(draw, in c2, in c3, in style);
                DrawEdge(draw, in c4, in c5, in style);
                DrawEdge(draw, in c4, in c6, in style);
                DrawEdge(draw, in c5, in c7, in style);
                DrawEdge(draw, in c6, in c7, in style);
                DrawEdge(draw, in c0, in c4, in style);
                DrawEdge(draw, in c1, in c5, in style);
                DrawEdge(draw, in c2, in c6, in style);
                DrawEdge(draw, in c3, in c7, in style);
            }

            private static void DrawEdge(IDebugDraw draw, in Vec3 from, in Vec3 to, in DebugDrawStyle style)
            {
                draw.DrawLine(in from, in to, in style);
            }
        }
    }
}
