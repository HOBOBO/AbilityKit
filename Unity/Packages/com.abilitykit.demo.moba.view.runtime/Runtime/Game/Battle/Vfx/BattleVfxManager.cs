using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    public sealed class BattleVfxManager
    {
        private readonly VfxDatabase _db;
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>(StringComparer.Ordinal);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<ulong> _interpFallbackWarned = new HashSet<ulong>();
#endif

        public BattleVfxManager(VfxDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public bool TryCreateVfxEntity(EC.IECWorld world, EC.IEntity parent, int vfxId, EC.IEntityId followTarget, in Vector3 position, out EC.IEntity entity)
        {
            entity = default;
            if (world == null) return false;
            if (!parent.IsValid) return false;
            if (vfxId <= 0) return false;

            if (!_db.TryGet(vfxId, out var dto) || dto == null || string.IsNullOrEmpty(dto.Resource))
            {
                return false;
            }

            if (!_prefabCache.TryGetValue(dto.Resource, out var prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>(dto.Resource);
                _prefabCache[dto.Resource] = prefab;
            }

            GameObject go;
            if (prefab != null)
            {
                go = UnityEngine.Object.Instantiate(prefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"Vfx_{vfxId}";
            go.transform.position = position;

            var e = world.CreateChild(parent);
            e.SetName($"Vfx_{vfxId}");
            e.WithRef(new BattleVfxComponent { VfxId = vfxId });
            e.WithRef(new BattleViewGameObjectComponent { GameObject = go });
            e.WithRef(new BattleViewFollowComponent { Target = followTarget, Offset = Vector3.zero });

            // DurationMs > 0 => auto-expire. DurationMs == 0 (or <0) => permanent, should be destroyed by external driver.
            if (dto.DurationMs > 0)
            {
                e.WithRef(new BattleVfxLifetimeComponent { ExpireAtTime = Time.time + (dto.DurationMs / 1000f) });
            }

            entity = e;
            return true;
        }

        public void Tick(in EC.IEntity vfxRoot)
        {
            Tick(vfxRoot, binder: null);
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder)
        {
            if (!vfxRoot.IsValid) return;
            var world = vfxRoot.World;
            if (world == null) return;

            // Scan all VFX entities and:
            // 1) Sync follow position
            // 2) Destroy if expired
            // Keep it simple; battle vfx count is expected to be low.

            var tmp = new List<EC.IEntityId>(32);
            CollectVfxEntities(vfxRoot, tmp);
            if (tmp.Count == 0) return;

            for (int i = 0; i < tmp.Count; i++)
            {
                var id = tmp[i];
                if (!world.IsAlive(id)) continue;
                var e = world.Wrap(id);

                if (e.TryGetRef(out BattleVfxLifetimeComponent life) && life != null && life.ExpireAtTime > 0f)
                {
                    if (Time.time >= life.ExpireAtTime)
                    {
                        DestroyVfxEntity(world, id);
                        continue;
                    }
                }

                if (e.TryGetRef(out BattleViewFollowComponent follow) && follow != null && follow.Target.Index != 0)
                {
                    if (!world.IsAlive(follow.Target)) continue;

                    // Prefer view-interpolated position when available.
                    if (binder != null && binder.TryGetInterpolatedPos(follow.Target, out var viewPos))
                    {
                        SyncFollow(world, id, viewPos);
                        continue;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (binder != null)
                    {
                        var key = ((ulong)(uint)id.Index << 32) | (uint)follow.Target.Index;
                        if (_interpFallbackWarned.Add(key))
                        {
                            Debug.LogWarning($"[BattleVfxManager] VFX follow fallback to logic position: vfx={id.Index} target={follow.Target.Index} frame={Time.frameCount}");
                        }
                    }
#endif

                    if (world.Wrap(follow.Target).TryGetRef(out BattleTransformComponent t) && t != null)
                    {
                        SyncFollow(world, id, t.Position);
                    }
                }
            }
        }

        private static void CollectVfxEntities(EC.IEntity root, List<EC.IEntityId> results)
        {
            if (!root.IsValid) return;
            if (results == null) return;

            var stack = new Stack<EC.IEntity>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (!e.IsValid) continue;

                if (e.TryGetRef(out BattleVfxComponent vfx) && vfx != null)
                {
                    results.Add(e.Id);
                }

                var childCount = e.ChildCount;
                for (int i = 0; i < childCount; i++)
                {
                    stack.Push(e.GetChild(i));
                }
            }
        }

        public void DestroyVfxEntity(EC.IECWorld world, EC.IEntityId id)
        {
            if (world == null) return;
            if (!world.IsAlive(id)) return;

            var e = world.Wrap(id);
            if (e.TryGetRef(out BattleViewGameObjectComponent goComp) && goComp != null && goComp.GameObject != null)
            {
                UnityEngine.Object.Destroy(goComp.GameObject);
                goComp.GameObject = null;
            }

            if (e.IsValid) e.Destroy();
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos)
        {
            if (world == null) return;
            if (!world.IsAlive(vfxEntityId)) return;

            var e = world.Wrap(vfxEntityId);
            if (!e.TryGetRef(out BattleViewGameObjectComponent goComp) || goComp == null || goComp.GameObject == null) return;

            var pos = targetPos;
            if (e.TryGetRef(out BattleViewFollowComponent follow) && follow != null)
            {
                pos += follow.Offset;
            }

            goComp.GameObject.transform.position = pos;
        }
    }
}
