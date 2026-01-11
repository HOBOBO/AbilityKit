using System;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow.Snapshot
{
    public static class BattleEnterGameApplier
    {
        public static void Apply(BattleContext ctx, EnterMobaGameRes res)
        {
            if (ctx == null) return;
            if (ctx.EntityWorld == null || ctx.EntityLookup == null || ctx.EntityFactory == null) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            var factory = ctx.EntityFactory;

            var dirty = ctx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.EntityId>(8);
                ctx.DirtyEntities = dirty;
            }
            else
            {
                dirty.Clear();
            }

            if (!EnterMobaGamePayloadCodec.TryDeserializePosition(res.OpCode, res.Payload, out var p))
            {
                return;
            }

            var pos = new Vector3(p.X, p.Y, p.Z);

            var localNetId = new BattleNetId(res.LocalActorId);
            if (!lookup.TryResolve(world, localNetId, out var e))
            {
                return;
            }

            if (!e.TryGetComponent(out BattleTransformComponent t) || t == null)
            {
                t = new BattleTransformComponent();
                e.AddComponent(t);
            }

            t.Position = pos;
            if (t.Forward == default) t.Forward = Vector3.forward;

            dirty.Add(e.Id);
        }
    }
}
