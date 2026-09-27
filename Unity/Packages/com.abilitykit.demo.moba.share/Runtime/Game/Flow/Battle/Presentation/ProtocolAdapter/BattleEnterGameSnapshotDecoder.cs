using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Decodes an enter-game wire payload directly into a platform-neutral snapshot.</summary>
    public static class BattleEnterGameSnapshotDecoder
    {
        public static BattleEnterGameSnapshot Decode(byte[] payload)
        {
            var response = EnterMobaGameCodec.DeserializeRes(payload);
            return BattleEnterGameSnapshotMapper.Map(in response);
        }
    }
}
