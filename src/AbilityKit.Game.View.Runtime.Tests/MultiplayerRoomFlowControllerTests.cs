using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests
{
    /// <summary>
    /// MultiplayerRoomFlowController 的状态转换逻辑测试。
    /// 使用 stub 实现的 IMultiplayerRoomSession / IRoomSnapshotProvider，零 Unity/host.extension 依赖。
    /// </summary>
    public sealed class MultiplayerRoomFlowControllerTests
    {
        [Fact]
        public async Task StartCreateRoomAsync_Success_Idle_To_InLobby()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            var visited = new List<MultiplayerRoomFlowState>();
            controller.StateChanged += s => visited.Add(s);

            await controller.StartCreateRoomAsync(NewSpec());

            Assert.Equal(MultiplayerRoomFlowState.InLobby, controller.CurrentState);
            Assert.Equal(StubSession.CreatedRoomId, controller.CurrentRoomId);
            // Idle → LoggingIn → CreatingRoom → InLobby（状态转换路径）
            Assert.Equal(
                new[]
                {
                    MultiplayerRoomFlowState.LoggingIn,
                    MultiplayerRoomFlowState.CreatingRoom,
                    MultiplayerRoomFlowState.InLobby
                },
                visited);
        }

        [Fact]
        public async Task StartJoinRoomAsync_Success_Idle_To_InLobby()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            var visited = new List<MultiplayerRoomFlowState>();
            controller.StateChanged += s => visited.Add(s);

            await controller.StartJoinRoomAsync(NewSpec(), "room-xyz");

            Assert.Equal(MultiplayerRoomFlowState.InLobby, controller.CurrentState);
            Assert.Equal("room-xyz", controller.CurrentRoomId);
            // Idle → LoggingIn → JoiningRoom → InLobby（状态转换路径）
            Assert.Equal(
                new[]
                {
                    MultiplayerRoomFlowState.LoggingIn,
                    MultiplayerRoomFlowState.JoiningRoom,
                    MultiplayerRoomFlowState.InLobby
                },
                visited);
        }

        [Theory]
        [InlineData(MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading, MultiplayerRoomFlowState.InLobby)]
        [InlineData(MultiplayerRoomRestoreNextStep.ReportAssetsLoaded, MultiplayerRoomFlowState.LoadingAssets)]
        [InlineData(MultiplayerRoomRestoreNextStep.WaitForBattleStart, MultiplayerRoomFlowState.WaitingForBattle)]
        [InlineData(MultiplayerRoomRestoreNextStep.EnterBattle, MultiplayerRoomFlowState.InBattle)]
        public async Task RestoreAsync_UsesNextStepWithoutReplayingCompletedStages(
            MultiplayerRoomRestoreNextStep nextStep,
            MultiplayerRoomFlowState expectedState)
        {
            var session = new StubSession
            {
                RestoreResult = Restored(nextStep)
            };
            var controller = new MultiplayerRoomFlowController(
                session,
                new StubSnapshotProvider());

            var result = await controller.RestoreAsync(NewSpec(), fallbackPlayerId: 9u);

            Assert.True(result.HasActiveRoom);
            Assert.Equal(expectedState, controller.CurrentState);
            Assert.Equal("room-restored", controller.CurrentRoomId);
            Assert.Equal(1, session.RestoreCalls);
            Assert.False(session.SetReadyCalled);
            Assert.False(session.BeginLoadingCalled);
            Assert.False(session.ReportAssetsLoadedCalled);
            Assert.False(session.WaitForBattleStartCalled);
        }

        [Fact]
        public async Task RestoreAsync_NoActiveRoom_ReturnsIdleWithoutFailure()
        {
            var session = new StubSession
            {
                RestoreResult = new MultiplayerRoomRestoreResult(
                    string.Empty,
                    0UL,
                    9u,
                    MultiplayerRoomPhase.Closed,
                    MultiplayerRoomRestoreNextStep.None,
                    MultiplayerRoomEntryKind.TeamLobby,
                    false,
                    "no active room",
                    MultiplayerRoomRestoreStatus.NoActiveRoom,
                    MultiplayerRoomRestoreErrorCode.NoAccountRoomMapping)
            };
            var controller = new MultiplayerRoomFlowController(
                session,
                new StubSnapshotProvider());

            var result = await controller.RestoreAsync(NewSpec(), fallbackPlayerId: 9u);

            Assert.False(result.HasActiveRoom);
            Assert.False(result.CanRetry);
            Assert.Equal(MultiplayerRoomFlowState.Idle, controller.CurrentState);
            Assert.Equal(string.Empty, controller.LastError);
        }

        [Fact]
        public async Task RestoreAsync_Timeout_ExposesRetryableFailure()
        {
            var session = new StubSession
            {
                RestoreResult = new MultiplayerRoomRestoreResult(
                    string.Empty,
                    0UL,
                    9u,
                    MultiplayerRoomPhase.Closed,
                    MultiplayerRoomRestoreNextStep.None,
                    MultiplayerRoomEntryKind.TeamLobby,
                    false,
                    "restore timed out",
                    MultiplayerRoomRestoreStatus.Timeout,
                    MultiplayerRoomRestoreErrorCode.Timeout)
            };
            var controller = new MultiplayerRoomFlowController(
                session,
                new StubSnapshotProvider());

            var result = await controller.RestoreAsync(NewSpec(), fallbackPlayerId: 9u);

            Assert.True(result.CanRetry);
            Assert.Equal(MultiplayerRoomFlowState.Failed, controller.CurrentState);
            Assert.Equal("restore timed out", controller.LastError);
        }

        [Fact]
        public async Task PickHeroAsync_InLobby_CallsSession()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());

            var loadout = new MultiplayerLoadoutSpec(
                heroId: 2002, teamId: 1, spawnPointId: 0, level: 1,
                attributeTemplateId: 0, basicAttackSkillId: 0, skillIds: new[] { 1, 2 });

            await controller.PickHeroAsync(loadout);

            Assert.True(session.ConfigureLoadoutCalled);
            Assert.Equal(2002, session.LastLoadout.HeroId);
            Assert.Equal(StubSession.CreatedRoomId, session.LastLoadoutRoomId);
        }

        [Fact]
        public async Task SetReadyAsync_InLobby_CallsSession()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());

            await controller.SetReadyAsync(true);

            Assert.True(session.SetReadyCalled);
            Assert.True(session.LastReadyValue);
        }

        [Fact]
        public async Task BeginLoadingAsync_InLobby_Transitions_To_LoadingAssets()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());
            provider.Emit(ReadyLobbySnapshot());

            await controller.BeginLoadingAsync();

            Assert.Equal(MultiplayerRoomFlowState.LoadingAssets, controller.CurrentState);
            Assert.True(session.BeginLoadingCalled);
        }

        [Fact]
        public async Task BeginLoadingAsync_NonOwner_IsRejectedBeforeSessionCommand()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartJoinRoomAsync(NewSpec(), StubSession.CreatedRoomId);
            provider.Emit(ReadyLobbySnapshot());

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.BeginLoadingAsync());

            Assert.Contains("owner", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(session.BeginLoadingCalled);
        }

        [Fact]
        public async Task BeginLoadingAsync_RoomNotReady_IsRejectedBeforeSessionCommand()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());
            var snapshot = ReadyLobbySnapshot();
            snapshot.CanStart = false;
            provider.Emit(snapshot);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.BeginLoadingAsync());

            Assert.Contains("not ready", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(session.BeginLoadingCalled);
        }

        [Fact]
        public async Task ReportAssetsLoadedAsync_AfterLoading_Transitions_To_WaitingForBattle()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());
            provider.Emit(ReadyLobbySnapshot());
            await controller.BeginLoadingAsync();

            await controller.ReportAssetsLoadedAsync();

            Assert.Equal(MultiplayerRoomFlowState.WaitingForBattle, controller.CurrentState);
            Assert.True(session.ReportAssetsLoadedCalled);
        }

        [Fact]
        public async Task WaitForBattleStartAsync_Transitions_To_InBattle()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());
            provider.Emit(ReadyLobbySnapshot());
            await controller.BeginLoadingAsync();
            await controller.ReportAssetsLoadedAsync();

            await controller.WaitForBattleStartAsync();

            Assert.Equal(MultiplayerRoomFlowState.InBattle, controller.CurrentState);
            Assert.True(session.WaitForBattleStartCalled);
        }

        [Fact]
        public async Task CreateRoom_Failure_Transitions_To_Failed_With_LastError()
        {
            var session = new StubSession
            {
                CreateRoomException = new InvalidOperationException("boom-create")
            };
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartCreateRoomAsync(NewSpec()));

            Assert.Equal(MultiplayerRoomFlowState.Failed, controller.CurrentState);
            Assert.Contains("boom-create", controller.LastError);
        }

        [Fact]
        public async Task JoinRoom_Failure_Transitions_To_Failed_With_LastError()
        {
            var session = new StubSession
            {
                JoinRoomException = new InvalidOperationException("boom-join")
            };
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.StartJoinRoomAsync(NewSpec(), "room-1"));

            Assert.Equal(MultiplayerRoomFlowState.Failed, controller.CurrentState);
            Assert.Contains("boom-join", controller.LastError);
        }

        [Fact]
        public async Task BeginLoading_Failure_Transitions_To_Failed()
        {
            var session = new StubSession
            {
                BeginLoadingException = new InvalidOperationException("boom-load")
            };
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());
            provider.Emit(ReadyLobbySnapshot());

            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.BeginLoadingAsync());

            Assert.Equal(MultiplayerRoomFlowState.Failed, controller.CurrentState);
            Assert.Contains("boom-load", controller.LastError);
        }

        [Fact]
        public async Task StateChanged_Fires_On_Every_Transition()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            var fired = new List<MultiplayerRoomFlowState>();
            controller.StateChanged += s => fired.Add(s);

            await controller.StartCreateRoomAsync(NewSpec());
            provider.Emit(ReadyLobbySnapshot());
            await controller.BeginLoadingAsync();

            // 依次触发：LoggingIn, CreatingRoom, InLobby, LoadingAssets
            Assert.Equal(4, fired.Count);
            Assert.Equal(MultiplayerRoomFlowState.LoggingIn, fired[0]);
            Assert.Equal(MultiplayerRoomFlowState.CreatingRoom, fired[1]);
            Assert.Equal(MultiplayerRoomFlowState.InLobby, fired[2]);
            Assert.Equal(MultiplayerRoomFlowState.LoadingAssets, fired[3]);
        }

        [Fact]
        public async Task PickHero_Outside_InLobby_Throws_InvalidOperation()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.PickHeroAsync(default));
        }

        [Fact]
        public async Task SnapshotChanged_In_ActiveFlow_Syncs_State()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());

            // 服务端推送 Loading 阶段快照，控制器应同步到 LoadingAssets。
            provider.Emit(new MultiplayerRoomSnapshot
            {
                RoomId = StubSession.CreatedRoomId,
                Phase = MultiplayerRoomPhase.Loading
            });

            Assert.Equal(MultiplayerRoomFlowState.LoadingAssets, controller.CurrentState);
        }

        [Fact]
        public async Task LoadingTimeoutSnapshot_ReturnsToLobbyReleasesAssetsAndExposesDiagnostic()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var loader = new StubAssetLoader(blockFirstLoad: true);
            var controller = new MultiplayerRoomFlowController(session, provider, loader);
            await controller.StartCreateRoomAsync(NewSpec());

            provider.Emit(LoadingSnapshot(7));
            await loader.FirstLoadStarted.Task;
            provider.Emit(new MultiplayerRoomSnapshot
            {
                RoomId = StubSession.CreatedRoomId,
                Phase = MultiplayerRoomPhase.Lobby,
                PhaseReason = "LoadingTimeout",
                LaunchGeneration = 7
            });

            Assert.Equal(MultiplayerRoomFlowState.InLobby, controller.CurrentState);
            Assert.Equal(
                "Room loading timed out before all players finished loading.",
                controller.LastError);
            Assert.True(loader.ReleaseCalls > 0);
        }

        [Fact]
        public void BattleEntryGate_RequiresCompleteAuthoritativeIdentityAndDeduplicatesGeneration()
        {
            var gate = new MultiplayerBattleEntryGate();
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-1",
                NumericRoomId = 7UL,
                Phase = MultiplayerRoomPhase.InBattle,
                BattleId = "battle-1",
                WorldId = 42UL,
                LaunchGeneration = 3,
                // 进战斗门要求服务端已声明同步能力。
                SyncCapabilities = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
                    new WireNetworkSyncCapabilities
                    {
                        MetadataVersion = RoomGatewayNetworkSyncCapabilitiesConverter.CurrentMetadataVersion,
                        ProfileName = "test-profile",
                        MinimumSchemaVersion = 1,
                        MaximumSchemaVersion = 1
                    })
            };

            Assert.False(gate.TryAccept(MultiplayerRoomFlowState.WaitingForBattle, snapshot));
            snapshot.BattleId = string.Empty;
            Assert.False(gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot));

            snapshot.BattleId = "battle-1";
            Assert.True(gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot));
            Assert.False(gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot));

            snapshot.LaunchGeneration = 4;
            Assert.True(gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot));
        }

        [Fact]
        public async Task LoadingSnapshot_AutomaticallyLoadsReportsAndWaitsForBattle()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var loader = new StubAssetLoader();
            var controller = new MultiplayerRoomFlowController(session, provider, loader);
            await controller.StartCreateRoomAsync(NewSpec());

            provider.Emit(LoadingSnapshot(7));
            await controller.ResumePendingStageAsync();

            Assert.Equal(new long[] { 7 }, loader.Generations);
            Assert.True(session.ReportAssetsLoadedCalled);
            Assert.NotEmpty(session.LoadingProgressReports);
            Assert.Equal(100, session.LoadingProgressReports[session.LoadingProgressReports.Count - 1]);
            Assert.Equal(100, controller.LocalLoadingProgress);
            Assert.True(session.WaitForBattleStartCalled);
            Assert.Equal(MultiplayerRoomFlowState.InBattle, controller.CurrentState);
        }

        [Fact]
        public async Task LoadingGenerationChange_CancelsOldLoadAndOnlyContinuesLatest()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var loader = new StubAssetLoader(blockFirstLoad: true);
            var controller = new MultiplayerRoomFlowController(session, provider, loader);
            await controller.StartCreateRoomAsync(NewSpec());

            provider.Emit(LoadingSnapshot(7));
            await loader.FirstLoadStarted.Task;
            provider.Emit(LoadingSnapshot(8));
            await controller.ResumePendingStageAsync();

            Assert.Equal(new long[] { 7, 8 }, loader.Generations);
            Assert.Equal(1, loader.CancelledLoads);
            Assert.True(session.ReportAssetsLoadedCalled);
            Assert.True(session.WaitForBattleStartCalled);
        }

        [Fact]
        public async Task Cancel_Resets_To_Idle()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);
            await controller.StartCreateRoomAsync(NewSpec());

            controller.Cancel();

            Assert.Equal(MultiplayerRoomFlowState.Idle, controller.CurrentState);
            Assert.Equal(string.Empty, controller.CurrentRoomId);
        }

        [Fact]
        public void RestoreFromSnapshot_NullSnapshot_Goes_Idle()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            controller.RestoreFromSnapshot();

            Assert.Equal(MultiplayerRoomFlowState.Idle, controller.CurrentState);
        }

        [Fact]
        public void RestoreFromSnapshot_LobbySnapshot_Goes_InLobby()
        {
            var session = new StubSession();
            var provider = new StubSnapshotProvider();
            var controller = new MultiplayerRoomFlowController(session, provider);

            controller.RestoreFromSnapshot();
            provider.Emit(new MultiplayerRoomSnapshot
            {
                RoomId = "restored-room",
                Phase = MultiplayerRoomPhase.Lobby
            });
            controller.RestoreFromSnapshot();

            Assert.Equal(MultiplayerRoomFlowState.InLobby, controller.CurrentState);
            Assert.Equal("restored-room", controller.CurrentRoomId);
        }

        private static MultiplayerRoomLaunchSpec NewSpec()
        {
            return new MultiplayerRoomLaunchSpec
            {
                SessionToken = "token",
                Region = "r",
                ServerId = "s",
                RoomType = "default",
                RoomTitle = "T",
                MaxPlayers = 2
            };
        }

        private static MultiplayerRoomRestoreResult Restored(
            MultiplayerRoomRestoreNextStep nextStep)
        {
            return new MultiplayerRoomRestoreResult(
                "room-restored",
                42UL,
                9u,
                nextStep == MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading
                    ? MultiplayerRoomPhase.Lobby
                    : nextStep == MultiplayerRoomRestoreNextStep.ReportAssetsLoaded
                        ? MultiplayerRoomPhase.Loading
                        : nextStep == MultiplayerRoomRestoreNextStep.WaitForBattleStart
                            ? MultiplayerRoomPhase.Starting
                            : MultiplayerRoomPhase.InBattle,
                nextStep,
                MultiplayerRoomEntryKind.Reconnect,
                true,
                string.Empty,
                MultiplayerRoomRestoreStatus.Restored,
                MultiplayerRoomRestoreErrorCode.None);
        }

        private static MultiplayerRoomSnapshot LoadingSnapshot(long generation)
        {
            return new MultiplayerRoomSnapshot
            {
                RoomId = StubSession.CreatedRoomId,
                Phase = MultiplayerRoomPhase.Loading,
                LaunchGeneration = generation,
                LaunchManifestVersion = 3,
                LaunchManifestHash = "manifest",
                LoadingDeadlineUnixMs = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()
            };
        }

        private static MultiplayerRoomSnapshot ReadyLobbySnapshot()
        {
            return new MultiplayerRoomSnapshot
            {
                RoomId = StubSession.CreatedRoomId,
                OwnerAccountId = "owner",
                Phase = MultiplayerRoomPhase.Lobby,
                CanStart = true
            };
        }

        private sealed class StubAssetLoader : IMultiplayerBattleAssetLoader
        {
            private readonly bool _blockFirstLoad;

            public StubAssetLoader(bool blockFirstLoad = false)
            {
                _blockFirstLoad = blockFirstLoad;
            }

            public List<long> Generations { get; } = new List<long>();
            public int CancelledLoads { get; private set; }
            public int ReleaseCalls { get; private set; }
            public TaskCompletionSource<bool> FirstLoadStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task LoadAsync(
                MultiplayerRoomSnapshot snapshot,
                IProgress<MultiplayerAssetLoadProgress> progress,
                CancellationToken cancellationToken)
            {
                Generations.Add(snapshot.LaunchGeneration);
                progress?.Report(new MultiplayerAssetLoadProgress(50, 1, 2, "test-asset"));
                FirstLoadStarted.TrySetResult(true);
                if (!_blockFirstLoad || Generations.Count > 1)
                {
                    progress?.Report(new MultiplayerAssetLoadProgress(100, 2, 2, "test-complete"));
                    return;
                }

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancelledLoads++;
                    throw;
                }
            }

            public void Release()
            {
                ReleaseCalls++;
            }
        }

        private sealed class StubSession : IMultiplayerRoomSession
        {
            public const string CreatedRoomId = "room-created";

            public bool ConfigureLoadoutCalled;
            public MultiplayerLoadoutSpec LastLoadout;
            public string LastLoadoutRoomId;

            public bool SetReadyCalled;
            public bool LastReadyValue;

            public bool BeginLoadingCalled;
            public bool ReportAssetsLoadedCalled;
            public readonly List<int> LoadingProgressReports = new List<int>();
            public bool WaitForBattleStartCalled;
            public int RestoreCalls;
            public MultiplayerRoomRestoreResult RestoreResult = Restored(
                MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading);

            public Exception CreateRoomException;
            public Exception JoinRoomException;
            public Exception BeginLoadingException;

            public Task<MultiplayerRoomRestoreResult> RestoreAsync(
                MultiplayerRoomLaunchSpec spec,
                uint fallbackPlayerId,
                CancellationToken cancellationToken)
            {
                RestoreCalls++;
                return Task.FromResult(RestoreResult);
            }

            public Task<string> CreateRoomAsync(MultiplayerRoomLaunchSpec spec, CancellationToken cancellationToken)
            {
                if (CreateRoomException != null) throw CreateRoomException;
                return Task.FromResult(CreatedRoomId);
            }

            public Task<MultiplayerRoomJoinResult> JoinRoomAsync(
                MultiplayerRoomLaunchSpec spec,
                string roomId,
                CancellationToken cancellationToken)
            {
                if (JoinRoomException != null) throw JoinRoomException;
                return Task.FromResult(new MultiplayerRoomJoinResult(roomId, 42UL, 9u));
            }

            public Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task ConfigureLoadoutAsync(string roomId, MultiplayerLoadoutSpec loadout, CancellationToken cancellationToken)
            {
                ConfigureLoadoutCalled = true;
                LastLoadout = loadout;
                LastLoadoutRoomId = roomId;
                return Task.CompletedTask;
            }

            public Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken)
            {
                SetReadyCalled = true;
                LastReadyValue = ready;
                return Task.CompletedTask;
            }

            public Task BeginLoadingAsync(string roomId, CancellationToken cancellationToken)
            {
                if (BeginLoadingException != null) throw BeginLoadingException;
                BeginLoadingCalled = true;
                return Task.CompletedTask;
            }

            public Task ReportAssetsLoadedAsync(string roomId, CancellationToken cancellationToken)
            {
                ReportAssetsLoadedCalled = true;
                return Task.CompletedTask;
            }

            public Task ReportLoadingProgressAsync(string roomId, int progress, CancellationToken cancellationToken)
            {
                LoadingProgressReports.Add(progress);
                return Task.CompletedTask;
            }

            public Task CancelLoadingAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken)
            {
                WaitForBattleStartCalled = true;
                return Task.CompletedTask;
            }
        }

        private sealed class StubSnapshotProvider : IRoomSnapshotProvider
        {
            private MultiplayerRoomSnapshot _current;

            public MultiplayerRoomSnapshot Current => _current;

            public event Action<MultiplayerRoomSnapshot> OnSnapshotChanged;

            public void Emit(MultiplayerRoomSnapshot snapshot)
            {
                _current = snapshot;
                OnSnapshotChanged?.Invoke(snapshot);
            }
        }
    }
}
