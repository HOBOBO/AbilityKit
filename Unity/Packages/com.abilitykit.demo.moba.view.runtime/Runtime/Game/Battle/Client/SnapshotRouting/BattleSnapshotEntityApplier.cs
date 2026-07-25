using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class BattleSnapshotEntityApplier
    {
        public static void ApplyStateHash(BattleContext ctx, MobaStateHashSnapshotPayload payload)
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
            comp.Frame = payload.Frame;
            comp.Hash = payload.Hash;
        }

        public static void ApplyTransform(
            BattleContext ctx,
            MobaActorTransformSnapshotEntry[] entries,
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

                transform.Position.x = entry.X;
                transform.Position.y = entry.Y;
                transform.Position.z = entry.Z;
                transform.Forward = ResolveForward(entry.ForwardX, entry.ForwardY, entry.ForwardZ, transform.Forward);

                dirty.Add(entity.Id);
            }
        }

        public static void ApplySpawn(
            BattleContext ctx,
            MobaActorSpawnSnapshotEntry[] entries,
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
                if (entry.NetId <= 0) continue;

                var netId = new BattleNetId(entry.NetId);
                if (!lookup.TryResolve(world, netId, out var entity))
                {
                    entity = entry.Kind == (int)SpawnEntityKind.Projectile
                        ? factory.CreateProjectile(netId, ownerNetId: new BattleNetId(entry.OwnerNetId), entityCode: entry.Code)
                        : factory.CreateCharacter(netId, entityCode: entry.Code);
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

                transform.Position = new Vector3(entry.X, entry.Y, entry.Z);
                if (transform.Forward == default) transform.Forward = Vector3.forward;

                dirty.Add(entity.Id);
            }
        }

        private static void UpdateExistingSpawnEntity(IEntity entity, MobaActorSpawnSnapshotEntry entry)
        {
            if (entity.TryGetRef(out BattleEntityMetaComponent meta) && meta != null)
            {
                meta.Kind = entry.Kind == (int)SpawnEntityKind.Projectile
                    ? BattleEntityKind.Projectile
                    : BattleEntityKind.Character;
                meta.EntityCode = entry.Code;
            }

            if (entry.Kind == (int)SpawnEntityKind.Projectile
                && entity.TryGetRef(out BattleProjectileComponent projectile)
                && projectile != null)
            {
                projectile.OwnerNetId = new BattleNetId(entry.OwnerNetId);
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
