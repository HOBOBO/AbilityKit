using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

public sealed class ProjectilePhaseSnapshotTests
{
    [Fact]
    public void Nested_snapshot_payload_rejects_unversioned_legacy_and_unknown_versions()
    {
        Assert.Throws<System.InvalidOperationException>(() => MobaProjectileEventSnapshotCodec.Deserialize(
            new byte[] { 1, 0, 0, 0, 0 })); // Previous payload: one member, an empty entries array.
        var invalid = MemoryPack.MemoryPackSerializer.Serialize(new MobaProjectileEventSnapshotPayload {
            Entries = System.Array.Empty<MobaProjectileEventSnapshotEntry>(), Version = 99 });
        Assert.Throws<System.InvalidOperationException>(() => MobaProjectileEventSnapshotCodec.Deserialize(invalid));
    }
    [Fact]
    public void Phase_payload_requires_negotiated_schema_two_at_the_decode_boundary()
    {
        var catalog = System.Linq.Enumerable.Single(AbilityKit.Protocol.Generated.BuiltInProtocolCatalogs.All,
            c => c.CatalogId == "abilitykit.moba.battle");
        var definition = System.Linq.Enumerable.Single(catalog.Messages, m => m.Id == "projectile-event.push");
        var registry = new AbilityKit.Protocol.Catalog.ProtocolPayloadDecoderRegistry();
        registry.Register(catalog.CatalogId, definition.Id, payload =>
            MobaProjectileEventSnapshotCodec.Deserialize(payload.Array));
        var bytes = MobaProjectileEventSnapshotCodec.Serialize(System.Array.Empty<MobaProjectileEventSnapshotEntry>());
        Assert.Equal(AbilityKit.Protocol.Catalog.ProtocolDecodeFailureKind.UnsupportedSchemaVersion,
            registry.Decode(catalog.CatalogId, definition, new System.ArraySegment<byte>(bytes), 1).FailureKind);
        Assert.True(registry.Decode(catalog.CatalogId, definition, new System.ArraySegment<byte>(bytes), 2).Success);
    }
    [Fact]
    public void Phase_cursor_roundtrip_preserves_actor_generation_original_phase_and_seek_time()
    {
        var source = new MobaProjectileEventSnapshotEntry {
            Kind = (int)ProjectileEventKind.Phase, ProjectileActorId = 21, ProjectileId = 9,
            ActorVersion = 3, TemplateId = 42, ProfileId = "persistent_object", StatePath = "deployed",
            PhaseInstance = 33, EnteredFrame = 33, SnapshotFrame = 80, LocalSeconds = 1.5f,
            X = 3, Y = 1, Z = 4, ForwardZ = 1
        };
        var bytes = MobaProjectileEventSnapshotCodec.Serialize(new[] { source });
        Assert.Equal("E94470A00A85550ED37E6FEFF3738F6FF38F93AA76127032DE7839BE7F13CCD0",
            System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        var decoded = Assert.Single(MobaProjectileEventSnapshotCodec.Deserialize(bytes));
        Assert.Equal(bytes, MobaProjectileEventSnapshotCodec.Serialize(new[] { decoded }));
        var mapped = ProjectileEventSnapshotMapper.Map(in decoded);
        Assert.Equal(ProjectilePresentationEventKind.Phase, mapped.Kind);
        Assert.Equal(21, mapped.ProjectileActorId);
        Assert.Equal(9, mapped.ProjectileId);
        Assert.Equal(3, mapped.ActorVersion);
        Assert.Equal("persistent_object", mapped.ProfileId);
        Assert.Equal("deployed", mapped.StatePath);
        Assert.Equal(33, mapped.PhaseInstance);
        Assert.Equal(33, mapped.EnteredFrame);
        Assert.Equal(80, mapped.SnapshotFrame);
        Assert.Equal(1.5f, mapped.LocalSeconds);
        Assert.Equal(4, mapped.Z);
    }
}
