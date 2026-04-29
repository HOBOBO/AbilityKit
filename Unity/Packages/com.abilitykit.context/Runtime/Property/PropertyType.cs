using System;
using System.Collections.Generic;

namespace AbilityKit.Context
{
    /// <summary>
    /// 属性类型注册表
    /// 管理所有属性类型的注册
    /// </summary>
    public sealed class PropertyTypeRegistry
    {
        private static PropertyTypeRegistry? _instance;
        public static PropertyTypeRegistry Instance => _instance ??= new PropertyTypeRegistry();

        private readonly Dictionary<int, PropertyType> _types = new();
        private readonly Dictionary<Type, PropertyType> _typesByCSharpType = new();
        private int _nextTypeId = 1;

        private PropertyTypeRegistry() { }

        /// <summary>
        /// 注册属性类型
        /// </summary>
        public PropertyType Register<T>() where T : IProperty
        {
            return Register(typeof(T));
        }

        /// <summary>
        /// 注册属性类型
        /// </summary>
        public PropertyType Register(Type propertyType)
        {
            if (_typesByCSharpType.TryGetValue(propertyType, out var existing))
            {
                return existing;
            }

            var typeId = _nextTypeId++;
            var type = new PropertyType(typeId, propertyType);
            _types[typeId] = type;
            _typesByCSharpType[propertyType] = type;
            return type;
        }

        /// <summary>
        /// 获取属性类型
        /// </summary>
        public PropertyType? Get(int typeId)
        {
            return _types.TryGetValue(typeId, out var type) ? type : null;
        }

        /// <summary>
        /// 获取属性类型
        /// </summary>
        public PropertyType? Get<T>() where T : IProperty
        {
            return Get(typeof(T));
        }

        /// <summary>
        /// 获取属性类型
        /// </summary>
        public PropertyType? Get(Type propertyType)
        {
            return _typesByCSharpType.TryGetValue(propertyType, out var type) ? type : null;
        }

        /// <summary>
        /// 获取已注册的属性类型数量
        /// </summary>
        public int Count => _types.Count;
    }

    /// <summary>
    /// 属性类型描述
    /// </summary>
    public sealed class PropertyType
    {
        public int Id { get; }
        public Type CSharpType { get; }

        internal PropertyType(int id, Type csharpType)
        {
            Id = id;
            CSharpType = csharpType;
        }

        public override string ToString() => CSharpType.Name;
    }
}
