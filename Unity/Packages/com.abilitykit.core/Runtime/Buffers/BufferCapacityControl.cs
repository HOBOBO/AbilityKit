namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// 提供可选的运行时保留容量控制；仅负责单个缓冲区的容量请求，不承担调度策略或跨缓冲区原子调整。
    /// </summary>
    public interface IBufferCapacityControl
    {
        /// <summary>
        /// 获取当前生效的保留容量；逻辑容量不保证等于底层已分配空间。
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// 尝试应用正数容量；实现可以拒绝运行时调整，具体保留顺序由缓冲区契约规定。
        /// </summary>
        bool TrySetCapacity(int capacity);
    }
}
