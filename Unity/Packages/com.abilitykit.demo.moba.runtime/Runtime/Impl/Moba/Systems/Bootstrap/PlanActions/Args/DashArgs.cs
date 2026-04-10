namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// dash Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct DashArgs
    {
        /// <summary>
        /// 鍐插埡閫熷害锛堝崟浣?绉掞級
        /// </summary>
        public readonly float Speed;

        /// <summary>
        /// 鍐插埡鎸佺画鏃堕棿锛堟绉掞級
        /// </summary>
        public readonly float DurationMs;

        /// <summary>
        /// 鍐插埡鏂瑰悜妯″紡
        /// 0=鏈濇妧鑳界瀯鍑嗘柟鍚? 1=鏈濈洰鏍囨柟鍚? 2=淇濇寔褰撳墠鏈濆悜
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 浼樺厛绾э紙楂樹紭鍏堢骇浼氭墦鏂綆浼樺厛绾ц繍鍔級
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 鏄惁搴旂敤鍒伴噴鏀捐€咃紙榛樿 caster锛?
        /// </summary>
        public readonly bool ApplyToCaster;

        public DashArgs(float speed, float durationMs, int directionMode = 0, int priority = 10, bool applyToCaster = true)
        {
            Speed = speed;
            DurationMs = durationMs;
            DirectionMode = directionMode;
            Priority = priority;
            ApplyToCaster = applyToCaster;
        }

        public static DashArgs Default => new DashArgs(0f, 0f, 0, 10, true);
    }
}
