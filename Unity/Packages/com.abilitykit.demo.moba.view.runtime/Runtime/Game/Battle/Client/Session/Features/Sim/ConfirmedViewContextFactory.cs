using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal static class ConfirmedViewContextFactory
    {
        public static BattleContext Create(BattleContext sourceCtx, WorldId authWorldId)
        {
            var ctx = BattleContext.Rent();
            ctx.Plan = sourceCtx != null ? sourceCtx.Plan : default;
            ctx.Session = null;
            ctx.RuntimeWorldId = authWorldId;
            ctx.HasRuntimeWorldId = true;

            CreateEntityRuntime(ctx);
            return ctx;
        }

        private static void CreateEntityRuntime(BattleContext ctx)
        {
            var viewWorld = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var node = viewWorld.Create("BattleEntity__confirmed");
            var entityFactory = new BattleEntityFactory(viewWorld, lookup, node);
            var query = new BattleEntityQuery(viewWorld, lookup);

            if (node.IsValid)
            {
                node.WithRef(lookup);
                node.WithRef(entityFactory);
                node.WithRef(query);
            }

            ctx.BindEntityRuntime(
                node,
                viewWorld,
                lookup,
                entityFactory,
                query,
                new List<IEntityId>(128));
        }
    }
}
