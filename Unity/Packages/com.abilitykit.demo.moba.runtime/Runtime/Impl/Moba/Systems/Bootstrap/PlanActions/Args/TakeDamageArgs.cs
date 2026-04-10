namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    using AbilityKit.Ability.Impl.Moba;
    /// <summary>
    /// take_damage Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct TakeDamageArgs
    {
        /// <summary>
        /// 浼ゅ鍊嶇巼
        /// </summary>
        public readonly float Rate;

        /// <summary>
        /// 浼ゅ鍘熷洜鍙傛暟锛堝叧鑱?DamageReasonKind锛?
        /// </summary>
        public readonly int ReasonParam;

        public TakeDamageArgs(float rate, int reasonParam)
        {
            Rate = rate;
            ReasonParam = reasonParam;
        }

        public static TakeDamageArgs Default => new TakeDamageArgs(1f, 0);
    }
}
