namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// debug_log Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct DebugLogArgs
    {
        /// <summary>
        /// 娑堟伅ID锛堜粠 TriggerPlanJsonDatabase 鐨?string table 涓幏鍙栵級
        /// 涓?鏃惰〃绀烘棤娑堟伅ID锛屼粎杈撳嚭涓婁笅鏂囦俊鎭?
        /// </summary>
        public readonly int MsgId;

        /// <summary>
        /// 鏄惁杈撳嚭瀹屾暣涓婁笅鏂囦俊鎭紙dump锛?
        /// </summary>
        public readonly bool Dump;

        public DebugLogArgs(int msgId, bool dump)
        {
            MsgId = msgId;
            Dump = dump;
        }

        public static DebugLogArgs Default => new DebugLogArgs(0, false);

        /// <summary>
        /// 鏃犲弬鏁扮増鏈紙浠呰緭鍑轰笂涓嬫枃淇℃伅锛?
        /// </summary>
        public static DebugLogArgs Empty => default;
    }
}
