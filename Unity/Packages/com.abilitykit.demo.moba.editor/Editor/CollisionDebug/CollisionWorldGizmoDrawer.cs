using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.DebugDraw;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Game.Battle;
using AbilityKit.Editor.DebugDraw;
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

                var style = new DebugDrawStyle(DebugDrawEditorSettings.CollisionColor);
                var mask = DebugDrawEditorSettings.CollisionLayerMask;

                var count = s_shapes.Count;
                if (count > max) count = max;

                for (int i = 0; i < count; i++)
                {
                    var s = s_shapes[i];
                    if (mask != 0 && (s.LayerMask & mask) == 0) continue;

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
                    }
                }
            }
        }
    }
}
