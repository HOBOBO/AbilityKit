namespace AbilityKit.Game.Flow
{
    public interface IGameFeatureStore
    {
        bool TryGet<T>(out T component) where T : class;
        void Set<T>(T component) where T : class;
        void Remove<T>() where T : class;
        void Remove(System.Type componentType);
    }

    public static class GameFeatureStoreExtensions
    {
        public static bool RemoveIfCurrent<T>(
            this IGameFeatureStore store,
            T expected)
            where T : class
        {
            if (store == null || expected == null ||
                !store.TryGet<T>(out var current) ||
                !ReferenceEquals(current, expected))
            {
                return false;
            }

            store.Remove<T>();
            return true;
        }
    }
}
