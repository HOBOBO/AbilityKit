namespace AbilityKit.Ability.FrameSync
{
    /// <summary>
    /// 帧时钟的可选对齐能力；预测驱动仅使用当前 IFrameTime 实例提供的此能力。
    /// 只读时钟无需实现，原有读取契约保持不变。
    /// </summary>
    public interface IFrameTimeAligner : IFrameTime
    {
        /// <summary>
        /// 按时钟自身的固定步长对齐到目标帧，不执行世界模拟。
        /// 步长尚未确定时可使用正的备用步长；无法确定时返回 false。
        /// </summary>
        bool AlignTo(FrameIndex targetFrame, float fallbackDeltaTime);
    }
}
