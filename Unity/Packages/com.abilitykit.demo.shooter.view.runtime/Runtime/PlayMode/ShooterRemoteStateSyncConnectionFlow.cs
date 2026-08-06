#nullable enable

using System;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public readonly struct ShooterRemoteStateSyncConnectionResult
    {
        public ShooterRemoteStateSyncConnectionResult(
            ShooterClientNetworkLaunchResult launch,
            ShooterRemoteStateSyncLaunchMode requestedMode,
            ShooterRoomGatewayEntryKind entryKind,
            bool usedFallbackCreate,
            Exception? restoreFailure)
        {
            Launch = launch;
            RequestedMode = requestedMode;
            EntryKind = entryKind;
            UsedFallbackCreate = usedFallbackCreate;
            RestoreFailure = restoreFailure;
        }

        public ShooterClientNetworkLaunchResult Launch { get; }
        public ShooterRemoteStateSyncLaunchMode RequestedMode { get; }
        public ShooterRoomGatewayEntryKind EntryKind { get; }
        public bool UsedFallbackCreate { get; }
        public Exception? RestoreFailure { get; }
        public bool RequiresInitialFullStateSync => EntryKind == ShooterRoomGatewayEntryKind.LateJoin
            || EntryKind == ShooterRoomGatewayEntryKind.Reconnect;
    }

    public sealed class ShooterRemoteStateSyncConnectionFlow
    {
        public async Task<ShooterRemoteStateSyncConnectionResult> ConnectAsync(
            ShooterClientNetworkLauncher launcher,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterStartGamePayload startGame,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId)
        {
            if (launcher == null)
            {
                throw new ArgumentNullException(nameof(launcher));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (launchOptions.LaunchMode == ShooterRemoteStateSyncLaunchMode.JoinRoom)
            {
                if (string.IsNullOrWhiteSpace(launchOptions.RoomId))
                {
                    throw new InvalidOperationException("Remote state-sync join mode requires a room id.");
                }

                var joined = await JoinReadyStartAndSubscribeAsync(
                    launcher,
                    runtime,
                    presentationSession,
                    launchOptions,
                    startGame,
                    launchSpec,
                    playerId).ConfigureAwait(false);
                return new ShooterRemoteStateSyncConnectionResult(
                    joined,
                    launchOptions.LaunchMode,
                    joined.Flow.EntryKind,
                    false,
                    null);
            }

            if (launchOptions.LaunchMode == ShooterRemoteStateSyncLaunchMode.CreateNew)
            {
                var created = await CreateReadyStartAndSubscribeAsync(
                    launcher,
                    runtime,
                    presentationSession,
                    launchOptions,
                    startGame,
                    launchSpec,
                    playerId).ConfigureAwait(false);
                return new ShooterRemoteStateSyncConnectionResult(
                    created,
                    launchOptions.LaunchMode,
                    created.Flow.EntryKind,
                    false,
                    null);
            }

            var restoreFirst = await RoomGatewayRestoreFirstConnectionPolicy.ConnectAsync(
                () => RestoreRoomAsLaunchAsync(
                    launcher,
                    runtime,
                    presentationSession,
                    launchOptions,
                    startGame,
                    launchSpec,
                    playerId),
                () => CreateReadyStartAndSubscribeAsync(
                    launcher,
                    runtime,
                    presentationSession,
                    launchOptions,
                    startGame,
                    launchSpec,
                    playerId),
                launchOptions.LaunchMode == ShooterRemoteStateSyncLaunchMode.RestoreFirst).ConfigureAwait(false);

            return new ShooterRemoteStateSyncConnectionResult(
                restoreFirst.Result,
                launchOptions.LaunchMode,
                restoreFirst.Result.Flow.EntryKind,
                restoreFirst.UsedFallbackCreate,
                restoreFirst.RestoreFailure);
        }

        private static async Task<ShooterClientNetworkLaunchResult> RestoreRoomAsLaunchAsync(
            ShooterClientNetworkLauncher launcher,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterStartGamePayload startGame,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId)
        {
            return await RestoreRoomAsync(
                launcher,
                runtime,
                presentationSession,
                launchOptions,
                startGame,
                launchSpec,
                playerId).ConfigureAwait(false);
        }

        private static Task<ShooterClientNetworkRestoreResult> RestoreRoomAsync(
            ShooterClientNetworkLauncher launcher,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterStartGamePayload startGame,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId)
        {
            return launcher.RestoreRoomAsync(
                launchOptions.Endpoint,
                runtime,
                presentationSession,
                startGame,
                launchOptions.SessionToken,
                launchOptions.Region,
                launchOptions.ServerId,
                launchSpec,
                playerId,
                launchOptions.CreateClientSyncAssemblyOptions(),
                launchOptions.SessionOptions.TickRate,
                launchOptions.Timeout);
        }

        private static Task<ShooterClientNetworkLaunchResult> JoinReadyStartAndSubscribeAsync(
            ShooterClientNetworkLauncher launcher,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterStartGamePayload startGame,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId)
        {
            return launcher.JoinReadyStartAndSubscribeAsync(
                launchOptions.Endpoint,
                runtime,
                presentationSession,
                startGame,
                launchOptions.SessionToken,
                launchOptions.RoomId,
                launchSpec,
                playerId,
                launchOptions.CreateClientSyncAssemblyOptions(),
                launchOptions.SessionOptions.TickRate,
                launchOptions.Timeout);
        }

        private static Task<ShooterClientNetworkLaunchResult> CreateReadyStartAndSubscribeAsync(
            ShooterClientNetworkLauncher launcher,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            ShooterRemoteStateSyncLaunchOptions launchOptions,
            ShooterStartGamePayload startGame,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId)
        {
            return launcher.CreateReadyStartAndSubscribeAsync(
                launchOptions.Endpoint,
                runtime,
                presentationSession,
                startGame,
                launchOptions.SessionToken,
                launchSpec,
                playerId,
                launchOptions.CreateClientSyncAssemblyOptions(),
                launchOptions.SessionOptions.TickRate,
                launchOptions.Timeout);
        }
    }
}
