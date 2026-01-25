using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public sealed class SearchContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<int, object> _data = new Dictionary<int, object>();

        public void SetService<T>(T service) where T : class
        {
            if (service == null)
            {
                _services.Remove(typeof(T));
                return;
            }

            _services[typeof(T)] = service;
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var v) && v is T t)
            {
                service = t;
                return true;
            }

            service = null;
            return false;
        }

        public void SetData(int key, object value)
        {
            if (value == null)
            {
                _data.Remove(key);
                return;
            }

            _data[key] = value;
        }

        public bool TryGetData(int key, out object value)
        {
            return _data.TryGetValue(key, out value);
        }

        public bool TryGetData<T>(int key, out T value)
        {
            if (_data.TryGetValue(key, out var v) && v is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void ClearData()
        {
            _data.Clear();
        }

        public void ClearServices()
        {
            _services.Clear();
        }
    }
}
