namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 接收对象池同步生命周期通知；回调在池锁外执行，不同对象的回调可能并发。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 对象获取时先于选项中的获取回调执行；异常会使本次获取失败并触发销毁。
        /// </summary>
        void OnPoolGet();
        /// <summary>
        /// 对象归还或预热时先于选项中的归还回调执行；异常会跳过后续选项回调。
        /// </summary>
        void OnPoolRelease();
        /// <summary>
        /// 永久移除对象时先于选项中的销毁回调执行；异常不会阻止后续选项回调。
        /// </summary>
        void OnPoolDestroy();
    }
}
