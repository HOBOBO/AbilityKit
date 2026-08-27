#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.View.Hosting;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    [CreateAssetMenu(
        fileName = "ShooterMultiplayerProfile",
        menuName = "AbilityKit/Shooter/Formal Multiplayer Profile")]
    public sealed class ShooterMultiplayerProfileSO : ScriptableObject
    {
        [Header("Room")]
        [SerializeField] private string roomTitle = "Shooter Room";
        [SerializeField] private int maxPlayers = ShooterGameplay.DefaultMaxPlayers;
        [SerializeField] private int roomListLimit = 10;

        [Header("Flow")]
        [SerializeField] private bool autoReady = true;
        [SerializeField] private bool autoStart = true;

        [Header("Battle Template")]
        [SerializeField] private string syncTemplateId = ShooterSyncTemplateIds.MassBattleLodAoiSampleBlock;
        [SerializeField] private string networkEnvironmentId = "ideal";
        [SerializeField] private int randomSeed = 3901;
        [SerializeField] private int playerCount = 2;
        [SerializeField] private int controlledPlayerId = 1;
        [SerializeField] private float worldScale = 1f;
        [SerializeField] private int enemyBudget = ShooterPlayModeSessionOptions.PlayModeDefaultEnemyBudget;
        [SerializeField] private bool enableAuthorityComparison;

        [Header("Presentation")]
        [SerializeField] private ShooterUnityViewRenderBackend renderBackend = ShooterUnityViewRenderBackendCatalog.DefaultBackend;
        [SerializeField] private string starterSceneName = "StarterScene";

        public string RoomTitle => string.IsNullOrWhiteSpace(roomTitle) ? "Shooter Room" : roomTitle.Trim();
        public int MaxPlayers => Math.Max(1, maxPlayers);
        public int RoomListLimit => Math.Max(1, roomListLimit);
        public bool AutoReady => autoReady;
        public bool AutoStart => autoStart;
        public string NetworkEnvironmentId => string.IsNullOrWhiteSpace(networkEnvironmentId)
            ? "ideal"
            : networkEnvironmentId.Trim();
        public ShooterUnityViewRenderBackend RenderBackend => ShooterUnityViewRenderBackendCatalog.Normalize(renderBackend);
        public string StarterSceneName => string.IsNullOrWhiteSpace(starterSceneName)
            ? "StarterScene"
            : starterSceneName.Trim();

        public ShooterPlayModeSessionOptions BuildSessionOptions()
        {
            var normalizedPlayers = Math.Max(1, playerCount);
            var normalizedControlledPlayer = Math.Min(Math.Max(1, controlledPlayerId), normalizedPlayers);
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(
                string.IsNullOrWhiteSpace(syncTemplateId)
                    ? ShooterRoomLaunchSpec.DefaultSyncTemplateId
                    : syncTemplateId.Trim());
            var templateOptions = ShooterPlayModeSessionOptions.FromTemplateForNetwork(
                template,
                NetworkEnvironmentId,
                randomSeed,
                normalizedControlledPlayer,
                Math.Max(0.01f, worldScale));

            return new ShooterPlayModeSessionOptions(
                templateOptions.SyncModel,
                templateOptions.TickRate,
                normalizedPlayers,
                templateOptions.RandomSeed,
                templateOptions.ControlledPlayerId,
                enableAuthorityComparison,
                templateOptions.LatencyMs,
                templateOptions.JitterMs,
                templateOptions.PacketLossRate,
                templateOptions.ReorderRate,
                templateOptions.BandwidthKbps,
                templateOptions.WorldScale,
                templateOptions.NetworkName,
                templateOptions.SyncTemplateId,
                ShooterPlayModeSessionOptions.CreatePlayModeScenario(Math.Max(1, enemyBudget)));
        }

        public ShooterRoomLaunchSpec BuildRoomLaunchSpec(
            ShooterPlayModeSessionOptions sessionOptions,
            string region,
            string serverId,
            string? titleOverride = null)
        {
            var defaults = ShooterRoomLaunchSpec.CreateDefault($"unity-{sessionOptions.ControlledPlayerId}");
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(sessionOptions.SyncTemplateId);
            var tags = new Dictionary<string, string>(defaults.Tags, StringComparer.Ordinal)
            {
                [ShooterRoomLaunchTagKeys.SyncTemplateId] = template.Id,
                [ShooterRoomLaunchTagKeys.SyncModel] = ((int)template.SyncModel).ToString(),
                [ShooterRoomLaunchTagKeys.NetworkEnvironmentId] = NetworkEnvironmentId,
                [ShooterRoomLaunchTagKeys.CarrierName] = template.ExpectedCarrierName,
                [ShooterRoomLaunchTagKeys.EnableAuthoritativeWorld] = template.EnableAuthoritativeWorld.ToString(),
                [ShooterRoomLaunchTagKeys.InterpolationEnabled] = template.ExpectsInterpolationDiagnostics.ToString(),
                [ShooterRoomLaunchTagKeys.InputDelayFrames] = "0",
                [ShooterRoomLaunchTagKeys.RandomSeed] = sessionOptions.RandomSeed.ToString(),
                [ShooterRoomLaunchTagKeys.DurationFrames] = sessionOptions.GameplayScenario.BattleFlow.DurationFrames.ToString(),
                [ShooterRoomLaunchTagKeys.EnemyBudget] = sessionOptions.GameplayScenario.BattleFlow.MaxActiveEnemies.ToString()
            };

            return new ShooterRoomLaunchSpec(
                region,
                serverId,
                string.IsNullOrWhiteSpace(titleOverride) ? RoomTitle : titleOverride!.Trim(),
                MaxPlayers,
                defaults.GameplayId,
                defaults.RuleSetId,
                defaults.ConfigVersion,
                defaults.ProtocolVersion,
                defaults.WorldType,
                defaults.ClientId,
                tags,
                template.Id,
                (int)template.SyncModel,
                NetworkEnvironmentId,
                template.ExpectedCarrierName,
                template.EnableAuthoritativeWorld,
                template.ExpectsInterpolationDiagnostics,
                inputDelayFrames: 0);
        }

        public ShooterRemoteStateSyncLaunchOptions BuildLaunchOptions(
            DemoMultiplayerLaunchRequest request,
            ShooterRemoteStateSyncLaunchMode launchMode,
            string roomId,
            string? roomTitleOverride = null)
        {
            if (!request.IsAuthenticated)
            {
                throw new InvalidOperationException("Shooter multiplayer launch requires an authenticated session.");
            }

            var sessionOptions = BuildSessionOptions();
            return new ShooterRemoteStateSyncLaunchOptions(
                sessionOptions,
                new ShooterClientNetworkEndpoint(request.Host, request.Port),
                request.SessionToken,
                request.Region,
                request.ServerId,
                launchMode,
                request.Timeout,
                roomId ?? string.Empty,
                BuildRoomLaunchSpec(sessionOptions, request.Region, request.ServerId, roomTitleOverride));
        }
    }
}
