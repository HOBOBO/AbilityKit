using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class FormalLobbyCommandCoordinatorTests
{
    [Fact]
    public async Task PrepareFailureClearsPreparedRoomMarker()
    {
        using var runtime = AttachedRuntime();
        runtime.MarkPrepared("room-a");
        var room = new TestRoomCommandPort
        {
            CurrentSnapshot = new MultiplayerRoomSnapshot { RoomId = "room-a" },
            LocalPlayerId = 7u,
            PickHeroError = new InvalidOperationException("loadout rejected")
        };
        var coordinator = new FormalLobbyCommandCoordinator(runtime, room);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.PrepareDefaultLoadoutAsync(
                Loadout(heroId: 1001),
                CurrentContext(runtime)));

        Assert.Equal("loadout rejected", error.Message);
        Assert.Equal(string.Empty, runtime.PreparedRoomId);
        Assert.Equal(new[] { "pick:1001" }, room.Calls);
    }

    [Fact]
    public async Task LeaveRoomAndRefreshRunsInOrderAndClearsAutomationMarkers()
    {
        using var runtime = AttachedRuntime();
        runtime.MarkPrepared("room-a");
        runtime.MarkAutomaticStart("room-a");
        var room = new TestRoomCommandPort { CurrentRoomId = "room-a" };
        var coordinator = new FormalLobbyCommandCoordinator(runtime, room);

        await coordinator.LeaveRoomAndRefreshAsync(
            _ =>
            {
                room.Calls.Add("refresh");
                return Task.CompletedTask;
            },
            CurrentContext(runtime));

        Assert.Equal(new[] { "leave", "refresh" }, room.Calls);
        Assert.Equal(string.Empty, runtime.PreparedRoomId);
        Assert.Equal(string.Empty, runtime.AutomaticStartRoomId);
    }

    [Fact]
    public async Task LateLeaveCompletionAfterDetachDoesNotRefresh()
    {
        using var runtime = AttachedRuntime();
        var leaveCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var room = new TestRoomCommandPort
        {
            CurrentRoomId = "room-a",
            LeaveTask = leaveCompletion.Task
        };
        var coordinator = new FormalLobbyCommandCoordinator(runtime, room);
        var refreshCount = 0;

        var operation = coordinator.LeaveRoomAndRefreshAsync(
            _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            CurrentContext(runtime));
        runtime.Detach();
        leaveCompletion.SetResult(true);
        await operation;

        Assert.Equal(0, refreshCount);
        Assert.Equal(new[] { "leave" }, room.Calls);
    }

    private static FormalLobbyRuntime AttachedRuntime()
    {
        var runtime = new FormalLobbyRuntime();
        runtime.Attach();
        return runtime;
    }

    private static LobbyOperationContext CurrentContext(FormalLobbyRuntime runtime)
    {
        return new LobbyOperationContext(
            runtime.CaptureAttachmentGeneration(),
            operationGeneration: 0,
            CancellationToken.None);
    }

    private static MultiplayerLoadoutSpec Loadout(int heroId)
    {
        return new MultiplayerLoadoutSpec(
            heroId,
            teamId: 1,
            spawnPointId: 1,
            level: 1,
            attributeTemplateId: 1,
            basicAttackSkillId: 1,
            skillIds: Array.Empty<int>());
    }

    private sealed class TestRoomCommandPort : ILobbyRoomCommandPort
    {
        public List<string> Calls { get; } = new();
        public MultiplayerRoomFlowState CurrentState { get; set; }
        public MultiplayerRoomSnapshot CurrentSnapshot { get; set; }
        public string CurrentRoomId { get; set; } = string.Empty;
        public uint LocalPlayerId { get; set; }
        public bool CanLeaveCurrentRoom { get; set; } = true;
        public Exception PickHeroError { get; set; }
        public Task LeaveTask { get; set; } = Task.CompletedTask;

        public Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("restore");
            return Task.FromResult(default(MultiplayerRoomRestoreResult));
        }

        public Task StartCreateRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("create");
            return Task.CompletedTask;
        }

        public Task StartJoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("join:" + roomId);
            return Task.CompletedTask;
        }

        public Task PickHeroAsync(
            MultiplayerLoadoutSpec loadout,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("pick:" + loadout.HeroId);
            return PickHeroError != null
                ? Task.FromException(PickHeroError)
                : Task.CompletedTask;
        }

        public Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
        {
            Calls.Add("ready:" + ready);
            return Task.CompletedTask;
        }

        public Task BeginLoadingAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("begin-loading");
            return Task.CompletedTask;
        }

        public Task CancelLoadingAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("cancel-loading");
            return Task.CompletedTask;
        }

        public Task LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("leave");
            return LeaveTask;
        }

        public void Cancel()
        {
            Calls.Add("cancel");
        }
    }
}
