using System;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
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

            Vector3 pos = default;
            if (res.Payload != null && res.Payload.Length >= 12)
            {
                var x = BitConverter.ToSingle(res.Payload, 0);
                var y = BitConverter.ToSingle(res.Payload, 4);
                var z = BitConverter.ToSingle(res.Payload, 8);
                pos = new Vector3(x, y, z);
            }

            var netId = new BattleNetId(res.LocalActorId);
            if (!lookup.TryResolve(world, netId, out var e))
            {
                e = factory.CreateCharacter(netId);
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
