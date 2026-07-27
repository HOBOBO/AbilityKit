using System;
using AbilityKit.Ability.FrameSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services.StateSync
{
    public interface IMobaStateRecoveryProvider
    {
        int Key { get; }
        string Name { get; }
        byte[] ExportState(FrameIndex frame);
        void ImportState(FrameIndex frame, byte[] payload);
        void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash);
    }

    [MemoryPackable]
    public readonly partial struct MobaStateRecoverySnapshot
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Frame;
        [MemoryPackOrder(2)] public readonly MobaStateRecoveryEntry[] Entries;

        [MemoryPackConstructor]
        public MobaStateRecoverySnapshot(int version, int frame, MobaStateRecoveryEntry[] entries)
        {
            Version = version;
            Frame = frame;
            Entries = entries ?? Array.Empty<MobaStateRecoveryEntry>();
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaStateRecoveryEntry
    {
        [MemoryPackOrder(0)] public readonly int Key;
        [MemoryPackOrder(1)] public readonly byte[] Payload;

        [MemoryPackConstructor]
        public MobaStateRecoveryEntry(int key, byte[] payload)
        {
            Key = key;
            Payload = payload ?? Array.Empty<byte>();
        }
    }
}
