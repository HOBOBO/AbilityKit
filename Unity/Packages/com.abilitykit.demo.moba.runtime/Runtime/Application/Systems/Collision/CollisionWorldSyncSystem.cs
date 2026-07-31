using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using Entitas;

namespace AbilityKit.Demo.Moba.Systems.Collision
{
    /// <summary>
    /// 碰撞世界同步系统
    /// 在每帧执行时同步 Entitas 实体与碰撞世界
    /// </summary>
    [WorldSystem(MobaSystemOrder.Base + WorldSystemOrder.Early, Phase = WorldSystemPhase.PreExecute)]
    public sealed class CollisionWorldSyncSystem : WorldSystemBase
    {
        private readonly ICollisionWorld _world;
        private readonly IGroup<global::ActorEntity> _withShape;
        private readonly IGroup<global::ActorEntity> _withCollisionId;

        private readonly HashSet<int> _validIds = new HashSet<int>();
        private readonly HashSet<int> _ownedIds = new HashSet<int>();
        private readonly List<int> _staleOwnedIds = new List<int>();

        public CollisionWorldSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
            if (!services.TryResolve<ICollisionService>(out var svc) || svc == null)
            {
                throw new InvalidOperationException("ICollisionService not registered");
            }

            _world = svc.World;
            var ctx = (global::Contexts)contexts;
            _withShape = ctx.actor.GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Collider));
            _withCollisionId = ctx.actor.GetGroup(ActorMatcher.CollisionId);
        }

        protected override void OnExecute()
        {
            _validIds.Clear();

            // 添加或更新所有活跃碰撞体。
            var entities = _withShape.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.isEnabled) continue;
                if (!e.hasTransform || !e.hasCollider) continue;

                var t = e.transform.Value;
                var shape = e.collider.LocalShape;
                var layerMask = e.hasCollisionLayer ? e.collisionLayer.Mask : 0;
                var layerId = ResolveLayerId(layerMask);

                if (!e.hasCollisionId)
                {
                    var id = _world.Add(t, shape, layerId);
                    e.AddCollisionId(id);
                    _ownedIds.Add(id.Value);
                    _validIds.Add(id.Value);
                }
                else
                {
                    var id = e.collisionId.Value;
                    _world.Update(id, t, shape);
                    _world.UpdateLayer(id, layerId);
                    _validIds.Add(id.Value);
                }
            }

            // 移除已经失效的碰撞体（丢失 Transform/Collider）。
            var withIds = _withCollisionId.GetEntities();
            for (int i = 0; i < withIds.Length; i++)
            {
                var e = withIds[i];
                if (e == null) continue;
                if (!e.hasCollisionId) continue;

                if (!e.isEnabled || !e.hasTransform || !e.hasCollider)
                {
                    var id = e.collisionId.Value;
                    _world.Remove(id);
                    _ownedIds.Remove(id.Value);
                    e.RemoveCollisionId();
                }
            }

            // 只回收本系统创建的 Actor Collider。地图和其他服务注册的静态 Collider
            // 不属于本系统，不能通过全世界扫描进行清理。
            _staleOwnedIds.Clear();
            foreach (var idValue in _ownedIds)
            {
                if (!_validIds.Contains(idValue))
                {
                    _staleOwnedIds.Add(idValue);
                }
            }

            for (int i = 0; i < _staleOwnedIds.Count; i++)
            {
                var idValue = _staleOwnedIds[i];
                _world.Remove(new ColliderId(idValue));
                _ownedIds.Remove(idValue);
            }
        }

        private static int ResolveLayerId(int layerMask)
        {
            if (layerMask == 0) return 0;
            if (layerMask < 0 || (layerMask & (layerMask - 1)) != 0)
            {
                throw new InvalidOperationException($"Actor collision layer must contain exactly one bit. mask=0x{layerMask:X8}");
            }

            var layerId = 0;
            while ((layerMask >>= 1) != 0)
            {
                layerId++;
            }
            return layerId;
        }
    }
}
