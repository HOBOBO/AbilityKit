using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Serialization
{
    public static class MemoryPackWireSerializerInstaller
    {
        public static void InstallAsCurrent()
        {
            WireSerializer.Current = new MemoryPackWireSerializer();
        }
    }
}
