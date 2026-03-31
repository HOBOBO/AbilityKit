using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Payload
{
    /// <summary>
    /// 强类型 Payload 访问器注册表
    /// 支持 struct-based 访问器以避免装箱开销
    /// </summary>
    public interface IStronglyTypedPayloadAccessorRegistry
    {
        /// <summary>
        /// 获取指定 Payload 类型的访问器
        /// </summary>
        IPayloadAccessor<TArgs> GetAccessor<TArgs>() where TArgs : struct;

        /// <summary>
        /// 尝试获取 Payload 访问器
        /// </summary>
        bool TryGetAccessor<TArgs>(out IPayloadAccessor<TArgs> accessor) where TArgs : struct;

        /// <summary>
        /// 检查是否存在指定 Payload 类型的访问器
        /// </summary>
        bool HasAccessor<TArgs>() where TArgs : struct;
    }

    /// <summary>
    /// 强类型 Payload 访问器注册表实现
    /// </summary>
    public sealed class StronglyTypedPayloadAccessorRegistry : IStronglyTypedPayloadAccessorRegistry
    {
        private readonly Dictionary<Type, object> _accessors = new Dictionary<Type, object>();

        /// <summary>
        /// 注册强类型 Payload 访问器
        /// </summary>
        public void Register<TArgs>(IPayloadAccessor<TArgs> accessor) where TArgs : struct
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _accessors[typeof(TArgs)] = accessor;
        }

        /// <summary>
        /// 注册强类型 Payload 访问器（使用静态委托创建）
        /// </summary>
        public void Register<TArgs>(DelegatePayloadAccessor<TArgs> accessor) where TArgs : struct
        {
            _accessors[typeof(TArgs)] = accessor;
        }

        /// <summary>
        /// 尝试获取 Payload 访问器
        /// </summary>
        public bool TryGetAccessor<TArgs>(out IPayloadAccessor<TArgs> accessor) where TArgs : struct
        {
            if (_accessors.TryGetValue(typeof(TArgs), out var obj) && obj is IPayloadAccessor<TArgs> typedAccessor)
            {
                accessor = typedAccessor;
                return true;
            }
            accessor = default;
            return false;
        }

        /// <inheritdoc />
        public IPayloadAccessor<TArgs> GetAccessor<TArgs>() where TArgs : struct
        {
            if (TryGetAccessor<TArgs>(out var accessor))
            {
                return accessor;
            }
            throw new KeyNotFoundException($"Payload accessor not found for type: {typeof(TArgs).Name}");
        }

        /// <inheritdoc />
        public bool HasAccessor<TArgs>() where TArgs : struct
        {
            return _accessors.ContainsKey(typeof(TArgs));
        }
    }
}
