using System;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleEntityFeature : IGamePhaseFeature
    {
        private EC.IECWorld _world;
        private BattleEntityLookup _lookup;
        private BattleEntityFactory _factory;
        private IBattleEntityQuery _query;

        private EC.IEntity _node;
        private long _bindingGeneration;

        public EC.IECWorld World => _world;
        public BattleEntityLookup Lookup => _lookup;
        public BattleEntityFactory Factory => _factory;
        public IBattleEntityQuery Query => _query;

        public void OnAttach(in GamePhaseContext ctx)
        {
            try
            {
                if (ctx.BattleEntities == null)
                    throw new InvalidOperationException("BattleEntityFeature requires a battle entity runtime.");
                var battleCtx = ctx.RequireFeature<BattleContext>(nameof(BattleEntityFeature));

                if (!ctx.BattleEntities.TryGetWorld(out EC.IECWorld world))
                    throw new InvalidOperationException("BattleEntityFeature requires an ECS world.");
                if (!ctx.BattleEntities.TryCreateNode("BattleEntity", out EC.IEntity node))
                    throw new InvalidOperationException("BattleEntityFeature could not create its entity node.");

                _world = world;
                _lookup = new BattleEntityLookup();
                _node = node;
                _factory = new BattleEntityFactory(_world, _lookup, _node);
                _query = new BattleEntityQuery(_world, _lookup);
                if (_node.IsValid)
                {
                    _node.WithRef(_lookup);
                    _node.WithRef(_factory);
                    _node.WithRef(_query);
                }

                _bindingGeneration = battleCtx.BindEntityRuntime(
                    _node, _world, _lookup, _factory, _query);
            }
            catch (Exception attachFailure)
            {
                try { OnDetach(ctx); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Entity feature attach and rollback failed.",
                        attachFailure, cleanupFailure);
                }
                throw;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            var context = ctx;
            BattleFeatureResourceCleanup.ReleaseAll(
                () =>
                {
                    if (_bindingGeneration != 0 &&
                        context.Features.TryGet(out BattleContext battleCtx))
                        battleCtx.ClearEntityRuntime(_bindingGeneration);
                    _bindingGeneration = 0;
                },
                () =>
                {
                    if (_node.IsValid) context.BattleEntities?.DestroyTree(_node);
                    _node = default;
                },
                () => { _lookup?.Clear(); _lookup = null; },
                () => { _world = null; _factory = null; _query = null; });
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
