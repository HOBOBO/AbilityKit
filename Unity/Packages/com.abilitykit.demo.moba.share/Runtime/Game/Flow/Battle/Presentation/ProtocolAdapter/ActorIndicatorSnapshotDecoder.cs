using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 指示器协议与平台无关表现合同的唯一映射。
    /// </summary>
    public static class ActorIndicatorSnapshotDecoder
    {
        /// <summary>
        /// 合法空集合保留为空基线；解码失败交由既有路由隔离，不静默清空显示。
        /// </summary>
        public static ActorIndicatorStateData[] Decode(byte[] payload)
        {
            var entries = MobaActorIndicatorSnapshotCodec.Deserialize(payload);
            var result = new ActorIndicatorStateData[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                result[i] = new ActorIndicatorStateData(e.ActorId, e.LayoutId, e.IndicatorId,
                    e.Current, e.Max, e.HasMax, e.Ready, e.Availability);
            }
            return result;
        }
    }
}
