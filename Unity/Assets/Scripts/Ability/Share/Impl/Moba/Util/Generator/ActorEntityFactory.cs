using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    // 通用实体创建工具（用于创建 ActorEntity 并初始化常用组件）
    // 设计目标：
    // 1) 逻辑层可用（不依赖 Unity）
    // 2) 便于扩展（新增组件时只需要新增一个 WithXxx 方法）
    public static class ActorEntityFactory
    {
        // 创建一个基础 ActorEntity（不带任何组件），后续可用 Builder 链式初始化
        public static ActorEntityBuilder Create(ActorContext context)
        {
            var entity = context.CreateEntity();
            return new ActorEntityBuilder(entity);
        }

        // 创建一个带 Transform 的 ActorEntity
        public static ActorEntity CreateWithTransform(ActorContext context, in Transform3 transform)
        {
            return Create(context)
                .WithTransform(transform)
                .Build();
        }
    }

    // ActorEntity 的链式初始化器
    public readonly struct ActorEntityBuilder
    {
        private readonly ActorEntity _entity;

        public ActorEntityBuilder(ActorEntity entity)
        {
            _entity = entity;
        }

        // 获取当前实体
        public ActorEntity Entity => _entity;

        // 初始化或替换 Transform 组件
        public ActorEntityBuilder WithTransform(in Transform3 transform)
        {
            if (_entity.hasTransform) _entity.ReplaceTransform(transform);
            else _entity.AddTransform(transform);
            return this;
        }

        // 初始化或替换 Collider（LocalShape）组件
        public ActorEntityBuilder WithCollider(in ColliderShape localShape)
        {
            if (_entity.hasCollider) _entity.ReplaceCollider(localShape);
            else _entity.AddCollider(localShape);
            return this;
        }

        // 初始化或替换碰撞层（用于查询过滤）
        public ActorEntityBuilder WithCollisionLayer(int layerMask)
        {
            if (_entity.hasCollisionLayer) _entity.ReplaceCollisionLayer(layerMask);
            else _entity.AddCollisionLayer(layerMask);
            return this;
        }

        // 初始化或替换逻辑层碰撞系统返回的 CollisionId
        public ActorEntityBuilder WithCollisionId(in ColliderId id)
        {
            if (_entity.hasCollisionId) _entity.ReplaceCollisionId(id);
            else _entity.AddCollisionId(id);
            return this;
        }

        // 如果组件存在则移除 Collider
        public ActorEntityBuilder WithoutCollider()
        {
            if (_entity.hasCollider) _entity.RemoveCollider();
            return this;
        }

        // 如果组件存在则移除 CollisionId
        public ActorEntityBuilder WithoutCollisionId()
        {
            if (_entity.hasCollisionId) _entity.RemoveCollisionId();
            return this;
        }

        // 构建（链式风格下的终结调用）
        public ActorEntity Build()
        {
            return _entity;
        }
    }
}
