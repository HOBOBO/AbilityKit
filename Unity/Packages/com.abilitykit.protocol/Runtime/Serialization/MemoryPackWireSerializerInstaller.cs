using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Serialization
{
    public static class MemoryPackWireSerializerInstaller
    {
        public static void InstallAsCurrent(bool replaceExisting = false)
        {
            WireSerializer.Install(new MemoryPackWireSerializer(), replaceExisting);
        }
    }
}
