using AbilityKit.Core.Common.Record.Lockstep;

namespace AbilityKit.Record.MemoryPack
{
    public static class LockstepMemoryPackInputRecordCodecInstaller
    {
        public static void InstallAsCurrent()
        {
            LockstepInputRecordCodecs.Current = new LockstepMemoryPackInputRecordCodec();
        }
    }
}
