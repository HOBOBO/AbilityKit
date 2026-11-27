namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Stable keys for typed presentation cue parameters shared by logic and view.
    /// These keys keep shape data generic and avoid hero-specific protocol fields.
    /// </summary>
    public static class PresentationCueParameterKeys
    {
        public const int AreaForward = 1001;
        public const int AreaShapeKind = 1002;
        public const int AreaHalfAngleDeg = 1003;
        public const int AreaWidth = 1004;
        public const int AreaLength = 1005;
        /// <summary>
        /// 固定发射点一次性表现的世界朝向，不允许绑定跟随目标。
        /// </summary>
        public const int ProjectileLaunchForward = 1006;
        /// <summary>
        /// ??????????????????????
        /// </summary>
        public const int ProjectileFlightDistance = 1007;
        public const int ProjectileFlightArcHeight = 1008;
    }
}
