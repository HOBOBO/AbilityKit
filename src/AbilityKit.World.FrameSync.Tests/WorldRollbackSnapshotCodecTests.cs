using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class WorldRollbackSnapshotCodecTests
{
    [Fact]
    public void Checkpoint_codec_roundtrips_provider_keys_and_arbitrary_payload_bytes()
    {
        var checkpoint = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion,
            new FrameIndex(17), new[]
            {
                new WorldRollbackSnapshotEntry(10011, new byte[] { 0, 1, 127, 255 }),
                new WorldRollbackSnapshotEntry(10012, System.Array.Empty<byte>()),
            });
        var bytes = WorldRollbackSnapshotCodec.Serialize(in checkpoint);
        var restored = WorldRollbackSnapshotCodec.Deserialize(bytes);
        Assert.Equal(checkpoint.Version, restored.Version);
        Assert.Equal(checkpoint.Frame, restored.Frame);
        Assert.Equal(2, restored.Entries.Length);
        for (var i = 0; i < checkpoint.Entries.Length; i++)
        {
            Assert.Equal(checkpoint.Entries[i].Key, restored.Entries[i].Key);
            Assert.Equal(checkpoint.Entries[i].Payload, restored.Entries[i].Payload);
        }
        Assert.Equal(bytes, WorldRollbackSnapshotCodec.Serialize(in restored));
    }
}
