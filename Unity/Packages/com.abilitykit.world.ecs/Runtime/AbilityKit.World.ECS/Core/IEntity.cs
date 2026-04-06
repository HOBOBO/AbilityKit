using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 实体句柄，提供流畅的链式API。
    /// 是值类型，零成本封装。
    /// </summary>
    public readonly struct IEntity : IEquatable<IEntity>
    {
        private readonly IECWorld _world;
        private readonly IEntityId _id;

        internal IEntity(IECWorld world, IEntityId id)
        {
            _world = world;
            _id = id;
        }

        /// <summary>实体唯一标识符。</summary>
        public IEntityId Id => _id;

        /// <summary>所属的世界。</summary>
        public IECWorld World => _world;

        /// <summary>实体是否有效（存活）。</summary>
        public bool IsValid => _world != null && _world.IsAlive(_id);

        /// <summary>实体是否无效（已销毁）。</summary>
        public bool IsNull => !IsValid;

        #region 组件操作

        /// <summary>添加或更新值类型组件（链式调用）。</summary>
        public IEntity With<T>(T component) where T : struct
        {
            _world.SetComponent(_id, component);
            return this;
        }

        /// <summary>添加或更新引用类型组件（链式调用）。</summary>
        public IEntity WithRef<T>(T component) where T : class
        {
            _world.SetComponentRef(_id, component);
            return this;
        }

        /// <summary>获取值类型组件。</summary>
        public T Get<T>() where T : struct => _world.GetComponent<T>(_id);

        /// <summary>获取引用类型组件。</summary>
        public T GetRef<T>() where T : class => _world.GetComponentRef<T>(_id);

        /// <summary>尝试获取值类型组件。</summary>
        public bool TryGet<T>(out T component) where T : struct => _world.TryGetComponent(_id, out component);

        /// <summary>尝试获取引用类型组件。</summary>
        public bool TryGetRef<T>(out T component) where T : class => _world.TryGetComponentRef(_id, out component);

        /// <summary>检查是否拥有指定组件。</summary>
        public bool Has<T>() where T : struct => _world.HasComponent<T>();

        /// <summary>移除指定组件。</summary>
        public bool Remove<T>() where T : struct => _world.RemoveComponent<T>(_id);

        #endregion

        #region 父子关系

        /// <summary>设置父实体（链式调用）。</summary>
        public IEntity SetParent(IEntity parent)
        {
            _world.SetParent(_id, parent._id);
            return this;
        }

        /// <summary>设置带逻辑ID的父实体（链式调用）。</summary>
        public IEntity SetParent(IEntity parent, int logicalChildId)
        {
            _world.SetParent(_id, parent._id, logicalChildId);
            return this;
        }

        /// <summary>添加子实体（链式调用）。</summary>
        public IEntity AddChild()
        {
            _world.CreateChild(this);
            return this;
        }

        /// <summary>添加带逻辑ID的子实体（链式调用）。</summary>
        public IEntity AddChild(int logicalChildId)
        {
            _world.CreateChild(this, logicalChildId);
            return this;
        }

        /// <summary>获取父实体。</summary>
        public IEntity Parent => _world.GetParent(_id);

        /// <summary>子实体数量。</summary>
        public int ChildCount => _world.GetChildCount(_id);

        /// <summary>获取指定索引的子实体。</summary>
        public IEntity GetChild(int index) => _world.GetChild(_id, index);

        /// <summary>尝试通过逻辑ID获取子实体。</summary>
        public bool TryGetChildById(int logicalChildId, out IEntity child)
            => _world.TryGetChildById(_id, logicalChildId, out child);

        #endregion

        #region 生命周期

        /// <summary>销毁此实体。</summary>
        public void Destroy()
        {
            _world.Destroy(_id);
        }

        #endregion

        #region 元数据

        /// <summary>获取名称。</summary>
        public string Name => _world.GetName(_id);

        /// <summary>设置名称。</summary>
        public void SetName(string name)
        {
            _world.SetName(_id, name);
        }

        #endregion

        #region IEquatable

        /// <inheritdoc/>
        public bool Equals(IEntity other)
        {
            return ReferenceEquals(_world, other._world) && _id.Equals(other._id);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is IEntity other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((_world != null ? _world.GetHashCode() : 0) * 397) ^ _id.GetHashCode();
            }
        }

        /// <summary>相等运算符。</summary>
        public static bool operator ==(IEntity left, IEntity right) => left.Equals(right);

        /// <summary>不相等运算符。</summary>
        public static bool operator !=(IEntity left, IEntity right) => !left.Equals(right);

        #endregion

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_world == null) return "Entity(null)";
            if (!IsValid) return $"Entity({_id}) [Invalid]";
            var name = _world.GetName(_id);
            return string.IsNullOrEmpty(name)
                ? $"Entity({_id})"
                : $"Entity({_id}) [{name}]";
        }
    }
}
