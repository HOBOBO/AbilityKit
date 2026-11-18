namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 对象池运行时控制接口，用于在管理器中以非泛型方式执行裁剪和清理，避免批量操作依赖反射。
    /// </summary>
    internal interface IObjectPoolControl
    {
        /// <summary>
        /// 按池自身配置执行常规裁剪。
        /// </summary>
        int Trim();

        /// <summary>
        /// 按显式策略执行常规裁剪。
        /// </summary>
        int Trim(PoolTrimPolicy policy);

        /// <summary>
        /// 按显式策略裁剪并忽略禁止常规裁剪的标志。
        /// </summary>
        int ForceTrim(PoolTrimPolicy policy);

        /// <summary>
        /// 清空空闲对象，并按参数决定是否调用销毁回调。
        /// </summary>
        void Clear(bool destroy = false);
    }
}
