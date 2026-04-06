using System;

namespace AbilityKit.World.ECS
{
    #region 生命周期事件

    /// <summary>实体创建事件。</summary>
    public struct EntityCreated
    {
        public IEntityId EntityId;
        public IEntity Entity;
        public string Name;

        public EntityCreated(IEntityId entityId, IEntity entity, string name)
        {
            EntityId = entityId;
            Entity = entity;
            Name = name;
        }

        public bool Equals(EntityCreated other) => EntityId.Equals(other.EntityId);
        public override bool Equals(object obj) => obj is EntityCreated other && Equals(other);
        public override int GetHashCode() => EntityId.GetHashCode();
    }

    /// <summary>实体销毁事件。</summary>
    public struct EntityDestroyed
    {
        public IEntityId EntityId;

        public EntityDestroyed(IEntityId entityId)
        {
            EntityId = entityId;
        }

        public bool Equals(EntityDestroyed other) => EntityId.Equals(other.EntityId);
        public override bool Equals(object obj) => obj is EntityDestroyed other && Equals(other);
        public override int GetHashCode() => EntityId.GetHashCode();
    }

    #endregion

    #region 组件事件

    /// <summary>组件设置事件。</summary>
    public struct ComponentSet
    {
        public IEntityId EntityId;
        public int ComponentTypeId;
        public object Component;

        public ComponentSet(IEntityId entityId, int componentTypeId, object component)
        {
            EntityId = entityId;
            ComponentTypeId = componentTypeId;
            Component = component;
        }

        public bool Equals(ComponentSet other) => EntityId.Equals(other.EntityId) && ComponentTypeId == other.ComponentTypeId;
        public override bool Equals(object obj) => obj is ComponentSet other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EntityId, ComponentTypeId);
    }

    /// <summary>组件移除事件。</summary>
    public struct ComponentRemoved
    {
        public IEntityId EntityId;
        public int ComponentTypeId;

        public ComponentRemoved(IEntityId entityId, int componentTypeId)
        {
            EntityId = entityId;
            ComponentTypeId = componentTypeId;
        }

        public bool Equals(ComponentRemoved other) => EntityId.Equals(other.EntityId) && ComponentTypeId == other.ComponentTypeId;
        public override bool Equals(object obj) => obj is ComponentRemoved other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EntityId, ComponentTypeId);
    }

    #endregion

    #region 层级事件

    /// <summary>父子关系变化事件。</summary>
    public struct ParentChanged
    {
        public IEntityId ChildId;
        public IEntityId OldParentId;
        public IEntityId NewParentId;
        public bool HasOldParent;

        public ParentChanged(IEntityId childId, IEntityId newParentId)
        {
            ChildId = childId;
            OldParentId = default(IEntityId);
            NewParentId = newParentId;
            HasOldParent = false;
        }

        public ParentChanged(IEntityId childId, IEntityId oldParentId, IEntityId newParentId)
        {
            ChildId = childId;
            OldParentId = oldParentId;
            NewParentId = newParentId;
            HasOldParent = true;
        }

        public bool Equals(ParentChanged other) => ChildId.Equals(other.ChildId);
        public override bool Equals(object obj) => obj is ParentChanged other && Equals(other);
        public override int GetHashCode() => ChildId.GetHashCode();
    }

    #endregion
}
