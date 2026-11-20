using System.Buffers.Binary;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Deterministic;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaResourceComponentRecoveryTests
{
    private static readonly FrameIndex Frame = new(10);
    private const long Marker = 765432109876543L;

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public void Full_component_shape_and_raw_fields_restore_independently(int shape, bool initialized)
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var map = shape < 3 ? null : new Dictionary<ResourceType, ResourceState>();
        if (shape == 4) map!.Add(ResourceType.Ammo, null!);
        if (shape == 5)
        {
            map!.Add(ResourceType.Hp, State(long.MinValue + 11, long.MaxValue - 13, -987));
            map.Add(ResourceType.Mana, State(0, -1, 0));
            map.Add(ResourceType.None, null!);
        }
        if (shape > 0) entity.AddResourceContainer(shape == 1 ? null : new ResourceContainer { Map = map }, initialized);
        var saved = provider.Export(Frame);
        if (shape == 5) map![ResourceType.Hp].Current = Fixed64.One;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            entity.ReplaceResourceContainer(new ResourceContainer
            {
                Map = new() { [ResourceType.Rage] = State(99, 100, 123) }
            }, !initialized);
            provider.Import(Frame, saved);
            Assert.Equal(shape > 0, entity.hasResourceContainer);
            if (shape == 0) continue;
            Assert.Equal(initialized, entity.resourceContainer.Initialized);
            var value = entity.resourceContainer.Value;
            Assert.Equal(shape != 1, value != null);
            if (shape == 1) continue;
            Assert.Equal(shape >= 3, value.Map != null);
            if (shape == 2) continue;
            Assert.NotSame(map, value.Map);
            Assert.Equal(shape == 3 ? 0 : shape == 4 ? 1 : 3, value.Map.Count);
            if (shape == 4) Assert.Null(value.Map[ResourceType.Ammo]);
            if (shape == 5)
            {
                Assert.NotSame(map![ResourceType.Hp], value.Map[ResourceType.Hp]);
                Assert.Equal(long.MinValue + 11, value.Map[ResourceType.Hp].Current.RawValue);
                Assert.Equal(long.MaxValue - 13, value.Map[ResourceType.Hp].LastMax.RawValue);
                Assert.Equal(-987, value.Map[ResourceType.Hp].MaxAttribute.RawId);
                Assert.Equal(0, value.Map[ResourceType.Mana].Current.RawValue);
                Assert.Equal(-1, value.Map[ResourceType.Mana].LastMax.RawValue);
                Assert.Equal(0, value.Map[ResourceType.Mana].MaxAttribute.RawId);
                Assert.Null(value.Map[ResourceType.None]);
            }
            Assert.Equal(saved, provider.Export(Frame));
        }
        if (entity.hasResourceContainer) entity.RemoveResourceContainer();
        provider.Import(Frame, saved);
        Assert.Equal(shape > 0, entity.hasResourceContainer);
        Assert.Equal(saved, provider.Export(Frame));
    }

    [Fact]
    public void Dictionary_order_is_canonical_and_unknown_export_keys_are_rejected()
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        var hp = State(123, 456, 789);
        var mana = State(12, 34, 56);
        entity.AddResourceContainer(new ResourceContainer { Map = new()
        { [ResourceType.Mana] = mana, [ResourceType.Hp] = hp } }, true);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var first = provider.Export(Frame);
        entity.resourceContainer.Value.Map = new() { [ResourceType.Hp] = hp, [ResourceType.Mana] = mana };
        Assert.Equal(first, provider.Export(Frame));
        entity.resourceContainer.Value.Map.Add((ResourceType)999, hp);
        Assert.Throws<InvalidDataException>(() => provider.Export(Frame));
    }

    [Theory]
    [InlineData("enum")]
    [InlineData("duplicate")]
    [InlineData("negative-count")]
    [InlineData("huge-count")]
    [InlineData("presence")]
    [InlineData("truncated")]
    [InlineData("old-version")]
    public void Malformed_resource_is_rejected_before_any_provider_import(string fault)
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        entity.AddResourceContainer(new ResourceContainer { Map = new()
        { [ResourceType.Hp] = State(Marker, 100, 10), [ResourceType.Mana] = State(Marker + 1, 200, 20) } }, true);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var target = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(provider.Export(Frame));
        var data = target.Entries[0].Data.ToArray();
        var rawOffset = Find(data, BitConverter.GetBytes(Marker));
        var keyOffset = rawOffset - 5;
        switch (fault)
        {
            case "enum": BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(keyOffset), 999); break;
            case "duplicate": BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(rawOffset + 20), (int)ResourceType.Hp); break;
            case "negative-count": BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(keyOffset - 4), -2); break;
            case "huge-count": BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(keyOffset - 4), int.MaxValue); break;
            case "presence": data[rawOffset - 1] = 2; break;
            case "truncated": data = data[..(rawOffset + 3)]; break;
        }
        var bad = MemoryPackSerializer.Serialize(new MobaEntitasComponentRollbackPayload(
            fault == "old-version" ? 3 : MobaEntitasComponentRollbackProvider.CurrentVersion,
            new[] { new MobaEntitasComponentRollbackEntry(1, 0, data) }));
        entity.resourceContainer.Value.Map[ResourceType.Hp].Current = Fixed64.FromRaw(111);
        var live = provider.Export(Frame);
        var early = new CountingProvider();
        var registry = new RollbackRegistry();
        registry.Register(early);
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = coordinator.Capture(Frame);
        var invalid = new WorldRollbackSnapshot(snapshot.Version, Frame, snapshot.Entries.Select(entry =>
            entry.Key == provider.Key ? new WorldRollbackSnapshotEntry(entry.Key, bad) : entry).ToArray());
        Assert.ThrowsAny<Exception>(() => provider.ValidateImport(Frame, bad));
        Assert.ThrowsAny<Exception>(() => provider.Import(Frame, bad));
        Assert.Equal(live, provider.Export(Frame));
        Assert.False(coordinator.TryRestore(in invalid, out var failure));
        Assert.Equal(0, failure.ProviderCount);
        Assert.Equal(0, early.Imports);
        Assert.Equal(live, provider.Export(Frame));
    }

    [Fact]
    public void Hp_provider_restores_only_base_value_and_rejects_old_layout()
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        var attrs = new AttributeContext();
        var group = attrs.GetGroupFor(MobaAttributeIds.HP);
        entity.AddAttributeGroup(group, attrs);
        group.SetBase(MobaAttributeIds.HP, 123f);
        entity.AddResourceContainer(new ResourceContainer { Map = new() { [ResourceType.Hp] = State(321, 999, 0) } }, true);
        var actors = new MobaActorRegistry();
        actors.Register(1, entity);
        var provider = new MobaActorHpRollbackProvider(actors);
        var target = provider.Export(Frame);
        group.SetBase(MobaAttributeIds.HP, 456f);
        entity.resourceContainer.Value.Map[ResourceType.Hp].Current = Fixed64.FromRaw(654);
        provider.Import(Frame, target);
        Assert.Equal(123f, group.GetOrCreate(MobaAttributeIds.HP).BaseValue);
        Assert.Equal(654, entity.resourceContainer.Value.Map[ResourceType.Hp].Current.RawValue);
        var old = MemoryPackSerializer.Serialize(new MobaActorHpRollbackPayload(2,
            new[] { new MobaActorHpRollbackEntry(1, 999) }));
        Assert.Throws<NotSupportedException>(() => provider.ValidateImport(Frame, old));
        Assert.Throws<NotSupportedException>(() => provider.Import(Frame, old));
        Assert.Equal(target, provider.Export(Frame));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Checkpoint_restores_resources_and_invalid_late_component_is_preflighted(bool populated)
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        entity.AddResourceContainer(new ResourceContainer { Map = populated ? new()
            { [ResourceType.Hp] = State(123, 456, 0) } : new() }, true);
        var component = new MobaEntitasComponentRollbackProvider(context);
        var early = new CountingProvider();
        var coordinator = new MobaDeterministicCheckpointCoordinator("resource-test", "Moba", 30,
            new IMobaStateRecoveryProvider[] { early, component });
        var target = coordinator.Capture(Frame);
        entity.resourceContainer.Value.Map[ResourceType.Rage] = State(1, 2, 0);
        coordinator.Restore(target);
        Assert.Equal(populated ? 1 : 0, entity.resourceContainer.Value.Map.Count);
        Assert.Equal(target.StateHash, coordinator.ComputeStateHash(Frame));
        early.Imports = 0;
        target.Entries[1] = new MobaStateRecoveryEntry(component.Key,
            MemoryPackSerializer.Serialize(new MobaEntitasComponentRollbackPayload(3, Array.Empty<MobaEntitasComponentRollbackEntry>())));
        Assert.ThrowsAny<Exception>(() => coordinator.Restore(target));
        Assert.Equal(0, early.Imports);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Resource_removal_clears_predicted_context_without_attribute_events(int shape)
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        var attrs = new AttributeContext();
        entity.AddAttributeGroup(attrs.GetGroupFor(MobaAttributeIds.HP), attrs);
        entity.AddResourceContainer(shape == 0 ? null : new ResourceContainer { Map = shape == 1 ? null : new() }, true);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var target = provider.Export(Frame);
        entity.ReplaceResourceContainer(new ResourceContainer { Map = new()
        { [ResourceType.Rage] = State(Fixed64.One.RawValue, Fixed64.One.RawValue, 0) } }, false);
        MobaResourceAttributeContextProjector.Refresh(entity);
        Assert.Equal(1f, attrs.GetFloat("resource.rage.ratio"));
        var changed = 0;
        attrs.AttributeChanged += (_, _, _, _) => changed++;
        provider.Import(Frame, target);
        Assert.Equal(0f, attrs.GetFloat("resource.rage.current"));
        Assert.Equal(0f, attrs.GetFloat("resource.rage.max"));
        Assert.Equal(0f, attrs.GetFloat("resource.rage.ratio"));
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Restore_projects_dirty_live_max_silently_without_changing_resource_snapshot()
    {
        var context = new ActorContext();
        var entity = context.CreateEntity();
        entity.AddActorId(1);
        var attrs = new AttributeContext();
        var maxId = AttributeRegistry.DefaultRegistry.Register(new AttributeDef($"resource_max_{Guid.NewGuid():N}"));
        var group = attrs.GetGroupFor(maxId);
        var instance = group.GetOrCreate(maxId);
        entity.AddAttributeGroup(group, attrs);
        entity.AddResourceContainer(new ResourceContainer { Map = new()
        {
            [ResourceType.Mana] = new ResourceState
            {
                Current = MobaResourceFixedConvert.ToFixed(75),
                LastMax = MobaResourceFixedConvert.ToFixed(100),
                MaxAttribute = maxId
            }
        } }, true);
        attrs.SetBase(maxId, 100);
        MobaResourceAttributeContextProjector.Refresh(entity);
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var saved = provider.Export(Frame);
        attrs.AddModifier(maxId, AbilityKit.Modifiers.ModifierOp.Add, 50);
        int instanceEvents = 0, groupEvents = 0, contextEvents = 0;
        instance.Changed += (_, _, _) => instanceEvents++;
        group.AttributeChanged += (_, _, _) => groupEvents++;
        attrs.AttributeChanged += (_, _, _, _) => contextEvents++;
        entity.resourceContainer.Value.Map[ResourceType.Mana].Current = Fixed64.Zero;
        provider.Import(Frame, saved);
        Assert.Equal(75, attrs.GetFloat("resource.mana.current"));
        Assert.Equal(150, attrs.GetFloat("resource.mana.max"));
        Assert.Equal(0.5f, attrs.GetFloat("resource.mana.ratio"));
        Assert.Equal(saved, provider.Export(Frame));
        Assert.Equal((0, 0, 0), (instanceEvents, groupEvents, contextEvents));
        Assert.Equal(150, attrs.GetValue(maxId));
        Assert.Equal((0, 0, 0), (instanceEvents, groupEvents, contextEvents));
        attrs.SetBase(maxId, 200);
        MobaResourceAttributeContextProjector.Refresh(entity);
        Assert.Equal(250, attrs.GetFloat("resource.mana.max"));
        Assert.Equal((1, 1, 1), (instanceEvents, groupEvents, contextEvents));
    }

    [Fact]
    public void Removed_resource_provider_key_and_old_checkpoint_schema_reject_before_import()
    {
        var early = new CountingProvider();
        var registry = new RollbackRegistry();
        registry.Register(early);
        var rollback = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var target = rollback.Capture(Frame);
        var old = new WorldRollbackSnapshot(target.Version, Frame, target.Entries.Append(
            new WorldRollbackSnapshotEntry(10013, new byte[] { 1 })).ToArray());
        Assert.False(rollback.TryRestore(in old, out var failure));
        Assert.Equal(0, failure.ProviderCount);
        Assert.Equal(0, early.Imports);

        var checkpoint = new MobaDeterministicCheckpointCoordinator("resources", "Moba", 30,
            new IMobaStateRecoveryProvider[] { early });
        var current = checkpoint.Capture(Frame);
        var legacy = new MobaDeterministicCheckpoint(2, current.WorldId, current.WorldType,
            current.TickRate, current.Frame, current.StateHash, current.Entries);
        Assert.ThrowsAny<Exception>(() => checkpoint.Restore(legacy));
        Assert.Equal(0, early.Imports);
    }

    private static ResourceState State(long current, long max, int attribute) => new()
    { Current = Fixed64.FromRaw(current), LastMax = Fixed64.FromRaw(max), MaxAttribute = AttributeId.FromRaw(attribute) };

    private static int Find(byte[] data, byte[] pattern)
    {
        for (var i = 0; i <= data.Length - pattern.Length; i++)
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        throw new InvalidOperationException("Resource marker missing from generated payload.");
    }

    private sealed class CountingProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public int Key => 1;
        public string Name => "Import counter";
        public int Imports;
        public byte[] Export(FrameIndex frame) => new byte[] { 0 };
        public byte[] ExportState(FrameIndex frame) => Export(frame);
        public void Import(FrameIndex frame, byte[] payload) => Imports++;
        public void ImportState(FrameIndex frame, byte[] payload) => Import(frame, payload);
        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash) => hash.AddInt(0);
    }
}
