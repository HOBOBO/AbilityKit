namespace AbilityKit.Timer
{
    /// <summary>
    /// 基础计时器接口。
    /// 用于跟踪经过的时间。
    ///
    /// 设计原则：
    /// - 值类型友好：实现应该轻量
    /// - 无副作用：Reset 不分配堆内存
    /// - 可比较：支持与数值比较操作符
    /// </summary>
    public interface ITimer
    {
        /// <summary>经过的时间（秒）</summary>
        float Elapsed { get; }

        /// <summary>重置计时器</summary>
        void Reset();
    }
}
