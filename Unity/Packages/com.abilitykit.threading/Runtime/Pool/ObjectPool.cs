using System;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// 从池中获取对象
        /// </summary>
        T Rent();

        /// <summary>
        /// 归还对象到池中
        /// </summary>
        void Return(T item);

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 峰值数量
        /// </summary>
        int PeakCount { get; }
    }

    /// <summary>
    /// 通用对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly MpscQueue<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;
        private readonly int _maxSize;
        private int _count;
        private int _peakCount;

        public int Count => _count;
        public int PeakCount => _peakCount;

        /// <summary>
        /// 创建一个新的对象池
        /// </summary>
        /// <param name="factory">对象创建工厂</param>
        /// <param name="reset">对象归还时的重置回调</param>
        /// <param name="maxSize">最大容量</param>
        public ObjectPool(Func<T> factory, Action<T> reset = null, int maxSize = 1024)
        {
            _pool = new MpscQueue<T>();
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _reset = reset;
            _maxSize = maxSize > 0 ? maxSize : 1024;
        }

        /// <summary>
        /// 从池中获取对象
        /// </summary>
        public T Rent()
        {
            if (_pool.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                return item;
            }

            return _factory();
        }

        /// <summary>
        /// 归还对象到池中
        /// </summary>
        public void Return(T item)
        {
            if (item == null)
                return;

            _reset?.Invoke(item);

            if (_count < _maxSize)
            {
                _pool.Enqueue(item);
                Interlocked.Increment(ref _count);

                var currentCount = _count;
                while (currentCount > _peakCount)
                {
                    var oldPeak = _peakCount;
                    if (Interlocked.CompareExchange(ref _peakCount, currentCount, oldPeak) == oldPeak)
                        break;
                    currentCount = _count;
                }
            }
        }

        /// <summary>
        /// 清空池中所有对象
        /// </summary>
        public void Clear()
        {
            while (_pool.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }

        /// <summary>
        /// 预热池（预先创建指定数量的对象）
        /// </summary>
        public void WarmUp(int count)
        {
            count = Math.Min(count, _maxSize);
            for (int i = 0; i < count; i++)
            {
                var item = _factory();
                _pool.Enqueue(item);
                Interlocked.Increment(ref _count);
            }

            var currentPeak = _peakCount;
            while (count > currentPeak)
            {
                var oldValue = Interlocked.CompareExchange(ref _peakCount, count, currentPeak);
                if (oldValue == currentPeak)
                    break;
                currentPeak = oldValue;
            }
        }
    }

    /// <summary>
    /// 池化对象包装器
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly T _value;
        private readonly ObjectPool<T> _pool;

        /// <summary>
        /// 获取对象实例
        /// </summary>
        public T Value => _value;

        /// <summary>
        /// 隐式转换为对象
        /// </summary>
        public static implicit operator T(PooledObject<T> pooled) => pooled._value;

        internal PooledObject(T value, ObjectPool<T> pool)
        {
            _value = value;
            _pool = pool;
        }

        /// <summary>
        /// 归还对象到池
        /// </summary>
        public void Dispose()
        {
            _pool?.Return(_value);
        }
    }

    /// <summary>
    /// 对象池扩展方法
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// 使用池化对象执行操作
        /// </summary>
        public static TResult Use<T, TResult>(this ObjectPool<T> pool, Func<T, TResult> action) where T : class
        {
            var item = pool.Rent();
            try
            {
                return action(item);
            }
            finally
            {
                pool.Return(item);
            }
        }

        /// <summary>
        /// 使用池化对象执行操作
        /// </summary>
        public static void Use<T>(this ObjectPool<T> pool, Action<T> action) where T : class
        {
            var item = pool.Rent();
            try
            {
                action(item);
            }
            finally
            {
                pool.Return(item);
            }
        }

        /// <summary>
        /// 获取池化对象
        /// </summary>
        public static PooledObject<T> Get<T>(this ObjectPool<T> pool) where T : class
        {
            return new PooledObject<T>(pool.Rent(), pool);
        }
    }

    /// <summary>
    /// 可池化对象接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 重置对象状态（归还到池时调用）
        /// </summary>
        void Reset();

        /// <summary>
        /// 释放对象（真正销毁）
        /// </summary>
        void Release();
    }

    /// <summary>
    /// 池化上下文
    /// </summary>
    public sealed class PoolContext<T> : IDisposable where T : class, IPoolable
    {
        private readonly ObjectPool<T> _pool;
        private readonly T _instance;
        private bool _returned;

        public T Instance => _instance;

        internal PoolContext(ObjectPool<T> pool, T instance)
        {
            _pool = pool;
            _instance = instance;
            _returned = false;
        }

        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return()
        {
            if (_returned)
                return;

            _returned = true;
            _instance.Reset();
            _pool.Return(_instance);
        }

        /// <summary>
        /// 归还对象
        /// </summary>
        public void Dispose()
        {
            Return();
        }
    }

    /// <summary>
    /// 对象池工厂
    /// </summary>
    public static class ObjectPoolFactory
    {
        /// <summary>
        /// 创建默认对象池
        /// </summary>
        public static ObjectPool<T> Create<T>(int maxSize = 1024) where T : class, new()
        {
            return new ObjectPool<T>(() => new T(), null, maxSize);
        }

        /// <summary>
        /// 创建带构造参数的池
        /// </summary>
        public static ObjectPool<T> Create<T>(Func<T> factory, int maxSize = 1024) where T : class
        {
            return new ObjectPool<T>(factory, null, maxSize);
        }

        /// <summary>
        /// 创建带重置回调的池
        /// </summary>
        public static ObjectPool<T> Create<T>(Func<T> factory, Action<T> reset, int maxSize = 1024) where T : class
        {
            return new ObjectPool<T>(factory, reset, maxSize);
        }
    }
}
