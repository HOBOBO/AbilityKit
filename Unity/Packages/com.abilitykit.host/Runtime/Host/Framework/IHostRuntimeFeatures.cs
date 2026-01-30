using System;

namespace AbilityKit.Ability.Host.Framework
{
    public interface IHostRuntimeFeatures
    {
        bool TryGetFeature(Type featureType, out object feature);
        bool RegisterFeature(Type featureType, object feature);
        bool UnregisterFeature(Type featureType);

        bool TryGetFeature<T>(out T feature);
        bool RegisterFeature<T>(T feature);
        bool UnregisterFeature<T>();
    }
}
