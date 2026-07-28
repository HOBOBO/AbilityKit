using System.Net.Sockets;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Orleans;

internal abstract class ShooterSmokeScenarioBase
{
    internal const string DefaultTcpGatewayHost = "127.0.0.1";

    internal static async Task WaitForTcpAsync(string host, int port, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, timeoutCts.Token);
                return;
            }
            catch when (!timeoutCts.IsCancellationRequested)
            {
                await Task.Delay(50, timeoutCts.Token);
            }
        }

        throw new TimeoutException($"TCP Gateway did not listen on {host}:{port} in time.");
    }

    internal static async Task<ShooterSmokeLogin> LoginGuestAsync(AbilityKit.Network.Abstractions.IConnection connection)
    {
        using var requestClient = new RequestClient(connection);
        var request = new WireRoomGuestLoginReq
        {
            GuestId = $"shooter-smoke-{Guid.NewGuid():N}"
        };
        var payload = WireRoomGatewayBinary.Serialize(in request);
        var responsePayload = await requestClient.SendRequestAsync(RoomGatewayOpCodes.GuestLogin, payload, TimeSpan.FromSeconds(10));
        var response = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(responsePayload);
        if (!response.Success || string.IsNullOrWhiteSpace(response.SessionToken) || string.IsNullOrWhiteSpace(response.AccountId))
        {
            throw new InvalidOperationException($"Shooter smoke guest login failed: {response.Message}");
        }

        return new ShooterSmokeLogin(response.AccountId, response.SessionToken);
    }

    internal static async Task<ShooterSmokeLogin> LoginAccountAsync(
        AbilityKit.Network.Abstractions.IConnection connection,
        string accountId,
        bool kickExisting = true,
        int expireSeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("accountId is required.", nameof(accountId));

        using var requestClient = new RequestClient(connection);
        var request = new WireRoomAccountLoginReq
        {
            AccountId = accountId,
            ExpireSeconds = expireSeconds,
            KickExisting = kickExisting
        };
        var payload = WireRoomGatewayBinary.Serialize(in request);
        var responsePayload = await requestClient.SendRequestAsync(RoomGatewayOpCodes.AccountLogin, payload, TimeSpan.FromSeconds(10));
        var response = WireRoomGatewayBinary.Deserialize<WireRoomAccountLoginRes>(responsePayload);
        if (!response.Success || string.IsNullOrWhiteSpace(response.SessionToken) || string.IsNullOrWhiteSpace(response.AccountId))
        {
            throw new InvalidOperationException($"Shooter smoke account login failed: {response.Message}");
        }

        if (!string.Equals(response.AccountId, accountId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Shooter smoke account login returned mismatched account. Expected={accountId}, Actual={response.AccountId}");
        }

        return new ShooterSmokeLogin(response.AccountId, response.SessionToken);
    }

    internal static ShooterSmokePresentationContext CreatePresentationContext()
    {
        var worldSession = ShooterBattleWorldSession.Create();
        try
        {
            var presentation = new ShooterPresentationFacade();
            var recorder = new RecordingProjectedViewSink();
            var projectedSink = new ShooterProjectedSnapshotViewSink(recorder);
            var session = ShooterPresentationSessionContext.CreateFromFacade(presentation, projectedSink);
            if (session.View is null)
            {
                if (session.Binder is IDisposable disposableBinder)
                {
                    disposableBinder.Dispose();
                }

                throw new InvalidOperationException("Shooter presentation session did not create a view.");
            }

            session.View.InterpolationEnabled = false;
            return new ShooterSmokePresentationContext(worldSession, presentation, recorder, session);
        }
        catch
        {
            worldSession.Dispose();
            throw;
        }
    }

    internal static ShooterViewProjectionApplyResult ValidateProjectedPresentation(
        RecordingProjectedViewSink recorder,
        int expectedPlayerCount,
        string label,
        bool requireFullSync,
        bool exactPlayerCount = true)
    {
        if (recorder.ApplyCount <= 0)
        {
            throw new InvalidOperationException($"Shooter {label} did not project any snapshot view batch.");
        }

        var result = requireFullSync ? recorder.LastFullSyncApplyResult : recorder.LastApplyResult;
        if (requireFullSync && recorder.FullSyncApplyCount <= 0)
        {
            throw new InvalidOperationException($"Shooter {label} did not project a full snapshot view batch.");
        }

        if (result.FinalEntityCount <= 0)
        {
            throw new InvalidOperationException($"Shooter {label} projection has no entities after snapshot apply.");
        }

        if (exactPlayerCount)
        {
            if (result.FinalPlayerCount != expectedPlayerCount)
            {
                throw new InvalidOperationException($"Shooter {label} projection player count mismatch. Expected={expectedPlayerCount}, Actual={result.FinalPlayerCount}");
            }
        }
        else if (result.FinalPlayerCount < expectedPlayerCount)
        {
            throw new InvalidOperationException($"Shooter {label} projection player count below minimum. Minimum={expectedPlayerCount}, Actual={result.FinalPlayerCount}");
        }

        return result;
    }

    internal static void ValidateSmokeResult(ShooterSmokeResult result)
    {
        if (string.IsNullOrWhiteSpace(result.RoomId))
        {
            throw new InvalidOperationException("Shooter smoke result returned empty room id.");
        }

        if (string.IsNullOrWhiteSpace(result.BattleId))
        {
            throw new InvalidOperationException("Shooter smoke result returned empty battle id.");
        }

        if (result.WorldId == 0)
        {
            throw new InvalidOperationException("Shooter smoke result returned zero world id.");
        }

        if (result.InputCount < 3)
        {
            throw new InvalidOperationException($"Shooter smoke submitted too few inputs. Count={result.InputCount}");
        }

        if (result.LastAcceptedFrame < result.LastRequestedFrame)
        {
            throw new InvalidOperationException($"Shooter smoke accepted frame regressed. Requested={result.LastRequestedFrame}, Accepted={result.LastAcceptedFrame}");
        }

        if (result.LastCurrentFrame < 0)
        {
            throw new InvalidOperationException($"Shooter smoke returned invalid current frame. Current={result.LastCurrentFrame}");
        }

        if (string.IsNullOrWhiteSpace(result.LastInputStatus))
        {
            throw new InvalidOperationException("Shooter smoke returned empty input status.");
        }

        if (result.LastServerTicks <= 0)
        {
            throw new InvalidOperationException("Shooter smoke returned invalid input server ticks.");
        }

        if (result.Frame <= 0)
        {
            throw new InvalidOperationException($"Shooter smoke client frame did not advance. Frame={result.Frame}");
        }

        if (result.ActorCount <= 0)
        {
            throw new InvalidOperationException("Shooter smoke presentation returned no active player actors.");
        }

        if (result.StateHash == 0)
        {
            throw new InvalidOperationException("Shooter smoke runtime returned zero state hash.");
        }

        if (result.SnapshotApplyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot)
        {
            throw new InvalidOperationException($"Shooter smoke did not apply packed snapshot push. Result={result.SnapshotApplyResult}");
        }

        if (result.SnapshotPayloadOpCode != ShooterOpCodes.Snapshot.PackedState)
        {
            throw new InvalidOperationException($"Shooter smoke snapshot payload opCode mismatch. Actual={result.SnapshotPayloadOpCode}");
        }

        if (result.SnapshotServerTicks <= 0 || result.SnapshotPackedServerTick <= 0)
        {
            throw new InvalidOperationException($"Shooter smoke snapshot returned invalid server ticks. Wire={result.SnapshotServerTicks}, Packed={result.SnapshotPackedServerTick}");
        }

        if (result.SnapshotFrame != result.SnapshotPackedFrame)
        {
            throw new InvalidOperationException($"Shooter smoke snapshot wire/packed frame mismatch. Wire={result.SnapshotFrame}, Packed={result.SnapshotPackedFrame}");
        }

        if (result.Frame < result.SnapshotPackedFrame)
        {
            throw new InvalidOperationException($"Shooter smoke client frame regressed behind snapshot. Snapshot={result.SnapshotPackedFrame}, Client={result.Frame}");
        }

        if (result.SnapshotStateHash == 0)
        {
            throw new InvalidOperationException("Shooter smoke packed snapshot returned zero state hash.");
        }

        if (result.SnapshotEntityCount <= 0)
        {
            throw new InvalidOperationException("Shooter smoke packed snapshot returned no entities.");
        }

        if (result.StaleSnapshotResult != ShooterSnapshotApplyResult.IgnoredStaleSnapshot)
        {
            throw new InvalidOperationException($"Shooter smoke stale snapshot was not ignored. Result={result.StaleSnapshotResult}");
        }

        ValidateProjectionResult(
            result.ProjectionApplyCount,
            result.ProjectionFullSyncApplyCount,
            result.ProjectionFinalEntityCount,
            result.ProjectionFinalPlayerCount,
            result.ActorCount,
            "primary client",
            requireFullSync: true);

        if (string.IsNullOrWhiteSpace(result.LateJoinAccountId))
        {
            throw new InvalidOperationException("Shooter smoke late join returned empty account id.");
        }

        if (result.LateJoinEntryKind == ShooterRoomGatewayEntryKind.TeamLobby)
        {
            throw new InvalidOperationException("Shooter smoke late join entered team lobby instead of running battle.");
        }

        ValidateProjectionResult(
            result.LateJoinProjectionApplyCount,
            result.LateJoinProjectionFullSyncApplyCount,
            result.LateJoinProjectionFinalEntityCount,
            result.LateJoinProjectionFinalPlayerCount,
            result.ActorCount,
            "late join client",
            requireFullSync: false,
            exactPlayerCount: false);

        if (result.ReconnectEntryKind != ShooterRoomGatewayEntryKind.Reconnect)
        {
            throw new InvalidOperationException($"Shooter smoke reconnect entry kind mismatch. Actual={result.ReconnectEntryKind}");
        }

        ValidateProjectionResult(
            result.ReconnectProjectionApplyCount,
            result.ReconnectProjectionFullSyncApplyCount,
            result.ReconnectProjectionFinalEntityCount,
            result.ReconnectProjectionFinalPlayerCount,
            result.ActorCount,
            "reconnect client",
            requireFullSync: false,
            exactPlayerCount: false);

        if (result.GameplayFinalFrame <= result.GameplayStartFrame)
        {
            throw new InvalidOperationException($"Shooter gameplay loop did not advance to a final frame. Start={result.GameplayStartFrame}, Final={result.GameplayFinalFrame}");
        }

        if (!result.GameplayMoved)
        {
            throw new InvalidOperationException("Shooter gameplay loop did not observe player movement.");
        }

        if (!result.GameplayFired)
        {
            throw new InvalidOperationException("Shooter gameplay loop did not submit firing input.");
        }

        if (!result.GameplayDefeatedEnemy)
        {
            throw new InvalidOperationException($"Shooter gameplay loop did not defeat any enemy. Defeated={result.GameplayDefeatedEnemies}");
        }

        if (!result.GameplayMatchFinal)
        {
            throw new InvalidOperationException($"Shooter gameplay loop did not reach final match state. State={result.GameplayFinalMatchState}, Frame={result.GameplayFinalFrame}, Time={result.GameplayRemainingTimeFrames}/{result.GameplayTimeLimitFrames}");
        }

        if (result.GameplayFinalMatchState != ShooterBattleMatchState.Victory &&
            result.GameplayFinalMatchState != ShooterBattleMatchState.Defeat &&
            result.GameplayFinalMatchState != ShooterBattleMatchState.Ended)
        {
            throw new InvalidOperationException($"Shooter gameplay loop reached unexpected final match state. State={result.GameplayFinalMatchState}");
        }

        if (result.GameplayTimeLimitFrames <= 0)
        {
            throw new InvalidOperationException("Shooter gameplay loop did not expose a time-limited battle flow.");
        }

        if (result.GameplayMatchCompletedFrame <= 0)
        {
            throw new InvalidOperationException($"Shooter gameplay loop returned invalid completion frame. CompletedFrame={result.GameplayMatchCompletedFrame}");
        }
    }

    internal static void ValidateProjectionResult(
        int applyCount,
        int fullSyncApplyCount,
        int finalEntityCount,
        int finalPlayerCount,
        int expectedPlayerCount,
        string label,
        bool requireFullSync,
        bool exactPlayerCount = true)
    {
        if (applyCount <= 0)
        {
            throw new InvalidOperationException($"Shooter smoke {label} did not apply any projection batch.");
        }

        if (requireFullSync && fullSyncApplyCount <= 0)
        {
            throw new InvalidOperationException($"Shooter smoke {label} did not apply a full projection batch.");
        }

        if (finalEntityCount <= 0)
        {
            throw new InvalidOperationException($"Shooter smoke {label} projection returned no entities.");
        }

        if (exactPlayerCount)
        {
            if (finalPlayerCount != expectedPlayerCount)
            {
                throw new InvalidOperationException($"Shooter smoke {label} projection player count mismatch. Expected={expectedPlayerCount}, Actual={finalPlayerCount}");
            }
        }
        else if (finalPlayerCount < expectedPlayerCount)
        {
            throw new InvalidOperationException($"Shooter smoke {label} projection player count below minimum. Minimum={expectedPlayerCount}, Actual={finalPlayerCount}");
        }
    }

    internal static async Task CleanupBattleAsync(IClusterClient clusterClient, ShooterSmokeResult result)
    {
        await UnsubscribeObserverAsync(clusterClient, result.AccountId, result.RoomId, result.BattleId);
        if (!string.IsNullOrWhiteSpace(result.LateJoinAccountId))
        {
            await UnsubscribeObserverAsync(clusterClient, result.LateJoinAccountId, result.RoomId, result.BattleId);
        }

        var battleGrain = clusterClient.GetGrain<IBattleLogicHostGrain>(result.BattleId);
        await battleGrain.DestroyAsync();
    }

    private static async Task UnsubscribeObserverAsync(
        IClusterClient clusterClient,
        string accountId,
        string roomId,
        string battleId)
    {
        var observerKey = $"{accountId}:{roomId}";
        var observerGrain = clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);
        await observerGrain.UnsubscribeAsync(battleId);
    }
}

internal sealed class ShooterSmokePresentationContext : IDisposable
{
    private readonly ShooterBattleWorldSession _worldSession;
    private bool _disposed;

    public ShooterSmokePresentationContext(
        ShooterBattleWorldSession worldSession,
        ShooterPresentationFacade presentation,
        RecordingProjectedViewSink recorder,
        ShooterPresentationSessionContext session)
    {
        _worldSession = worldSession ?? throw new ArgumentNullException(nameof(worldSession));
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        Recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public ShooterBattleRuntimePort Runtime => _worldSession.Runtime;

    public ShooterPresentationFacade Presentation { get; }

    public RecordingProjectedViewSink Recorder { get; }

    public ShooterPresentationSessionContext Session { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Session.Binder is IDisposable disposableBinder)
        {
            disposableBinder.Dispose();
        }
        else
        {
            Session.Binder.Clear();
        }

        _worldSession.Dispose();
    }
}
