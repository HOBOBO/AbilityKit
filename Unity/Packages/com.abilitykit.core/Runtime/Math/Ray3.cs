namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 保存射线起点和原始方向，不归一化方向，也不限制取点参数正负。
    /// </summary>
    public readonly struct Ray3
    {
        /// <summary>
        /// 保存射线起点，单位由调用方决定。
        /// </summary>
        public readonly Vec3 Origin;
        /// <summary>
        /// 保存原始方向及其长度，不保证为单位向量。
        /// </summary>
        public readonly Vec3 Direction;

        /// <summary>
        /// 直接保存起点与方向，允许零方向及非单位方向。
        /// </summary>
        public Ray3(in Vec3 origin, in Vec3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        /// <summary>
        /// 返回 Origin + Direction * distance，允许负参数；仅单位方向下参数才等于沿射线的有符号距离。
        /// </summary>
        public Vec3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }
    }
}
