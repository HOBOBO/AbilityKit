using System;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Demo.Moba.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using AbilityKit.Core.Logging;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.Snapshot
{
    public static class BattleEnterGameApplier
    {
        public static void Apply(BattleContext ctx, BattleEnterGameSnapshot res)
        {
            if (ctx == null) return;
            if (ctx.EntityWorld == null || ctx.EntityLookup == null || ctx.EntityFactory == null)
            {
                Log.Error("[BattleEnterGameApplier] Apply ignored: BattleContext entity wiring not ready.");
                return;
            }

            var localPlayerId = ctx.ResolveLocalControlPlayerId();
            if (!string.IsNullOrEmpty(localPlayerId) &&
                !string.Equals(
                    localPlayerId,
                    res.PlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning(
                    $"[BattleEnterGameApplier] Ignored non-local enter-game actor. " +
                    $"localPlayer={localPlayerId}, responsePlayer={res.PlayerId}, actor={res.LocalActorId}.");
                return;
            }

            if (res.LocalActorId > 0)
            {
                ctx.LocalActorId = res.LocalActorId;
            }

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            var factory = ctx.EntityFactory;

            var dirty = ctx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.IEntityId>(8);
                ctx.DirtyEntities = dirty;
            }
            else
            {
                dirty.Clear();
            }

            if (!res.HasLocalActorPosition)
            {
                return;
            }

            var pos = new Vector3(res.LocalActorX, res.LocalActorY, res.LocalActorZ);

            var localNetId = new AbilityKit.Game.Battle.Entity.BattleNetId(res.LocalActorId);
            if (!lookup.TryResolve(world, localNetId, out var e))
            {
                return;
            }

            if (!e.TryGetRef(out BattleTransformComponent t) || t == null)
            {
                t = new BattleTransformComponent();
                e.WithRef(t);
            }

            t.Position = pos;
            if (t.Forward == default) t.Forward = Vector3.forward;

            dirty.Add(e.Id);
        }
    }
}

