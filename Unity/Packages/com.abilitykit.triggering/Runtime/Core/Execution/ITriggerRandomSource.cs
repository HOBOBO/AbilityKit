namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 执行树使用的宿主随机源；需要重演时，由宿主保证取样顺序并捕获、恢复随机状态。
    /// 运行时不验证随机源是否可重演，也不代为保存其状态。
    /// </summary>
    public interface ITriggerRandomSource
    {
        /// <summary>
        /// 取得下一次取样值，范围为大于等于零且小于一。
        /// </summary>
        float NextFloat01();
    }
}
