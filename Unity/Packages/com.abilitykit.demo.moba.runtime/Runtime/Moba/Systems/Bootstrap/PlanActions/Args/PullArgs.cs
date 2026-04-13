namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// pull Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct PullArgs
    {
        /// <summary>
        /// 鎷夊姏閫熷害锛堝崟浣?绉掞級
        /// </summary>
        public readonly float Speed;

        /// <summary>
        /// 鎷夊彇鎸佺画鏃堕棿锛堟绉掞級
        /// </summary>
        public readonly float DurationMs;

        /// <summary>
        /// 鎷夊彇鏂瑰悜妯″紡
        /// 0=浠庣洰鏍囨媺鍒版妧鑳介噴鏀捐€? 1=浠庣洰鏍囨媺鍒版寚瀹氳窛绂? 2=鍨傜洿鍚戜笂鎷?
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 鐩爣璺濈锛圖irectionMode=1鏃朵娇鐢級
        /// </summary>
        public readonly float TargetDistance;

        /// <summary>
        /// 浼樺厛绾?
        /// </summary>
        public readonly int Priority;

        public PullArgs(float speed, float durationMs, int directionMode = 0, float targetDistance = 0f, int priority = 12)
        {
            Speed = speed;
            DurationMs = durationMs;
            DirectionMode = directionMode;
            TargetDistance = targetDistance;
            Priority = priority;
        }

        public static PullArgs Default => new PullArgs(0f, 0f, 0, 0f, 12);
    }
}
