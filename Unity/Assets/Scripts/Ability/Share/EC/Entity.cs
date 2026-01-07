using System;

namespace AbilityKit.Ability.EC
{
    public readonly struct Entity : IEquatable<Entity>
    {
        private readonly EntityWorld _world;
        public readonly EntityId Id;

        internal Entity(EntityWorld world, EntityId id)
        {
            _world = world;
            Id = id;
        }

        public bool IsValid => _world != null && _world.IsAlive(Id);

        public EntityWorld World => _world;

        public Entity GetParent()
        {
            return _world.GetParent(Id);
        }

        public bool TryGetParent(out Entity parent)
        {
            return _world.TryGetParent(Id, out parent);
        }

        public void SetParent(Entity parent)
        {
            _world.SetParent(Id, parent.Id);
        }

        public void SetParent(Entity parent, int childId)
        {
            _world.SetParent(Id, parent.Id, childId);
        }

        public Entity AddChild()
        {
            return _world.CreateChild(Id);
        }

        public Entity AddChild(string name)
        {
            return _world.CreateChild(Id, name);
        }

        public Entity AddChild(int childId)
        {
            return _world.CreateChild(Id, childId);
        }

        public Entity AddChild(int childId, string name)
        {
            return _world.CreateChild(Id, childId, name);
        }

        public Entity AddChild<T1>(int childId, T1 arg1, Action<Entity, T1> init)
        {
            return _world.CreateChild(Id, childId, arg1, init);
        }

        public Entity AddChild<T1, T2>(int childId, T1 arg1, T2 arg2, Action<Entity, T1, T2> init)
        {
            return _world.CreateChild(Id, childId, arg1, arg2, init);
        }

        public Entity AddChild<T1, T2, T3>(int childId, T1 arg1, T2 arg2, T3 arg3, Action<Entity, T1, T2, T3> init)
        {
            return _world.CreateChild(Id, childId, arg1, arg2, arg3, init);
        }

        public Entity AddChild<T1, T2, T3, T4>(int childId, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<Entity, T1, T2, T3, T4> init)
        {
            return _world.CreateChild(Id, childId, arg1, arg2, arg3, arg4, init);
        }

        public Entity AddChild<T1, T2, T3, T4, T5>(int childId, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, Action<Entity, T1, T2, T3, T4, T5> init)
        {
            return _world.CreateChild(Id, childId, arg1, arg2, arg3, arg4, arg5, init);
        }

        public void AddChild(Entity child)
        {
            _world.SetParent(child.Id, Id);
        }

        public void AddChild(Entity child, int childId)
        {
            _world.SetParent(child.Id, Id, childId);
        }

        public int ChildCount => _world.GetChildCount(Id);

        public Entity GetChild(int index)
        {
            return _world.GetChild(Id, index);
        }

        public bool TryGetChildById(int childId, out Entity child)
        {
            return _world.TryGetChildById(Id, childId, out child);
        }

        public Entity GetChildById(int childId)
        {
            return _world.GetChildById(Id, childId);
        }

        public void AddComponent<T>(T component) where T : class
        {
            _world.SetComponent(Id, component);
        }

        public void AddComponent(object component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _world.SetComponent(Id, component.GetType(), component);
        }

        public T GetComponent<T>() where T : class
        {
            return _world.GetComponent<T>(Id);
        }

        public bool TryGetComponent<T>(out T component) where T : class
        {
            return _world.TryGetComponent(Id, out component);
        }

        public bool HasComponent<T>() where T : class
        {
            return _world.HasComponent<T>(Id);
        }

        public bool RemoveComponent<T>() where T : class
        {
            return _world.RemoveComponent<T>(Id);
        }

        public bool RemoveComponent(Type componentType)
        {
            return _world.RemoveComponent(Id, componentType);
        }

        public void Destroy()
        {
            _world.Destroy(Id);
        }

        public bool TryGetName(out string name)
        {
            return _world.TryGetName(Id, out name);
        }

        public string GetName()
        {
            return _world.GetName(Id);
        }

        public void SetName(string name)
        {
            _world.SetName(Id, name);
        }

        public bool Equals(Entity other)
        {
            return ReferenceEquals(_world, other._world) && Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (( _world != null ? _world.GetHashCode() : 0) * 397) ^ Id.GetHashCode();
            }
        }

        public static bool operator ==(Entity left, Entity right) => left.Equals(right);
        public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

        public override string ToString()
        {
            return _world == null ? "Entity(null)" : $"Entity({Id.Index},{Id.Version})";
        }
    }
}
