using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 实体世界接口，定义 ECS 的核心操作。
    /// 所有实体世界实现都应实现此接口。
    /// </summary>
    public interface IECWorld
    {
        // ============ 实体生命周期 ============

        /// <summary>创建空实体。</summary>
        IEntity Create();

        /// <summary>创建带名称的实体（用于调试）。</summary>
        IEntity Create(string name);

        /// <summary>创建子实体。</summary>
        IEntity CreateChild(IEntity parent);

        /// <summary>创建带逻辑ID的子实体。</summary>
        IEntity CreateChild(IEntity parent, int logicalChildId);

        /// <summary>销毁实体（不递归）。</summary>
        void Destroy(IEntityId id);

        /// <summary>递归销毁实体及其所有子实体。</summary>
        void DestroyRecursive(IEntityId id);

        /// <summary>检查实体是否存活。</summary>
        bool IsAlive(IEntityId id);

        /// <summary>将ID包装为实体句柄。</summary>
        IEntity Wrap(IEntityId id);

        // ============ 组件操作 ============

        /// <summary>设置值类型组件。</summary>
        void SetComponent<T>(IEntityId id, T component) where T : struct;

        /// <summary>设置引用类型组件（null表示移除）。</summary>
        void SetComponentRef<T>(IEntityId id, T component) where T : class;

        /// <summary>获取值类型组件（未找到时返回default）。</summary>
        T GetComponent<T>(IEntityId id) where T : struct;

        /// <summary>获取引用类型组件（未找到时返回null）。</summary>
        T GetComponentRef<T>(IEntityId id) where T : class;

        /// <summary>尝试获取值类型组件。</summary>
        bool TryGetComponent<T>(IEntityId id, out T component) where T : struct;

        /// <summary>尝试获取引用类型组件。</summary>
        bool TryGetComponentRef<T>(IEntityId id, out T component) where T : class;

        /// <summary>检查是否拥有组件（值类型）。</summary>
        bool HasComponent<T>() where T : struct;

        /// <summary>移除组件。</summary>
        bool RemoveComponent<T>(IEntityId id) where T : struct;

        // ============ 查询 ============

        /// <summary>查询拥有指定组件类型的所有存活实体。</summary>
        EntityQuery<T> Query<T>() where T : struct;

        /// <summary>查询拥有两个指定组件类型的实体。</summary>
        EntityQuery<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct;

        /// <summary>查询拥有三个指定组件类型的实体。</summary>
        EntityQuery<T1, T2, T3> Query<T1, T2, T3>() where T1 : struct where T2 : struct where T3 : struct;

        /// <summary>遍历所有存活实体。</summary>
        void ForEachAlive(Action<IEntity> visitor);

        /// <summary>遍历实体的所有组件（用于调试/编辑器）。</summary>
        void ForEachComponent(IEntityId id, Action<int, object> visitor);

        // ============ 父子关系 ============

        /// <summary>获取父实体（无父级返回null）。</summary>
        IEntity GetParent(IEntityId id);

        /// <summary>设置父实体。</summary>
        void SetParent(IEntityId child, IEntityId parent);

        /// <summary>设置带逻辑ID的父子关系。</summary>
        void SetParent(IEntityId child, IEntityId parent, int logicalChildId);

        /// <summary>获取子实体数量。</summary>
        int GetChildCount(IEntityId id);

        /// <summary>获取指定索引的子实体。</summary>
        IEntity GetChild(IEntityId id, int index);

        /// <summary>尝试通过逻辑ID获取子实体。</summary>
        bool TryGetChildById(IEntityId parent, int logicalChildId, out IEntity child);

        // ============ 元数据 ============

        /// <summary>获取实体名称（调试用）。</summary>
        string GetName(IEntityId id);

        /// <summary>设置实体名称（调试用）。</summary>
        void SetName(IEntityId id, string name);

        // ============ 统计 ============

        /// <summary>当前存活实体数量。</summary>
        int AliveCount { get; }

        /// <summary>已分配实体槽位总数（含已销毁）。</summary>
        int TotalCapacity { get; }

        // ============ 事件 ============

        /// <summary>事件总线。</summary>
        IWorldEventBus Events { get; }
    }
}
