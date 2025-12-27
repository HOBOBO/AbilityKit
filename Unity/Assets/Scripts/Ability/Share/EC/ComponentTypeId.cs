using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.EC
{
    public static class ComponentTypeId
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<Type, int> _ids = new Dictionary<Type, int>();
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
                return id;
            }
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
