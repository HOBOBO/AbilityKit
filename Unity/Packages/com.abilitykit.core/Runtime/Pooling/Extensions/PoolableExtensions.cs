namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 仅在元素实现生命周期接口时同步转发通知，不吞掉或转换回调异常。
    /// </summary>
    internal static class PoolableExtensions
    {
        public static void TryOnPoolGet<T>(this T element) where T : class
        {
            if (element is IPoolable poolable)
            {
                poolable.OnPoolGet();
            }
        }

        public static void TryOnPoolRelease<T>(this T element) where T : class
        {
            if (element is IPoolable poolable)
            {
                poolable.OnPoolRelease();
            }
        }

        public static void TryOnPoolDestroy<T>(this T element) where T : class
        {
            if (element is IPoolable poolable)
            {
                poolable.OnPoolDestroy();
            }
        }
    }
}
