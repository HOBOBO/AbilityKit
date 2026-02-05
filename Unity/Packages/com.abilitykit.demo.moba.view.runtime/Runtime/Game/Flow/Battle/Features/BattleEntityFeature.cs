using System;
using EC = AbilityKit.Ability.EC;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleEntityFeature : IGamePhaseFeature
    {
        private EC.EntityWorld _world;
        private BattleEntityLookup _lookup;
        private BattleEntityFactory _factory;
        private IBattleEntityQuery _query;

        private EC.Entity _node;

        public EC.EntityWorld World => _world;
        public BattleEntityLookup Lookup => _lookup;
        public BattleEntityFactory Factory => _factory;
        public IBattleEntityQuery Query => _query;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (!ctx.Root.IsValid) return;

            if (!ctx.Root.TryGetComponent(out BattleContext battleCtx) || battleCtx == null) return;

            _world = ctx.Root.World;

            _lookup = new BattleEntityLookup();
            _node = EntityGenerator.CreateChild(ctx.Root, debugName: "BattleEntity");
            _factory = new BattleEntityFactory(_world, _lookup, _node);
            _query = new BattleEntityQuery(_world, _lookup);
            if (_node.IsValid)
            {
                _node.AddComponent(_lookup);
                _node.AddComponent(_factory);
                _node.AddComponent(_query);
            }

            battleCtx.EntityNode = _node;
            battleCtx.EntityWorld = _world;
            battleCtx.EntityLookup = _lookup;
            battleCtx.EntityFactory = _factory;
            battleCtx.EntityQuery = _query;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Root.IsValid && ctx.Root.TryGetComponent(out BattleContext battleCtx) && battleCtx != null)
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

        private static void DestroyTree(EC.Entity root)
        {
            if (!root.IsValid) return;

            // EntityWorld.Destroy will detach children without destroying them.
            // So we must collect the full subtree first, then destroy bottom-up.
            var list = new System.Collections.Generic.List<EC.Entity>(16);
            var stack = new System.Collections.Generic.Stack<EC.Entity>();
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
