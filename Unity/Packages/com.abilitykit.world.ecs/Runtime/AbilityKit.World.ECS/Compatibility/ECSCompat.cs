using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;

// Legacy namespace alias for backward compatibility
namespace AbilityKit.Ability.Share.ECS
{
    /// <summary>
    /// [兼容性别名] 请使用 <see cref="AbilityKit.World.ECS.IEntityId"/> 代替。
    /// </summary>
    [System.Obsolete("Use AbilityKit.World.ECS.IEntityId instead.")]
    public readonly struct IEntityId
    {
        public readonly int ActorId;

        public IEntityId(int actorId)
        {
            ActorId = actorId;
        }

        public bool IsValid => ActorId > 0;

        public override string ToString() => ActorId.ToString();

        public static explicit operator global::AbilityKit.World.ECS.IEntityId(IEntityId id) =>
            new global::AbilityKit.World.ECS.IEntityId(id.ActorId, 0);
    }

    /// <summary>
    /// [兼容性别名] 请使用 <see cref="AbilityKit.World.ECS.IEntityId"/> 代替。
    /// </summary>
    [System.Obsolete("Use AbilityKit.World.ECS.IEntityId instead.")]
    public readonly struct EcsEntityId
    {
        public readonly int ActorId;

        public EcsEntityId(int actorId) => ActorId = actorId;
        public EcsEntityId(string actorId) => ActorId = int.Parse(actorId);
        public bool IsValid => ActorId > 0;
        public override string ToString() => ActorId.ToString();
    }

    /// <summary>
    /// [兼容性别名] 请使用 <see cref="AbilityKit.World.ECS.IEntity"/> 代替。
    /// </summary>
    [System.Obsolete("Use AbilityKit.World.ECS.IEntity instead.")]
    public readonly struct IEntity
    {
        public readonly IEntityId Id;
        public IEntity(IEntityId id) => Id = id;
        public bool IsValid => Id.IsValid;
        public override string ToString() => Id.ToString();
    }

