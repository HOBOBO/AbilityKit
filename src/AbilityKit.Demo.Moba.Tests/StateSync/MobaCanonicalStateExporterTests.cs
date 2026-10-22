using System;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaCanonicalStateExporterTests
{
    [Fact]
    public void Capture_orders_actors_and_resources_and_preserves_raw_values()
    {
        using var actors = new MobaActorRegistry();
        var first = new ActorContext().CreateEntity();
        first.AddTransform(new Transform3(
            new Vec3(1.25f, -2f, 3f),
            new Quat(0f, 0.5f, 0f, 1f),
            new Vec3(1f, 2f, 3f)));
        first.AddResourceContainer(new ResourceContainer
        {
            Map = new()
            {
                [ResourceType.Mana] = new ResourceState
                {
                    Current = Fixed64.FromRaw(-1234567890123L),
                    LastMax = Fixed64.FromRaw(9876543210987L),
                    MaxAttribute = AttributeId.FromRaw(77),
                },
                [ResourceType.Hp] = new ResourceState
                {
                    Current = Fixed64.FromRaw(42),
                    LastMax = Fixed64.FromRaw(84),
                    MaxAttribute = AttributeId.FromRaw(78),
                },
            },
        }, true);

        var second = new ActorContext().CreateEntity();
        second.AddTransform(Transform3.Identity);
        actors.Register(20, second);
        actors.Register(10, first);

        var file = new MobaCanonicalStateExporter(actors, "world-a", "build-1", "config-1")
            .Capture(new FrameIndex(37));

        Assert.Equal(1, file.SchemaVersion);
        Assert.Equal("world-a", file.WorldId);
        Assert.Equal("build-1", file.BuildId);
        Assert.Equal("config-1", file.ConfigHash);
        Assert.All(file.Entries, entry => Assert.Equal(37, entry.Frame));
        Assert.Equal(
            file.Entries.OrderBy(entry => entry.EntityId)
                .ThenBy(entry => entry.ComponentId)
                .ThenBy(entry => entry.FieldId),
            file.Entries);

        var positionX = Assert.Single(file.Entries, entry =>
            entry.EntityId == 10 && entry.ComponentId == MobaCanonicalStateExporter.ComponentTransform &&
            entry.FieldId == MobaCanonicalStateExporter.FieldTransformPositionX);
        Assert.Equal("1067450368", positionX.RawValue);
        Assert.Equal("1.25", positionX.DisplayValue);
        Assert.Equal(CanonicalValueKind.Unsigned, positionX.ValueKind);

        var manaCurrent = Assert.Single(file.Entries, entry =>
            entry.EntityId == 10 && entry.ComponentId == MobaCanonicalStateExporter.ComponentResources + (int)ResourceType.Mana &&
            entry.FieldId == MobaCanonicalStateExporter.FieldResourceCurrent);
        Assert.Equal("-1234567890123", manaCurrent.RawValue);
        Assert.Equal(CanonicalValueKind.FixedRaw, manaCurrent.ValueKind);

        var maxAttribute = Assert.Single(file.Entries, entry =>
            entry.EntityId == 10 && entry.ComponentId == MobaCanonicalStateExporter.ComponentResources + (int)ResourceType.Mana &&
            entry.FieldId == MobaCanonicalStateExporter.FieldResourceMaxAttribute);
        Assert.Equal("77", maxAttribute.RawValue);
        Assert.Equal(CanonicalValueKind.Signed, maxAttribute.ValueKind);
    }

    [Fact]
    public void Capture_omits_actors_with_missing_components()
    {
        using var actors = new MobaActorRegistry();
        var noComponents = new ActorContext().CreateEntity();
        actors.Register(2, noComponents);

        var file = new MobaCanonicalStateExporter(actors, "world").Capture(new FrameIndex(5));

        Assert.Empty(file.Entries);
    }
}
