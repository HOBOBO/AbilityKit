using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaSkillRuntimeFrozenParametersRollbackTests
{
    private static readonly FrameIndex Frame = new(13);

    [Fact]
    public void Standalone_roundtrip_preserves_all_frozen_values_and_declaration_order()
    {
        var service = new MobaSkillCastRuntimeService();
        var provider = new MobaSkillRuntimeRollbackProvider(service);
        var runtime = CreateRuntime(service, 8, new("Radius", 3.25f), new("damage", 27.75f), new("Zero", 0f));
        var empty = CreateRuntime(service, 0);
        var checkpoint = provider.Export(Frame);
        var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(checkpoint);
        var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
        Assert.Equal(4, state.Version);
        Assert.Equal(8, state.Runtimes[0].ChampionLevel);
        Assert.Equal(new[] { "damage", "Radius", "Zero" }, state.Runtimes[0].LevelParameters.Select(value => value.Name));
        Assert.Empty(state.Runtimes[1].LevelParameters);

        var changed = new[] { new MobaSkillRuntimeLevelParameterRollbackEntry("replacement", 100f, 0) };
        provider.Import(Frame, RewriteRuntimeState(local, state, state.Version, WithFrozenData(state.Runtimes[0], 8, changed)));
        Assert.False(runtime.LevelParameters.TryGetValue("damage", out _));
        provider.Import(Frame, checkpoint);

        Assert.True(service.TryGet(runtime.Handle, out var restored));
        Assert.Same(runtime, restored);
        Assert.Equal(8, restored.ChampionLevel);
        Assert.Equal(new[] { "Radius", "damage", "Zero" }, restored.LevelParameters.ToArray().Select(value => value.Name));
        Assert.True(restored.LevelParameters.TryGetValue("DAMAGE", out var damage));
        Assert.Equal(27.75f, damage);
        Assert.True(restored.LevelParameters.TryGetValue("radius", out var radius));
        Assert.Equal(3.25f, radius);
        Assert.True(restored.LevelParameters.TryGetValue("zero", out var zero));
        Assert.Equal(0f, zero);
        Assert.Equal(0, empty.ChampionLevel);
        Assert.Equal(0, empty.LevelParameters.Count);
        Assert.Equal(checkpoint, provider.Export(Frame));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("null-parameters")]
    [InlineData("empty-name")]
    [InlineData("duplicate-name")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("declaration-order")]
    [InlineData("unsorted-names")]
    [InlineData("negative-level")]
    [InlineData("mismatched-live-level")]
    public void Invalid_frozen_data_is_rejected_before_standalone_import(string corrupt)
    {
        var service = new MobaSkillCastRuntimeService();
        var provider = new MobaSkillRuntimeRollbackProvider(service);
        var runtime = CreateRuntime(service, 8, new MobaSkillLevelParameterValue("damage", 27.75f));
        var baseline = provider.Export(Frame);
        var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(baseline);
        var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
        var parameters = corrupt switch
        {
            "null-parameters" => null,
            "empty-name" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new(" ", 1, 0) },
            "duplicate-name" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("Damage", 1, 0), new("damage", 2, 1) },
            "nan" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("damage", float.NaN, 0) },
            "infinity" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("damage", float.PositiveInfinity, 0) },
            "declaration-order" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("damage", 1, 0), new("radius", 2, 0) },
            "unsorted-names" => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("radius", 1, 0), new("damage", 2, 1) },
            _ => new MobaSkillRuntimeLevelParameterRollbackEntry[] { new("damage", 1, 0) },
        };
        var level = corrupt == "negative-level" ? -1 : corrupt == "mismatched-live-level" ? 9 : 8;
        var version = corrupt == "legacy" ? 3 : state.Version;
        var payload = RewriteRuntimeState(local, state, version, WithFrozenData(state.Runtimes[0], level, parameters));

        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, payload));
        Assert.Throws<InvalidOperationException>(() => provider.Import(Frame, payload));
        var corruptedLocal = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(payload);
        Assert.Throws<InvalidOperationException>(() => provider.ImportState(Frame, corruptedLocal.RuntimeState));
        Assert.True(service.TryGet(runtime.Handle, out var live));
        Assert.Same(runtime, live);
        Assert.Equal(baseline, provider.Export(Frame));
    }

    internal static MobaSkillRuntimeRollbackEntry WithFrozenData(MobaSkillRuntimeRollbackEntry entry, int level,
        MobaSkillRuntimeLevelParameterRollbackEntry[]? parameters) => new(
        entry.RuntimeId, entry.Generation, entry.RootContextId, entry.SkillId, entry.SkillSlot, entry.SkillLevel,
        entry.Sequence, entry.CasterActorId, entry.TargetActorId, entry.AimX, entry.AimY, entry.AimZ, entry.DirX,
        entry.DirY, entry.DirZ, entry.Stage, entry.PipelineEnded, entry.IsEnding, entry.IsEnded, entry.EndReason,
        entry.Children, entry.BlackboardEntries, entry.BaseSkillId, level, parameters!);

    private static byte[] RewriteRuntimeState(MobaSkillRuntimeLocalRollbackPayload local, MobaSkillRuntimeRollbackPayload state,
        int version, MobaSkillRuntimeRollbackEntry replacement)
    {
        var entries = (MobaSkillRuntimeRollbackEntry[])state.Runtimes.Clone();
        entries[0] = replacement;
        var rewritten = new MobaSkillRuntimeRollbackPayload(version, state.NextRuntimeId, state.NextRetainId,
            state.NextGeneration, entries, state.Retains);
        return MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(local.Version,
            MemoryPackSerializer.Serialize(rewritten), local.ExecutionContextNodes, local.ExecutionContextNextId));
    }

    private static MobaSkillCastRuntime CreateRuntime(MobaSkillCastRuntimeService service, int championLevel,
        params MobaSkillLevelParameterValue[] parameters)
    {
        var request = new MobaSkillCastRuntimeCreateRequest(10, 1, 3, 1, 1, 2, Vec3.Zero, Vec3.Forward, 0,
            levelParameters: new MobaSkillLevelParameterSnapshot(parameters), championLevel: championLevel);
        return service.Create(in request);
    }
}
