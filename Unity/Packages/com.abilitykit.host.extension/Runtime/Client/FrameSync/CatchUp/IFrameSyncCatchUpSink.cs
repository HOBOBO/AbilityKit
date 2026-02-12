namespace AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp
{
    public interface IFrameSyncCatchUpSink
    {
        void ApplyCatchUp(in FrameSyncCatchUpPayload payload);
    }
}
