using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.FrameSync;
using AbilityKit.Demo.Tiny.Server;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Tiny;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class TinyBattleIntegrationTests
{
    [Fact]
    public void FrameSync_LateInputRestoresPriorStateAndReplaysEmptyFrame()
    {
        var predicted = NewBattle();
        var session = new TinyFrameSyncSession(predicted);
        session.Predict(Array.Empty<TinyFrameInput>());
        session.Predict(Array.Empty<TinyFrameInput>());

        var authority = NewBattle();
        authority.Submit(1, new TinyInput(1, 0, true));
        authority.Tick();
        var firstHash = authority.ComputeHash();
        authority.Tick();

        Assert.Equal(TinyReconcileResult.Replayed, session.ApplyAuthoritative(1,
            new[] { new TinyFrameInput(1, new TinyInput(1, 0, true)) }, firstHash));
        Assert.Equal(authority.ComputeHash(), session.StateHash);
        Assert.Equal(authority.CaptureState(), predicted.CaptureState(), TinyBattleStateComparer.Instance);
    }

    [Fact]
    public void FrameSync_HashMismatchStopsPredictionUntilFullRecovery()
    {
        var battle = NewBattle();
        var session = new TinyFrameSyncSession(battle);
        session.Predict(Array.Empty<TinyFrameInput>());
        Assert.Equal(TinyReconcileResult.NeedsFullSnapshot,
            session.ApplyAuthoritative(1, Array.Empty<TinyFrameInput>(), session.StateHash + 1));
        Assert.True(session.RequiresFullSnapshot);
        Assert.Throws<InvalidOperationException>(() => session.Predict(Array.Empty<TinyFrameInput>()));

        var authority = NewBattle();
        authority.Tick();
        session.RestoreAuthoritativeFullState(authority.CaptureState());
        session.Predict(Array.Empty<TinyFrameInput>());
        Assert.False(session.RequiresFullSnapshot);
        Assert.Equal(2, session.Frame);
    }

    [Fact]
    public void FrameSync_ExpiredHistoryRequestsFullRecovery()
    {
        var session = new TinyFrameSyncSession(NewBattle(), historyCapacity: 2);
        for (var i = 0; i < 3; i++) session.Predict(Array.Empty<TinyFrameInput>());
        Assert.Equal(TinyReconcileResult.NeedsFullSnapshot,
            session.ApplyAuthoritative(1, Array.Empty<TinyFrameInput>(), 0));
        Assert.True(session.RequiresFullSnapshot);
    }

    [Fact]
    public void FrameSync_EmptyInputsAndHashesAreDeterministic()
    {
        var left = new TinyFrameSyncSession(NewBattle());
        var right = new TinyFrameSyncSession(NewBattle());
        for (var i = 0; i < 5; i++)
        {
            left.Predict(Array.Empty<TinyFrameInput>());
            right.Predict(Array.Empty<TinyFrameInput>());
            Assert.Equal(left.StateHash, right.StateHash);
            Assert.Equal(TinyReconcileResult.Matched,
                left.ApplyAuthoritative(left.Frame, Array.Empty<TinyFrameInput>(), right.StateHash));
        }
    }

    private static TinyBattle NewBattle()
    {
        var battle = new TinyBattle();
        battle.AddPlayer(1, -1, 0);
        battle.AddPlayer(2, 1, 0);
        return battle;
    }

    private sealed class TinyBattleStateComparer : IEqualityComparer<TinyBattleState>
    {
        public static readonly TinyBattleStateComparer Instance = new();
        public bool Equals(TinyBattleState x, TinyBattleState y) =>
            x.Frame == y.Frame && x.Actors.SequenceEqual(y.Actors);
        public int GetHashCode(TinyBattleState obj) => obj.Frame;
    }

    [Fact]
    public void CoreRules_MoveAndCooldownRemainDeterministic()
    {
        var battle = new TinyBattle();
        battle.AddPlayer(1, -1, 0);
        battle.AddPlayer(2, 1, 0);
        battle.Submit(1, new TinyInput(1, 0, true));
        battle.Tick();
        var firstHash = battle.ComputeHash();

        Assert.Equal(90, battle.Actors.Single(actor => actor.PlayerId == 2).Hp);
        Assert.Equal(TinyBattle.AttackCooldownFrames, battle.Actors.Single(actor => actor.PlayerId == 1).CooldownFrames);
        battle.Submit(1, new TinyInput(0, 0, true));
        battle.Tick();
        Assert.Equal(90, battle.Actors.Single(actor => actor.PlayerId == 2).Hp);
        Assert.NotEqual(firstHash, battle.ComputeHash());
    }

    [Fact]
    public void RegisteredModule_DeclaresIndependentStateSyncProfile()
    {
        var catalog = new ServerGameplayModuleCatalog(new[] { TinyServerGameplayModule.Create() });
        var module = catalog.ResolveModule(TinyGameplay.RoomType);
        var capability = module.ResolveSyncCapabilities(null, TinyGameplay.StateSyncTemplate);
        using var manager = new ServerBattleWorldManager(NullLogger.Instance, catalog);

        Assert.Equal(TinyGameplay.RoomType, Assert.Single(catalog.GameplayCatalog.Descriptors).RoomType);
        Assert.Equal(TinyGameplay.RoomType, catalog.GameplayCatalog.DefaultDescriptor.RoomType);
        Assert.IsType<TinyBattleRuntimeAdapter>(
            Assert.Single(catalog.CreateBattleRuntimeAdapters(manager)));
        Assert.IsType<TinyRoomGameplayAdapter>(module.CreateRoomAdapter());
        Assert.Equal(TinyGameplay.WorldType, Assert.Single(module.CreateWorldBlueprints()).WorldType);
        Assert.Equal(1, capability.MinimumSchemaVersion);
        Assert.Equal(1, capability.MaximumSchemaVersion);
        Assert.True(module.SyncProfile.SupportsStateSyncPush);
    }

    [Fact]
    public void CustomCatalog_ResolvesTinyAlongsideAnotherRoomType()
    {
        var shooter = ServerGameplayModuleCatalog.Default.ResolveModule(ShooterGameplay.RoomType);
        var catalog = new ServerGameplayModuleCatalog(new[]
        {
            TinyServerGameplayModule.Create(),
            shooter
        });

        Assert.Equal(TinyGameplay.RoomType, catalog.GameplayCatalog.DefaultDescriptor.RoomType);
        Assert.Equal(TinyGameplay.RoomType, catalog.ResolveModule(null).RoomType);
        Assert.Equal(shooter.RoomType, catalog.ResolveModule(shooter.RoomType).RoomType);
        Assert.Equal(2, catalog.CreateRoomAdapters().Count);
    }

    [Fact]
    public void RoomState_RestoresPlayerSlotsAndReadyFlags()
    {
        var room = new TinyRoomGameplayAdapter();
        var summary = Summary();
        var state = room.CreateState(summary);
        room.Join(state, summary, Array.Empty<string>(), "alice");
        room.Join(state, summary, new[] { "alice" }, "bob");
        room.SetReady(state, new RoomReadyRequest("alice", true));
        room.SetReady(state, new RoomReadyRequest("bob", true));

        var restored = room.RestorePersistentState(summary, room.ExportPersistentState(state));

        Assert.True(room.CanStart(restored));
        Assert.Equal(new uint[] { 1, 2 }, room.BuildPlayerSnapshots(restored).Select(player => player.PlayerId));
    }

    [Fact]
    public void BattleSession_ProducesAuthoritativeFullState()
    {
        var modules = ServerGameplayModuleCatalog.Default.WithModule(TinyServerGameplayModule.Create());
        using var manager = new ServerBattleWorldManager(NullLogger.Instance, modules);
        using var session = new TinyBattleRuntimeAdapter(manager).CreateSession("tiny-test");
        var init = new BattleInitParams
        {
            WorldId = 42,
            TickRate = 30,
            Players = new List<PlayerInitInfo>
            {
                new() { PlayerId = 1, PosX = -1 },
                new() { PlayerId = 2, PosX = 1 }
            }
        };

        Assert.True(session.Start(init).Succeeded);
        var attack = new BattleInputItem
        {
            PlayerId = 1,
            OpCode = TinyBattle.InputOpCode,
            Payload = new TinyInput(0, 0, true).Encode()
        };
        Assert.True(session.ValidateInput(attack).Accepted);
        Assert.Equal(1, session.SubmitInputs(1, new[] { attack }));
        Assert.True(session.Tick(1, 30, 1f / 30));

        var push = session.CreateStateSyncPush(42, 1, isFullSnapshot: false);
        Assert.True(push.IsFullSnapshot);
        Assert.Equal(1, push.Frame);
        Assert.Equal(2, push.Actors.Count);
        Assert.Equal(90, push.Actors.Single(actor => actor.ActorId == 2).Hp);
        Assert.Equal(42UL, push.WorldId);
    }

    private static RoomSummary Summary() => new(
        "dev", "local", "tiny-room", TinyGameplay.RoomType, "Tiny", false,
        2, 2, "alice", 1, null);
}