    /// <summary>
    /// [兼容性别名] ComponentTypeId 功能已移除。
    /// </summary>
    [System.Obsolete("ComponentTypeId is deprecated.")]
    public static class ComponentTypeId
    {
        public static int GetId<T>() where T : struct => 0;
    }
}

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// [兼容性别名] 请使用 <see cref="AbilityKit.World.ECS.IEntity"/> 代替。
    /// </summary>
    [System.Obsolete("Use AbilityKit.World.ECS.IEntity instead.")]
    public readonly struct Entity
    {
        public readonly global::AbilityKit.World.ECS.IEntityId Id;
        public readonly IECWorld World;

        public Entity(global::AbilityKit.World.ECS.IEntityId id, IECWorld world)
        {
            Id = id;
            World = world;
        }

        public Entity(global::AbilityKit.World.ECS.IEntity entity)
        {
            Id = entity.Id;
            World = entity.World;
        }

        public bool IsValid => World != null && World.IsAlive(Id);
        public override string ToString() => Id.ToString();
    }

    /// <summary>
    /// [兼容性别名] 请使用 <see cref="AbilityKit.World.ECS.IECWorld"/> 代替。
    /// </summary>
    [System.Obsolete("Use AbilityKit.World.ECS.IECWorld instead.")]
    public interface IEntityWorld : IECWorld { }

    /// <summary>
    /// [兼容性别别名] 扩展方法，为 Entity 提供旧 API 兼容。
    /// </summary>
    public static class EntityExtensions
    {
        public static bool TryGetComponent<T>(this Entity entity, out T component) where T : class
        {
            component = default(T);
            if (entity.World == null) return false;
            return entity.World.TryGetComponentRef(entity.Id, out component);
        }

        public static bool TryGetRef<T>(this Entity entity, out T component) where T : class
        {
            component = default(T);
            if (entity.World == null) return false;
            return entity.World.TryGetComponentRef(entity.Id, out component);
        }

        public static T GetComponent<T>(this Entity entity) where T : class
        {
            if (entity.World == null) return default;
            return entity.World.GetComponentRef<T>(entity.Id);
        }

        public static Entity WithRef<T>(this Entity entity, T component) where T : class
        {
            if (entity.World != null)
                entity.World.SetComponentRef(entity.Id, component);
            return entity;
        }

        public static Entity With<T>(this Entity entity, T component) where T : struct
        {
            if (entity.World != null)
                entity.World.SetComponent(entity.Id, component);
            return entity;
        }

        public static Entity AddChild(this Entity entity)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.CreateChild(parentEntity);
            return new Entity(child.Id, entity.World);
        }

        public static Entity AddChild(this Entity entity, string debugName)
        {
            if (entity.World == null) return default;
            var child = entity.World.Create(debugName);
            var parentEntity = entity.World.Wrap(entity.Id);
            child.SetParent(parentEntity);
            return new Entity(child.Id, entity.World);
        }

        public static Entity AddChild(this Entity entity, int childId)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.CreateChild(parentEntity, childId);
            return new Entity(child.Id, entity.World);
        }

        public static Entity AddChild(this Entity entity, int childId, string debugName)
        {
            if (entity.World == null) return default;
            var child = entity.World.Create(debugName);
            var parentEntity = entity.World.Wrap(entity.Id);
            child.SetParent(parentEntity, childId);
            return new Entity(child.Id, entity.World);
        }

        public static Entity GetChildById(this Entity entity, int id)
        {
            if (entity.World == null) return default;
            entity.World.TryGetChildById(entity.Id, id, out var child);
            return new Entity(child.Id, entity.World);
        }

        public static bool TryGetChildById(this Entity entity, int id, out Entity child)
        {
            child = default;
            if (entity.World == null) return false;
            if (entity.World.TryGetChildById(entity.Id, id, out var c))
            {
                child = new Entity(c.Id, entity.World);
                return true;
            }
            return false;
        }

        public static Entity RemoveComponent<T>(this Entity entity) where T : struct
        {
            if (entity.World != null)
                entity.World.RemoveComponent<T>(entity.Id);
            return entity;
        }

        public static int ChildCount(this Entity entity)
        {
            if (entity.World == null) return 0;
            return entity.World.GetChildCount(entity.Id);
        }

        public static Entity GetChild(this Entity entity, int index)
        {
            if (entity.World == null) return default;
            var child = entity.World.GetChild(entity.Id, index);
            return new Entity(child.Id, entity.World);
        }

        public static void Destroy(this Entity entity)
        {
            if (entity.World != null)
                entity.World.Destroy(entity.Id);
        }
    }

    /// <summary>
    /// [兼容性别名] 扩展方法。
    /// </summary>
    public static class IEntityExtensions
    {
        public static bool TryGetComponent<T>(this IEntity entity, out T component) where T : class
        {
            component = default(T);
            if (entity.World != null)
                return entity.World.TryGetComponentRef(entity.Id, out component);
            return false;
        }

        public static bool TryGetRef<T>(this IEntity entity, out T component) where T : class
        {
            return TryGetComponent(entity, out component);
        }

        public static T GetComponent<T>(this IEntity entity) where T : class
        {
            if (entity.World != null)
                return entity.World.GetComponentRef<T>(entity.Id);
            return default(T);
        }

        public static T GetRef<T>(this IEntity entity) where T : class
        {
            return GetComponent<T>(entity);
        }

        public static IEntity WithRef<T>(this IEntity entity, T component) where T : class
        {
            if (entity.World != null)
                entity.World.SetComponentRef(entity.Id, component);
            return entity;
        }

        public static IEntity With<T>(this IEntity entity, T component) where T : struct
        {
            if (entity.World != null)
                entity.World.SetComponent(entity.Id, component);
            return entity;
        }

        public static IEntity AddChild(this IEntity entity)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.CreateChild(parentEntity);
            return child;
        }

        public static IEntity AddChild(this IEntity entity, string debugName)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.Create(debugName);
            child.SetParent(parentEntity);
            return child;
        }

        public static IEntity AddChild(this IEntity entity, int childId)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.CreateChild(parentEntity, childId);
            return child;
        }

        public static IEntity AddChild(this IEntity entity, int childId, string debugName)
        {
            if (entity.World == null) return default;
            var parentEntity = entity.World.Wrap(entity.Id);
            var child = entity.World.Create(debugName);
            child.SetParent(parentEntity, childId);
            return child;
        }

        public static IEntity GetChildById(this IEntity entity, int id)
        {
            if (entity.World == null) return default;
            entity.World.TryGetChildById(entity.Id, id, out var child);
            return child;
        }

        public static bool TryGetChildById(this IEntity entity, int id, out IEntity child)
        {
            if (entity.World == null)
            {
                child = default;
                return false;
            }
            return entity.World.TryGetChildById(entity.Id, id, out child);
        }

        public static bool TryGetParent(this IEntity entity, out IEntity parent)
        {
            if (entity.World == null)
            {
                parent = default;
                return false;
            }
            parent = entity.World.GetParent(entity.Id);
            return parent.IsValid;
        }

        public static void RemoveComponent<T>(this IEntity entity) where T : struct
        {
            if (entity.World != null)
                entity.World.RemoveComponent<T>(entity.Id);
        }

        public static void RemoveComponent(this IEntity entity, Type componentType)
        {
            if (entity.World != null && componentType != null)
            {
                // Only handle value types (structs) via reflection
                // Reference types and interfaces cannot satisfy the 'where T : struct' constraint
                if (componentType.IsValueType && !IsNullableType(componentType))
                {
                    var method = typeof(IEntityExtensions).GetMethod("RemoveComponentGeneric",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        var generic = method.MakeGenericMethod(componentType);
                        generic.Invoke(null, new object[] { entity });
                    }
                }
            }
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static void RemoveComponentGeneric<T>(this IEntity entity) where T : struct
        {
            if (entity.World != null)
                entity.World.RemoveComponent<T>(entity.Id);
        }

        public static int ChildCount(this IEntity entity)
        {
            if (entity.World == null) return 0;
            return entity.World.GetChildCount(entity.Id);
        }

        public static IEntity GetChild(this IEntity entity, int index)
        {
            if (entity.World == null) return default;
            return entity.World.GetChild(entity.Id, index);
        }

        public static void Destroy(this IEntity entity)
        {
            if (entity.World != null)
                entity.World.Destroy(entity.Id);
        }
    }

    /// <summary>
    /// [兼容性别名] 扩展方法。
    /// </summary>
    public static class IECWorldExtensions
    {
        public static bool TryGetName(this IECWorld world, global::AbilityKit.World.ECS.IEntityId id, out string name)
        {
            name = world?.GetName(id);
            return !string.IsNullOrEmpty(name);
        }

        public static IDisposable EntityCreated(this IECWorld world, Action<EntityCreated> handler)
        {
            return world?.Events.OnEntityCreated(handler) ?? EmptyDisposable.Instance;
        }

        public static IDisposable EntityDestroyed(this IECWorld world, Action<EntityDestroyed> handler)
        {
            return world?.Events.OnEntityDestroyed(handler) ?? EmptyDisposable.Instance;
        }

        public static IDisposable ParentChanged(this IECWorld world, Action<ParentChanged> handler)
        {
            return world?.Events.OnParentChanged(handler) ?? EmptyDisposable.Instance;
        }

        public static IDisposable ComponentSet(this IECWorld world, Action<ComponentSet> handler)
        {
            return world?.Events.OnComponentSet(handler) ?? EmptyDisposable.Instance;
        }

        public static IDisposable ComponentRemoved(this IECWorld world, Action<ComponentRemoved> handler)
        {
            return world?.Events.OnComponentRemoved(handler) ?? EmptyDisposable.Instance;
        }

        public static bool TryGetChildById(this IECWorld world, IEntityId parent, int childId, out IEntity child)
        {
            child = default;
            if (world == null) return false;
            return world.TryGetChildById(parent, childId, out child);
        }

        public static IEntity Create(this IECWorld world, string name)
        {
            return world?.Create(name) ?? default;
        }

        public static IEntity CreateChild(this IECWorld world, IEntity parent)
        {
            if (world == null) return default;
            return world.CreateChild(parent);
        }

        public static IEntity CreateChild(this IECWorld world, IEntity parent, int logicalChildId)
        {
            if (world == null) return default;
            return world.CreateChild(parent, logicalChildId);
        }

        public static bool TryGetChildId(this IECWorld world, IEntityId parent, IEntityId child, out int childId)
        {
            childId = 0;
            if (world == null) return false;
            // Check if this child actually belongs to the parent
            var actualParent = world.GetParent(child);
            if (actualParent.IsValid && actualParent.Id == parent)
            {
                // Try to find the child's logical ID by iterating children
                var count = world.GetChildCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var c = world.GetChild(parent, i);
                    if (c.IsValid && c.Id == child)
                    {
                        childId = i; // Fallback to index
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 空的可释放对象。
    /// </summary>
    public sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new EmptyDisposable();
        private EmptyDisposable() { }
        public void Dispose() { }
    }
}
