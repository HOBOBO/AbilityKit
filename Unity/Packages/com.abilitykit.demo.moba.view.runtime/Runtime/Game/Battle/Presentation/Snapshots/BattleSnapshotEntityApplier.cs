using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Protocol.Moba;
using AbilityKit.World.ECS;
using UnityEngine;
using BattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Flow
{
    internal static class BattleSnapshotEntityApplier
    {
        public static void ApplyStateHash(BattleContext ctx, StateHashData payload)
        {
            if (ctx == null) return;

            var node = ctx.EntityNode;
            if (!node.IsValid) return;

            var comp = node.TryGetRef(out BattleStateHashSnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleStateHashSnapshotComponent();
                node.WithRef(comp);
            }

            comp.Version = payload.Version;
            comp.Frame = payload.FrameIndex;
            comp.Hash = payload.StateHash;
        }

        public static void ApplyTransform(
            BattleContext ctx,
            ActorTransformData[] entries,
            string logContext = null)
        {
            if (ctx == null) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            if (world == null || lookup == null || ctx.EntityFactory == null)
            {
                LogMissingEntityRuntime(logContext, "ApplyTransform");
                return;
            }

            var dirty = GetDirtyEntities(ctx, 64);

            if (entries == null || entries.Length == 0) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var netId = new BattleNetId(entry.ActorId);

                if (!lookup.TryResolve(world, netId, out var entity))
                {
                    continue;
                }

                if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null)
                {
                    transform = new BattleTransformComponent();
                    entity.WithRef(transform);
                }

                transform.Position.x = entry.PositionX;
                transform.Position.y = entry.PositionY;
                transform.Position.z = entry.PositionZ;
                transform.Forward = ResolveForward(entry.ForwardX, entry.ForwardY, entry.ForwardZ, transform.Forward);

                dirty.Add(entity.Id);
            }
        }

        public static void ApplySpawn(
            BattleContext ctx,
            ActorSpawnData[] entries,
            bool updateExisting = true,
            string logContext = null)
        {
            if (ctx == null) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            var factory = ctx.EntityFactory;
            if (world == null || lookup == null || factory == null)
            {
                LogMissingEntityRuntime(logContext, "ApplySpawn");
                return;
            }

            var dirty = GetDirtyEntities(ctx, entries?.Length ?? 8);

            if (entries == null || entries.Length == 0) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0) continue;
                ctx.ObserveActorSpawnIdentity(entry.ActorId, entry.EntityVersion);

                var netId = new BattleNetId(entry.ActorId);
                if (!lookup.TryResolve(world, netId, out var entity))
                {
                    entity = entry.IsProjectile
                        ? factory.CreateProjectile(netId, ownerNetId: new BattleNetId(entry.OwnerActorId), entityCode: entry.EntityCode)
                        : factory.CreateCharacter(netId, entityCode: entry.EntityCode);
                }
                else if (!updateExisting)
                {
                    continue;
                }
                else
                {
                    UpdateExistingSpawnEntity(entity, entry);
                }

                if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null)
                {
                    transform = new BattleTransformComponent();
                    entity.WithRef(transform);
                }

                transform.Position = new Vector3(entry.PositionX, entry.PositionY, entry.PositionZ);
                if (transform.Forward == default) transform.Forward = Vector3.forward;

                dirty.Add(entity.Id);
            }
        }

        public static void ApplyDespawn(BattleContext ctx, ActorDespawnData[] entries)
        {
            if (ctx == null || entries == null || entries.Length == 0) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            if (world == null || lookup == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0) continue;
                ctx.ForgetActorIdentity(entry.ActorId);

                ctx.ViewVfxManager?.DestroyVfxByFollowTargetActorId(
                    ctx.ViewVfxNode,
                    entry.ActorId);

                var netId = new BattleNetId(entry.ActorId);
                if (lookup.TryResolve(world, netId, out var entity) && entity.IsValid)
                {
                    entity.Destroy();
                }

                lookup.Unbind(netId);
            }
        }

        private static void UpdateExistingSpawnEntity(IEntity entity, ActorSpawnData entry)
        {
            if (entity.TryGetRef(out BattleEntityMetaComponent meta) && meta != null)
            {
                meta.Kind = entry.IsProjectile
                    ? BattleEntityKind.Projectile
                    : BattleEntityKind.Character;
                meta.EntityCode = entry.EntityCode;
            }

            if (entry.IsProjectile
                && entity.TryGetRef(out BattleProjectileComponent projectile)
                && projectile != null)
            {
                projectile.OwnerNetId = new BattleNetId(entry.OwnerActorId);
            }
        }

        private static Vector3 ResolveForward(float x, float y, float z, Vector3 fallback)
        {
            var forward = new Vector3(x, y, z);
            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
            if (fallback.sqrMagnitude > 0.0001f) return fallback.normalized;
            return Vector3.forward;
        }

        private static List<IEntityId> GetDirtyEntities(BattleContext ctx, int capacity)
        {
            var dirty = ctx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new List<IEntityId>(capacity);
                ctx.DirtyEntities = dirty;
            }
            // Note: clearing is delegated to SharedDirtySyncSubFeature.Tick at the
            // start of each frame to avoid accidentally clearing between producers.

            return dirty;
        }

        private static void LogMissingEntityRuntime(string logContext, string operation)
        {
            if (string.IsNullOrEmpty(logContext)) return;

            Log.Error($"[{logContext}] {operation} ignored: BattleContext entity wiring not ready.");
        }
    }
}
