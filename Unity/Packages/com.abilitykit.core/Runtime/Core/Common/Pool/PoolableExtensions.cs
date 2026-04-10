namespace AbilityKit.Core.Common.Pool
{
    internal static class PoolableExtensions
    {
        public static void TryOnPoolGet<T>(this T obj) where T : class
        {
            if (obj is IPoolable p) p.OnPoolGet();
        }

        public static void TryOnPoolRelease<T>(this T obj) where T : class
        {
            if (obj is IPoolable p) p.OnPoolRelease();
        }

        public static void TryOnPoolDestroy<T>(this T obj) where T : class
        {
            if (obj is IPoolable p) p.OnPoolDestroy();
        }
    }
}
