namespace AbilityKit.Continuous
{
    /// <summary>
    /// 持续体状态
    /// </summary>
    public enum ContinuousState
    {
        /// <summary>未激活/已销毁</summary>
        Inactive,
        /// <summary>正在激活中</summary>
        Activating,
        /// <summary>运行中</summary>
        Active,
        /// <summary>已暂停</summary>
        Paused,
        /// <summary>已过期（正常结束）</summary>
        Expired,
        /// <summary>已中止（非正常结束）</summary>
        Aborted,
    }
}
