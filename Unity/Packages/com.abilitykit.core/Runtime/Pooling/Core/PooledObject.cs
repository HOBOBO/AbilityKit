using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 提供将借出对象归还原池的值类型句柄；复制不会转移所有权，句柄及其所有副本合计只能释放一次。
    /// </summary>
    /// <typeparam name="T">池中保存的引用类型。</typeparam>
    /// <remarks>默认句柄不执行操作。非默认句柄及其副本共享同一归还责任，只能合计释放一次；失败时责任按池的归还契约处理。</remarks>
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly ObjectPool<T> _pool;
        /// <summary>
        /// 获取借出的对象引用；归还后不得继续使用，句柄自身不会清空此字段。
        /// </summary>
        public readonly T Value;

        internal PooledObject(ObjectPool<T> pool, T value)
        {
            _pool = pool;
            Value = value;
        }

        /// <summary>
        /// 将 Value 归还原池；默认句柄不执行操作，非默认句柄及其副本合计只能调用一次，归还异常直接传播。
        /// </summary>
        public void Dispose()
        {
            if (Value == null) return;
            _pool?.Release(Value);
        }
    }
}
