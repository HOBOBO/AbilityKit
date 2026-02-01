using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AbilityKit.Game.Flow
{
    public sealed class TestBattleBootstrapper : IBattleBootstrapper
    {
        public BattleStartPlan Build()
        {
            var cfg = LoadConfig();

            var req = cfg.BuildEnterMobaGameReq();
            var payload = EnterMobaGameCodec.SerializeReq(req);

            var legacy = cfg.StartPlan;

            var worldId = legacy != null ? legacy.WorldId : "room_1";
            var worldType = legacy != null ? legacy.WorldType : "battle";
            if (cfg.TryGetSelectedWorldPlan(out var world) && world != null)
            {
                if (!string.IsNullOrEmpty(world.WorldId)) worldId = world.WorldId;
                if (!string.IsNullOrEmpty(world.WorldType)) worldType = world.WorldType;
            }

            var hostMode = cfg.Profile != null && cfg.Profile.Host != null ? cfg.Profile.Host.Mode : BattleStartConfig.BattleHostMode.Local;

            var autoConnect = cfg.Profile != null && cfg.Profile.Client != null ? cfg.Profile.Client.AutoConnect : (legacy != null && legacy.AutoConnect);
            var clientId = cfg.Profile != null && cfg.Profile.Client != null && !string.IsNullOrEmpty(cfg.Profile.Client.ClientId)
                ? cfg.Profile.Client.ClientId
                : (legacy != null ? legacy.ClientId : "battle_client");

            var autoCreateWorld = cfg.Profile != null && cfg.Profile.World != null ? cfg.Profile.World.AutoCreateWorld : (legacy != null && legacy.AutoCreateWorld);
            var autoJoin = cfg.Profile != null && cfg.Profile.World != null ? cfg.Profile.World.AutoJoin : (legacy != null && legacy.AutoJoin);
            var autoReady = cfg.Profile != null && cfg.Profile.World != null ? cfg.Profile.World.AutoReady : (legacy != null && legacy.AutoReady);

            var syncMode = cfg.Profile != null ? cfg.Profile.SyncMode : (legacy != null ? legacy.SyncMode : BattleSyncMode.Lockstep);
            var viewEventSourceMode = cfg.Profile != null ? cfg.Profile.ViewEventSourceMode : (legacy != null ? legacy.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly);

            var runMode = cfg.Profile != null && cfg.Profile.RunMode != null ? cfg.Profile.RunMode.Mode : BattleStartConfig.BattleRunMode.Normal;
            if (legacy != null)
            {
                if (legacy.EnableInputReplay) runMode = BattleStartConfig.BattleRunMode.Replay;
                else if (legacy.EnableInputRecording) runMode = BattleStartConfig.BattleRunMode.Record;
            }

            var enableInputRecording = runMode == BattleStartConfig.BattleRunMode.Record;
            var enableInputReplay = runMode == BattleStartConfig.BattleRunMode.Replay;

            var recordPath = cfg.Profile != null && cfg.Profile.RunMode != null && !string.IsNullOrEmpty(cfg.Profile.RunMode.RecordOutputPath)
                ? cfg.Profile.RunMode.RecordOutputPath
                : (legacy != null ? legacy.InputRecordOutputPath : "battle_record.json");

            var replayPath = cfg.Profile != null && cfg.Profile.RunMode != null && !string.IsNullOrEmpty(cfg.Profile.RunMode.ReplayInputPath)
                ? cfg.Profile.RunMode.ReplayInputPath
                : (legacy != null ? legacy.InputReplayPath : "battle_record.json");

            return new BattleStartPlan(
                worldId: worldId,
                worldType: worldType,
                clientId: clientId,
                playerId: req.PlayerId.Value,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                hostMode: hostMode,
                useGatewayTransport: cfg.Gateway != null && cfg.Gateway.UseGatewayTransport,
                gatewayHost: cfg.Gateway != null ? cfg.Gateway.Host : "127.0.0.1",
                gatewayPort: cfg.Gateway != null ? cfg.Gateway.Port : 4000,
                numericRoomId: cfg.Gateway != null ? cfg.Gateway.NumericRoomId : 0,
                gatewaySessionToken: cfg.Gateway != null ? cfg.Gateway.SessionToken : string.Empty,
                gatewayRegion: cfg.Gateway != null ? cfg.Gateway.Region : "dev",
                gatewayServerId: cfg.Gateway != null ? cfg.Gateway.ServerId : "local",
                gatewayAutoCreateRoom: cfg.Gateway != null && cfg.Gateway.AutoCreateRoom,
                gatewayAutoJoinRoom: cfg.Gateway != null && cfg.Gateway.AutoJoinRoom,
                gatewayJoinRoomId: cfg.Gateway != null ? cfg.Gateway.JoinRoomId : string.Empty,
                gatewayCreateRoomOpCode: cfg.Gateway != null ? cfg.Gateway.CreateRoomOpCode : 110,
                gatewayJoinRoomOpCode: cfg.Gateway != null ? cfg.Gateway.JoinRoomOpCode : 111,
                autoConnect: autoConnect,
                autoCreateWorld: autoCreateWorld,
                autoJoin: autoJoin,
                autoReady: autoReady,
                syncMode: syncMode,
                viewEventSourceMode: viewEventSourceMode,
                enableInputRecording: enableInputRecording,
                inputRecordOutputPath: recordPath,
                enableInputReplay: enableInputReplay,
                inputReplayPath: replayPath,
                runMode: runMode,
                createWorldOpCode: MobaWorldBootstrapModule.InitOpCode,
                createWorldPayload: payload
            );
        }

        private static BattleStartConfig LoadConfig()
        {
#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets($"t:{nameof(BattleStartConfig)}");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<BattleStartConfig>(path);
                if (asset != null)
                {
                    Debug.Log($"[TestBattleBootstrapper] Loaded BattleStartConfig from: {path}");
                    return asset;
                }
            }

            Debug.LogWarning("[TestBattleBootstrapper] BattleStartConfig not found via AssetDatabase. Falling back to defaults (Local mode).");
#endif
            return ScriptableObject.CreateInstance<BattleStartConfig>();
        }
    }
}
