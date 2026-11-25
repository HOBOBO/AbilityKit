using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Protocol.Moba.StateSync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class MobaColdReplayFrameZeroTests
{
    [Fact]
    public void Authority_resource_regeneration_runs_in_server_system_and_state_sync_overhead_projection()
    {
        const string battleId = "server-mana-regeneration";
        const float delta = 1f / 30f;
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var session = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        Assert.True(world.Services.Resolve<MobaActorRegistry>().TryGet(1, out var actor));
        new MobaAttrs(actor).Mana = 100;
        var before = actor.resourceContainer.Value.Map[ResourceType.Mana].Current;
        var clock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(session);
        for (var frame = 1; frame <= 30; frame++)
        {
            Assert.True(clock.TryPrepareExternalFrame(frame, delta));
            Assert.True(session.Tick(frame, 30, delta));
        }
        var after = actor.resourceContainer.Value.Map[ResourceType.Mana].Current;
        Assert.InRange((after - before).ToDouble(), 1.599, 1.601);
        var projection = Assert.IsAssignableFrom<IFixedBaselineBattleSnapshotSource>(session)
            .CaptureFixedBaselineProjection(1, 30);
        Assert.True(MobaStateSyncCuePayloadCodec.TryDecode(projection.Payload!, out var op,
            out _, out var payload, out _));
        Assert.Equal(MobaStateSyncOverheadPayloadCodec.OpCode, op);
        var overhead = MobaStateSyncOverheadPayloadCodec.Decode(payload);
        var mana = Assert.Single(MobaActorIndicatorSnapshotCodec.Deserialize(overhead.Indicators),
            row => row.ActorId == 1 && row.IndicatorId == 2);
        Assert.Equal((float)after.ToDouble(), mana.Current);
        Assert.Equal(actor.attributeGroup.Group.GetValue(MobaAttributeIds.MAX_MANA), mana.Max);
    }
}
