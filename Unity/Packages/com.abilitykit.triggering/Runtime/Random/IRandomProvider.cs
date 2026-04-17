namespace AbilityKit.Triggering.Runtime.Random
{
    /// <summary>
    /// 随机数提供者接口
    /// 用于确定性随机数生成和帧同步
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>
        /// 获取指定范围的随机整数 [min, max)
        /// </summary>
        int Next(int min, int max);

        /// <summary>
        /// 获取随机浮点数 [0.0, 1.0)
        /// </summary>
        float NextFloat();
    }
}