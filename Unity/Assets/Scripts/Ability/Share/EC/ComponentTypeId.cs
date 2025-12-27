using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.EC
{
    public static class ComponentTypeId
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<Type, int> _ids = new Dictionary<Type, int>();
        private static readonly Dictionary<int, Type> _types = new Dictionary<int, Type>();
        private static int _nextId = 1;

        public static int Get<T>()
        {
            return Get(typeof(T));
        }

        public static int Get(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            lock (_lock)
            {
                if (_ids.TryGetValue(type, out var id)) return id;
                id = _nextId++;
                _ids.Add(type, id);
                _types[id] = type;
                return id;
            }
        }

        public static bool TryGetType(int typeId, out Type type)
        {
            lock (_lock)
            {
                return _types.TryGetValue(typeId, out type);
            }
        }

        public static Type GetTypeById(int typeId)
        {
            if (TryGetType(typeId, out var type)) return type;
            throw new KeyNotFoundException($"Unknown component typeId: {typeId}");
        }

        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _nextId;
                }
            }
        }
    }
}
