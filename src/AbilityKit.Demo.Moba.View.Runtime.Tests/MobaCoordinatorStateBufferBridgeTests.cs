using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Coordinator;
using AbilityKit.Demo.Moba.Session;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaCoordinatorStateBufferBridgeTests
{
    [Fact]
    public void Adapter_appends_states_without_array_conversion()
    {
        var adapter = new MobaCoordinatorStateAdapter();
        var source = new List<LogicWorldEntityState>
        {
            new LogicWorldEntityState(17)
            {
                X = 1.5f,
                Hp = 80f,
                HpMax = 100f,
                TeamId = 2,
                IsDead = false,
            },
        };
        var destination = new List<SnapshotEntityState>();

        var count = adapter.AppendToCoordinatorStates(source, destination);

        Assert.Equal(1, count);
        var state = Assert.Single(destination);
        Assert.Equal(17, state.EntityId);
        Assert.True(state.TryGetPayload<EntityState>(out var payload));
        Assert.Equal(1.5f, payload.X);
        Assert.Equal(80f, payload.Hp);
        Assert.Equal(2, payload.TeamId);
    }
}
