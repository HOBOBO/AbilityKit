using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 轻量级对象池。
    /// 用于减少频繁的分配/销毁开销。
    /// </summary>
    /// <typeparam name="T">对象类型，必须有默认构造函数。</typeparam>
    public class Pool<T> where T : class
    {
        /// <summary>对象工厂。</summary>
        public Func<T> Factory { get; }

        /// <summary>归还时的回调。</summary>
        public Action<T> OnRelease { get; }

        /// <summary>销毁对象时的回调。</summary>
        public Action<T> OnDestroy { get; }

        /// <summary>默认容量。</summary>
        public int DefaultCapacity { get; }

        /// <summary>最大容量。</summary>
        public int MaxSize { get; }

        /// <summary>当前池中对象数量。</summary>
        public int Count => _stack.Count;

        private readonly System.Collections.Concurrent.ConcurrentStack<T> _stack;

        /// <summary>
        /// 创建对象池。
        /// </summary>
        /// <param name="factory">对象工厂。</param>
        /// <param name="onRelease">归还时的回调。</param>
        /// <param name="defaultCapacity">默认容量。</param>
        /// <param name="maxSize">最大容量。</param>
        /// <param name="onDestroy">销毁时的回调。</param>
        public static Pool<T> Create(
            Func<T> factory,
            Action<T> onRelease = null,
            int defaultCapacity = 16,
            int maxSize = int.MaxValue,
            Action<T> onDestroy = null)
        {
            return new Pool<T>(factory, onRelease, defaultCapacity, maxSize, onDestroy);
        }

        internal Pool(
            Func<T> factory,
            Action<T> onRelease,
            int defaultCapacity,
            int maxSize,
            Action<T> onDestroy)
        {
            Factory = factory;
            OnRelease = onRelease;
            OnDestroy = onDestroy;
            DefaultCapacity = defaultCapacity;
            MaxSize = maxSize;
            _stack = new System.Collections.Concurrent.ConcurrentStack<T>();

            // 预热池
            for (int i = 0; i < defaultCapacity; i++)
            {
                _stack.Push(factory());
            }
        }

        /// <summary>获取对象。</summary>
        public T Get()
        {
            if (_stack.TryPop(out var item))
            {
                return item;
            }
            return Factory();
        }

        /// <summary>归还对象。</summary>
        public void Release(T item)
        {
            if (item == null) return;

            if (OnRelease != null)
            {
                OnRelease.Invoke(item);
            }

            if (_stack.Count < MaxSize)
            {
                _stack.Push(item);
            }
            else
            {
                if (OnDestroy != null)
                {
                    OnDestroy.Invoke(item);
                }
            }
        }
    }
}
