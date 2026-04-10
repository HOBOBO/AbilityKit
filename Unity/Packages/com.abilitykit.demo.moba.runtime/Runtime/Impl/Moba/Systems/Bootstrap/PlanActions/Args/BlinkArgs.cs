namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// blink Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct BlinkArgs
    {
        /// <summary>
        /// 闂儊璺濈锛堝崟浣嶏級
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// 闂儊鏂瑰悜妯″紡
        /// 0=鏈濇妧鑳界瀯鍑嗘柟鍚? 1=鏈濈洰鏍囨柟鍚?
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 浼樺厛绾?
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 鏄惁搴旂敤鍒伴噴鏀捐€咃紙榛樿 caster锛?
        /// </summary>
        public readonly bool ApplyToCaster;

        public BlinkArgs(float distance, int directionMode = 0, int priority = 15, bool applyToCaster = true)
        {
            Distance = distance;
            DirectionMode = directionMode;
            Priority = priority;
            ApplyToCaster = applyToCaster;
        }

        public static BlinkArgs Default => new BlinkArgs(0f, 0, 15, true);
    }
}
