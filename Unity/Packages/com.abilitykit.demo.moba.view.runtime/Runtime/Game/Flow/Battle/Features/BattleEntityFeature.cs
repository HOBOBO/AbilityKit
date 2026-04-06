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
            if (!ctx.Root.IsValid) return;

            if (!ctx.Root.TryGetRef(out BattleContext battleCtx)) return;

            _world = ctx.Root.World;

            _lookup = new BattleEntityLookup();
            _node = EntityGenerator.CreateChild(ctx.Root, debugName: "BattleEntity");
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
            if (ctx.Root.IsValid && ctx.Root.TryGetRef(out BattleContext battleCtx))
            {
                battleCtx.EntityNode = default;
                battleCtx.EntityWorld = null;
                battleCtx.EntityLookup = null;
                battleCtx.EntityFactory = null;
                battleCtx.EntityQuery = null;
            }

            if (_node.IsValid)
            {
                DestroyTree(_node);
            }

            _lookup?.Clear();
            _world = null;
            _lookup = null;
            _factory = null;
            _query = null;
            _node = default;
        }

        private static void DestroyTree(EC.IEntity root)
        {
            if (!root.IsValid) return;

            var list = new System.Collections.Generic.List<EC.IEntity>(16);
            var stack = new System.Collections.Generic.Stack<EC.IEntity>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (!e.IsValid) continue;
                list.Add(e);

                var count = e.ChildCount;
                for (int i = 0; i < count; i++)
                {
                    stack.Push(e.GetChild(i));
                }
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.IsValid) e.Destroy();
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
