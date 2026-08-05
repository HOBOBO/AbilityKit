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

        public EC.IECWorld World => _world;
        public BattleEntityLookup Lookup => _lookup;
        public BattleEntityFactory Factory => _factory;
        public IBattleEntityQuery Query => _query;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (ctx.BattleEntities == null) return;
            if (!ctx.Features.TryGet(out BattleContext battleCtx)) return;

            if (!ctx.BattleEntities.TryGetWorld(out EC.IECWorld world)) return;
            if (!ctx.BattleEntities.TryCreateNode("BattleEntity", out EC.IEntity node)) return;

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

            battleCtx.EntityNode = _node;
            battleCtx.EntityWorld = _world;
            battleCtx.EntityLookup = _lookup;
            battleCtx.EntityFactory = _factory;
            battleCtx.EntityQuery = _query;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Features.TryGet(out BattleContext battleCtx))
            {
                battleCtx.EntityNode = default;
                battleCtx.EntityWorld = null;
                battleCtx.EntityLookup = null;
                battleCtx.EntityFactory = null;
                battleCtx.EntityQuery = null;
            }

            if (_node.IsValid)
            {
                ctx.BattleEntities?.DestroyTree(_node);
            }

            _lookup?.Clear();
            _world = null;
            _lookup = null;
            _factory = null;
            _query = null;
            _node = default;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
