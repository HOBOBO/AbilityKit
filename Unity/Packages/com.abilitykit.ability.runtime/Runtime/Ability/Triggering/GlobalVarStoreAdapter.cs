using System;

namespace AbilityKit.Ability.Triggering
{
    public sealed class GlobalVarStoreAdapter : IVarStore
    {
        public static readonly GlobalVarStoreAdapter Instance = new GlobalVarStoreAdapter();

        private GlobalVarStoreAdapter()
        {
        }

        public bool TryGet(string key, out object value)
        {
            return GlobalVarStore.TryGet(key, out value);
        }

        public bool TryGet<T>(string key, out T value)
        {
            return GlobalVarStore.TryGet(key, out value);
        }

        public void Set(string key, object value)
        {
            GlobalVarStore.Set(key, value);
        }
    }
}
