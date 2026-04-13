namespace AbilityKit.Core.Common.Record.Lockstep
{
    public sealed class LockstepBinaryInputRecordCodec : ILockstepInputRecordCodec
    {
        public ILockstepInputRecordWriter CreateWriter(string outputPath, LockstepInputRecordMeta meta)
        {
            return new LockstepBinaryInputRecordWriter(outputPath, meta);
        }

        public LockstepInputRecordFile Load(string path)
        {
            return LockstepBinaryInputRecordReader.Load(path);
        }
    }
}
