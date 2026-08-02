using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class StateSyncObserverSubscriptionStateTests
{
    [Fact]
    public void DecideSubscribe_WhenNotSubscribed_ReturnsSubscribe()
    {
        var state = new StateSyncObserverSubscriptionState();

        var decision = state.DecideSubscribe("battle-a");

        Assert.Equal(StateSyncObserverSubscriptionAction.Subscribe, decision.Action);
        Assert.Equal(string.Empty, decision.PreviousBattleKey);
    }

    [Fact]
    public void DecideSubscribe_WhenSameBattle_ReturnsRefreshFullSnapshot()
    {
        var state = new StateSyncObserverSubscriptionState();
        state.MarkSubscribed("battle-a");

        var decision = state.DecideSubscribe("battle-a");

        Assert.Equal(StateSyncObserverSubscriptionAction.RefreshFullSnapshot, decision.Action);
        Assert.Equal("battle-a", decision.PreviousBattleKey);
        Assert.True(state.IsSubscribed);
        Assert.Equal("battle-a", state.CurrentBattleKey);
    }

    [Fact]
    public void DecideSubscribe_WhenDifferentBattle_ReturnsSwitchBattle()
    {
        var state = new StateSyncObserverSubscriptionState();
        state.MarkSubscribed("battle-a");

        var decision = state.DecideSubscribe("battle-b");

        Assert.Equal(StateSyncObserverSubscriptionAction.SwitchBattle, decision.Action);
        Assert.Equal("battle-a", decision.PreviousBattleKey);
    }

    [Fact]
    public void Clear_WhenSubscribed_ResetsSubscription()
    {
        var state = new StateSyncObserverSubscriptionState();
        state.MarkSubscribed("battle-a");

        state.Clear();

        Assert.False(state.IsSubscribed);
        Assert.Equal(string.Empty, state.CurrentBattleKey);
    }
    [Fact]
    public void CreateObserverContext_PreservesAuthoritativeObserverMetadata()
    {
        var context = BattleLogicHostGrain.CreateObserverContext(new StateSyncObserverInfo
        {
            ObserverKey = "account-a:room-a",
            AccountId = "account-a",
            RoomId = "room-a"
        });

        Assert.Equal("account-a:room-a", context.ObserverKey);
        Assert.Equal("account-a", context.AccountId);
        Assert.Equal("room-a", context.RoomId);
    }

    [Fact]
    public void CreateObserverContext_WhenMetadataIsNull_UsesEmptyValues()
    {
        var context = BattleLogicHostGrain.CreateObserverContext(null);

        Assert.Equal(string.Empty, context.ObserverKey);
        Assert.Equal(string.Empty, context.AccountId);
        Assert.Equal(string.Empty, context.RoomId);
    }

    [Fact]
    public void SubscriptionContracts_RequireMetadataWithoutObserverInfoCallback()
    {
        var subscribe = typeof(IBattleLogicHostGrain).GetMethod(nameof(IBattleLogicHostGrain.SubscribeAsync));

        Assert.NotNull(subscribe);
        Assert.Equal(
            new[]
            {
                typeof(IStateSyncObserverGrain),
                typeof(StateSyncObserverInfo),
                typeof(ReliableBattleEventSubscribeCursor)
            },
            subscribe.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Null(typeof(IStateSyncObserverGrain).GetMethod("GetObserverInfoAsync"));
    }

    [Fact]
    public void GatewayPushBinding_StaleUnbindDoesNotClearReplacementObserver()
    {
        var binding = new StateSyncObserverGrain.StateSyncGatewayPushBinding();
        var oldObserver = new RecordingGatewayPushObserver();
        var newObserver = new RecordingGatewayPushObserver();
        var payload = new byte[] { 1, 2, 3 };

        binding.Bind("old-binding", oldObserver);
        binding.Bind("new-binding", newObserver);
        binding.Unbind("old-binding");

        Assert.True(binding.UsesGatewayPushObserver);
        Assert.True(binding.TryPush(42, payload));
        Assert.Empty(oldObserver.Pushes);
        var push = Assert.Single(newObserver.Pushes);
        Assert.Equal(42u, push.OpCode);
        Assert.Same(payload, push.Payload);

        binding.Unbind("new-binding");

        Assert.True(binding.UsesGatewayPushObserver);
        Assert.False(binding.TryPush(43, payload));
        Assert.Single(newObserver.Pushes);
    }

    private sealed class RecordingGatewayPushObserver : IStateSyncGatewayPushObserver
    {
        public List<(uint OpCode, byte[] Payload)> Pushes { get; } = new();

        public void OnPush(uint opCode, byte[] payload)
        {
            Pushes.Add((opCode, payload));
        }
    }
}
