using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Markers;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaBattleRouteManifestTests
{
    private static readonly int[] RuntimeInputOpCodes =
    {
        MobaOpCodes.Input.Move,
        MobaOpCodes.Input.SkillInput,
        MobaOpCodes.Input.DebugSpawnUnit,
        MobaOpCodes.Input.DebugReplaceHero,
    };

    [Fact]
    public void GeneratedRouteManifest_MatchesLegacyRuntimeAssemblyScan()
    {
        var generated = new MobaBattleRouteRegistry();
        var reflected = new MobaBattleRouteRegistry();

        var generatedCount = MobaGeneratedBattleRouteManifest.Register(generated);
        MarkerScanner<MobaBattleRouteAttribute>.Scan(
            new[] { typeof(MobaBattleRouteRegistry).Assembly },
            reflected);

        Assert.Equal(reflected.Count, generatedCount);
        Assert.Equal(reflected.Count, generated.Count);
        foreach (var expected in reflected.Descriptors)
        {
            Assert.True(generated.TryGet(expected.Kind, expected.OpCode, out var actual));
            Assert.Equal(expected.OwnerType, actual.OwnerType);
            Assert.Equal(expected.PayloadType, actual.PayloadType);
            Assert.Equal(expected.HandlerType, actual.HandlerType);
            Assert.Equal(expected.Name, actual.Name);
        }
    }

    [Fact]
    public void GeneratedInputManifest_MatchesLegacyRuntimeAssemblyScan()
    {
        var generated = new MobaInputCommandHandlerRegistry();
        var reflected = new MobaInputCommandHandlerRegistry();

        var generatedCount = MobaGeneratedInputCommandHandlerManifest.Register(generated);
        MarkerScanner<MobaInputCommandHandlerAttribute>.Scan(
            new[] { typeof(MobaInputCommandHandlerRegistry).Assembly },
            reflected);

        Assert.Equal(reflected.DescriptorCount, generatedCount);
        Assert.Equal(reflected.DescriptorCount, generated.DescriptorCount);
        foreach (var opCode in RuntimeInputOpCodes)
        {
            Assert.True(reflected.TryGetHandlerDescriptor(opCode, out var expected));
            Assert.True(generated.TryGetHandlerDescriptor(opCode, out var actual));
            Assert.Equal(expected.HandlerType, actual.HandlerType);
        }
    }

    [Fact]
    public void DefaultScans_KeepExternalHandlerDiscovery()
    {
        AppContext.SetSwitch("AbilityKit.Moba.DisableBattleRouteReflectionFallback", true);
        AppContext.SetSwitch("AbilityKit.Moba.DisableInputCommandHandlerReflectionFallback", true);
        try
        {
            var routes = MobaBattleRouteRegistry.CreateDefault();
            var inputs = MobaInputCommandHandlerRegistry.CreateScanned();

            Assert.True(routes.TryGet(MobaBattleRouteKind.RuntimeInput, 999, out var route));
            Assert.Equal(typeof(ExternalInputCommandHandler), route.OwnerType);
            Assert.Equal(typeof(ExternalInputCommandHandler), route.HandlerType);
            Assert.True(inputs.TryGetHandlerDescriptor(999, out var input));
            Assert.Equal(typeof(ExternalInputCommandHandler), input.HandlerType);
        }
        finally
        {
            AppContext.SetSwitch("AbilityKit.Moba.DisableBattleRouteReflectionFallback", false);
            AppContext.SetSwitch("AbilityKit.Moba.DisableInputCommandHandlerReflectionFallback", false);
        }
    }

    [MobaInputCommandHandler(999)]
    private sealed class ExternalInputCommandHandler : IMobaInputCommandHandler
    {
        public bool Handle(
            MobaInputCommandContext context,
            FrameIndex frame,
            PlayerInputCommand command,
            out MobaInputCommandResult result)
        {
            result = default;
            return false;
        }
    }
}
