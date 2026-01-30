using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Framework
{
    public sealed class HostRuntimeFeatures : IHostRuntimeFeatures
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public bool TryGetFeature(Type featureType, out object feature)
        {
            if (featureType == null)
            {
                feature = null;
                return false;
            }

            return _map.TryGetValue(featureType, out feature);
        }

        public bool RegisterFeature(Type featureType, object feature)
        {
            if (featureType == null) return false;
            if (feature == null) return false;
            if (!featureType.IsAssignableFrom(feature.GetType())) return false;

            _map[featureType] = feature;
            return true;
        }

        public bool UnregisterFeature(Type featureType)
        {
            if (featureType == null) return false;
            return _map.Remove(featureType);
        }

        public bool TryGetFeature<T>(out T feature)
        {
            if (TryGetFeature(typeof(T), out var obj) && obj is T t)
            {
                feature = t;
                return true;
            }

            feature = default;
            return false;
        }

        public bool RegisterFeature<T>(T feature)
        {
            return RegisterFeature(typeof(T), feature);
        }

        public bool UnregisterFeature<T>()
        {
            return UnregisterFeature(typeof(T));
        }
    }
}
