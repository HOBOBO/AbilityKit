namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 可被中断的管线接�?
    /// </summary>
    public interface IPipelineInterruptible
    {
        /// <summary>
        /// 中断管线
        /// </summary>
        void Interrupt();
    }
}
