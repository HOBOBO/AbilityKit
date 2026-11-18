using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 保存对象池创建、生命周期、空闲容量、引用校验和裁剪的可变选项；池构造时读取选项，调用方不得并发修改。
    /// </summary>
    /// <typeparam name="T">池中保存的引用类型。</typeparam>
    public sealed class ObjectPoolOptions<T> where T : class
    {
        /// <summary>
        /// 池无空闲对象或执行预热时调用的工厂，必须返回新的非空对象且不得重复提供仍在使用的实例。
        /// </summary>
        public Func<T> CreateFunc;

        /// <summary>
        /// 对象实现的获取回调成功后执行；任一获取回调失败则销毁该对象后传播异常。
        /// </summary>
        public Action<T>? OnGet;

        /// <summary>
        /// 对象实现的归还回调成功后执行，回调成功前对象不会重新进入空闲栈；失败时归还不提交。
        /// </summary>
        public Action<T>? OnRelease;

        /// <summary>
        /// 永久移除对象时执行的可选销毁回调；即使对象自身销毁回调失败也仍会尝试执行。
        /// </summary>
        public Action<T>? OnDestroy;

        /// <summary>
        /// 获取或设置构造预热数量，必须非负；实际预热受最大空闲容量限制。
        /// </summary>
        public int DefaultCapacity = 0;

        /// <summary>
        /// 获取或设置最大空闲保留数量，必须为正数；该值不限制同时借出的对象数量。
        /// </summary>
        public int MaxSize = 1024;

        /// <summary>
        /// 获取或设置无参裁剪使用的策略；修改已交付选项不会更新现有池。
        /// </summary>
        public PoolTrimPolicy TrimPolicy = PoolTrimPolicy.KeepDefaultCapacity;

        /// <summary>
        /// 获取或设置是否按引用身份检查重复及转换期间的归还；不证明对象由本池创建。
        /// </summary>
        public bool CollectionCheck = true;

        /// <summary>
        /// 获取或设置是否跳过常规裁剪；强制裁剪及清空仍生效。
        /// </summary>
        public bool NeverTrim;

        /// <summary>
        /// 创建使用指定工厂的选项；工厂为空时抛出参数异常。
        /// </summary>
        /// <param name="createFunc">必须返回新的非空对象的工厂。</param>
        /// <exception cref="ArgumentNullException">参数为空引用。</exception>
        public ObjectPoolOptions(Func<T> createFunc)
        {
            CreateFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        }
    }
}
