namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 世界 Tick 计数器。
    /// 用于跟踪世界执行的帧数。
    /// </summary>
    public sealed class WorldTickCounter
    {
        /// <summary>
        /// 当前Tick计数，从0开始，每帧递增。
        /// </summary>
        public int TickCount;
    }
}