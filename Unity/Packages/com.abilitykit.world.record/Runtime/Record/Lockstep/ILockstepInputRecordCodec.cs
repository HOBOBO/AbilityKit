using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.Record.Lockstep
{
    public interface ILockstepInputRecordCodec
    {
        ILockstepInputRecordWriter CreateWriter(string outputPath, LockstepInputRecordMeta meta);

        LockstepInputRecordFile Load(string path);
    }
}
