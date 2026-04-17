namespace AbilityKit.Triggering.Runtime.Time
{
    /// <summary>
    /// 帧时钟接口
    /// 用于帧同步和定时器
    /// </summary>
    public interface IFrameClock
    {
        /// <summary>当前帧号</summary>
        long CurrentFrame { get; }

        /// <summary>每帧的毫秒数（固定值）</summary>
        float FrameDurationMs { get; }

        /// <summary>推进一帧（每帧调用）</summary>
        void AdvanceFrame(float deltaTimeMs);

        /// <summary>强制设置帧号（用于回滚/同步）</summary>
        void SetFrame(long frame);

        /// <summary>重置时钟</summary>
        void Reset();

        /// <summary>毫秒转帧数</summary>
        long MsToFrame(float ms);

        /// <summary>帧数转毫秒</summary>
        float FrameToMs(long frame);
    }
}