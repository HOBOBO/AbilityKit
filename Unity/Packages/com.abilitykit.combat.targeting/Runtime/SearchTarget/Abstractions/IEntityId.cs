namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 实体 ID 抽象接口
    /// 核心层不依赖具体 ECS 实现
    /// </summary>
    public interface IEntityId
    {
        int ActorId { get; }
        bool IsValid { get; }
    }

    /// <summary>
    /// 默认实体 ID 实现
    /// </summary>
    public readonly struct EntityId : IEntityId
    {
        public int ActorId { get; }
        public bool IsValid => ActorId != 0;

        public EntityId(int actorId)
        {
            ActorId = actorId;
        }

        public static readonly EntityId Invalid = new EntityId(0);
        public static implicit operator EntityId(int actorId) => new EntityId(actorId);
        public static implicit operator int(EntityId id) => id.ActorId;
    }
}
