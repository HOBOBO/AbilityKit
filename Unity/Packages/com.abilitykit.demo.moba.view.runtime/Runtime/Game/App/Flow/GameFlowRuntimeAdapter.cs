using System;
using AbilityKit.Game.EntityCreation;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class GameFlowRuntimeAdapter
    {
        public GameFlowRuntimeAdapter(IEntity root)
        {
            if (!root.IsValid)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Root = root;
            Features = new EntityFeatureStore(root);
            FeatureBinder = new EntityFeatureBinder(root);
            BattleEntities = new BattleEntityRuntime(root);
        }

        public IEntity Root { get; }

        public IGameFeatureStore Features { get; }

        public IFeatureBinder FeatureBinder { get; }

        public IBattleEntityRuntime BattleEntities { get; }

        private sealed class EntityFeatureStore : IGameFeatureStore
        {
            private readonly IEntity _entity;

            public EntityFeatureStore(IEntity entity)
            {
                _entity = entity;
            }

            public bool TryGet<T>(out T component) where T : class => _entity.TryGetRef(out component);
            public void Set<T>(T component) where T : class => _entity.WithRef(component);
            public void Remove<T>() where T : class => _entity.RemoveComponent(typeof(T));
            public void Remove(Type componentType) => _entity.RemoveComponent(componentType);
        }

        private sealed class EntityFeatureBinder : IFeatureBinder
        {
            private readonly IEntity _entity;

            public EntityFeatureBinder(IEntity entity)
            {
                _entity = entity;
            }

            public void AttachFeature(object feature)
            {
                if (feature == null) throw new ArgumentNullException(nameof(feature));
                _entity.WithRef(feature.GetType(), feature);
            }

            public void DetachFeature(object feature) => _entity.RemoveComponent(feature.GetType());
        }

        private sealed class BattleEntityRuntime : IBattleEntityRuntime
        {
            private readonly IEntity _root;

            public BattleEntityRuntime(IEntity root)
            {
                _root = root;
            }

            public bool TryGetWorld<TWorld>(out TWorld world)
            {
                if (_root.World is TWorld typedWorld)
                {
                    world = typedWorld;
                    return true;
                }

                world = default;
                return false;
            }

            public bool TryCreateNode<TNode>(string debugName, out TNode node)
            {
                var entity = EntityGenerator.CreateChild(_root, debugName: debugName);
                if (entity is TNode typedNode)
                {
                    node = typedNode;
                    return true;
                }

                node = default;
                return false;
            }

            public void DestroyTree<TNode>(TNode root)
            {
                if (root is not IEntity entity) return;
                DestroyTree(entity);
            }

            private static void DestroyTree(IEntity root)
            {
                if (!root.IsValid) return;

                var list = new System.Collections.Generic.List<IEntity>(16);
                var stack = new System.Collections.Generic.Stack<IEntity>();
                stack.Push(root);

                while (stack.Count > 0)
                {
                    var entity = stack.Pop();
                    if (!entity.IsValid) continue;
                    list.Add(entity);

                    var count = entity.ChildCount;
                    for (int i = 0; i < count; i++)
                    {
                        stack.Push(entity.GetChild(i));
                    }
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var entity = list[i];
                    if (entity.IsValid) entity.Destroy();
                }
            }
        }
    }
}
