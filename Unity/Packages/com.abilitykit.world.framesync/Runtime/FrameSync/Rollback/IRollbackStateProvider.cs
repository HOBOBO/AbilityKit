using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public interface IRollbackStateProvider
    {
        int Key { get; }

        byte[] Export(FrameIndex frame);

        void Import(FrameIndex frame, byte[] payload);
    }
}
