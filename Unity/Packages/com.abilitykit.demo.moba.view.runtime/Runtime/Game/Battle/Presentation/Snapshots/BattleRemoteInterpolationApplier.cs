using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class BattleRemoteInterpolationApplier
    {
        internal static int ResolveExcludedLocalActorId(bool enableClientPrediction, int localActorId)
        {
            return enableClientPrediction ? localActorId : 0;
        }

        public static void Apply(
            IBattleEntityContext entityContext,
            in GatewayStateSyncSnapshot snapshot,
            int localActorId)
        {
            new Session().Apply(entityContext, in snapshot, localActorId);
        }

        internal sealed class Session
        {
            private readonly BattleActorViewProjectionWorkspace _workspace =
                new BattleActorViewProjectionWorkspace();
            private UnityActorViewPort _port;

            public void Apply(
                IBattleEntityContext context,
                in GatewayStateSyncSnapshot snapshot,
                int localActorId)
            {
                if (context?.EntityWorld == null ||
                    context.EntityLookup == null ||
                    context.EntityFactory == null) return;

                if (_port == null || !_port.Matches(context))
                    _port = new UnityActorViewPort(context);
                BattleActorViewProjector.ApplyWithoutBatch(
                    _port, in snapshot, localActorId, _workspace);
            }

            public void Reset()
            {
                _port = null;
            }
        }

        private sealed class UnityActorViewPort : IBattleActorViewPort
        {
            private readonly IBattleEntityContext _context;
            private readonly IECWorld _world;
            private readonly BattleEntityLookup _lookup;
            private readonly BattleEntityFactory _factory;

            public UnityActorViewPort(IBattleEntityContext context)
            {
                _context = context;
                _world = context.EntityWorld;
                _lookup = context.EntityLookup;
                _factory = context.EntityFactory;
            }

            public bool Matches(IBattleEntityContext context)
            {
                return ReferenceEquals(_context, context) &&
                       ReferenceEquals(_world, context.EntityWorld) &&
                       ReferenceEquals(_lookup, context.EntityLookup) &&
                       ReferenceEquals(_factory, context.EntityFactory);
            }

            public IEnumerable<int> GetActorIds()
            {
                var ids = new List<int>();
                _world.ForEachAlive(entity =>
                {
                    if (entity.TryGetRef(out BattleNetIdComponent netId) && netId != null)
                        ids.Add(netId.NetId.Value);
                });
                return ids;
            }

            public void Upsert(in GatewayStateSyncActorSnapshot actor)
            {
                var netId = new BattleNetId(actor.ActorId);
                if (!_lookup.TryResolve(_world, netId, out var entity))
                {
                    entity = actor.Kind == (int)SpawnEntityKind.Projectile
                        ? _factory.CreateProjectile(netId, new BattleNetId(actor.OwnerNetId), actor.Code)
                        : _factory.CreateCharacter(netId, actor.Code);
                }

                if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null)
                {
                    transform = new BattleTransformComponent();
                    entity.WithRef(transform);
                }

                transform.Position = new Vector3(actor.X, actor.Y, actor.Z);
                transform.Forward = new Vector3(Mathf.Sin(actor.Rotation), 0f, Mathf.Cos(actor.Rotation));
                var dirty = _context.DirtyEntities;
                if (dirty == null)
                {
                    dirty = new List<IEntityId>();
                    _context.DirtyEntities = dirty;
                }
                dirty.Add(entity.Id);
            }

            public void Remove(int actorId)
            {
                var netId = new BattleNetId(actorId);
                if (_lookup.TryResolve(_world, netId, out var entity))
                {
                    Destroy(entity.Id);
                    return;
                }

                var staleIds = new List<IEntityId>();
                _world.ForEachAlive(candidate =>
                {
                    if (candidate.TryGetRef(out BattleNetIdComponent component) &&
                        component != null &&
                        component.NetId.Value == actorId)
                    {
                        staleIds.Add(candidate.Id);
                    }
                });
                foreach (var id in staleIds) Destroy(id);
            }

            private void Destroy(IEntityId id)
            {
                _lookup.UnbindByEntityId(id);
                if (_world.IsAlive(id)) _world.Wrap(id).Destroy();
            }
        }
    }
}
