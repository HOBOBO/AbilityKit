using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Server;
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

            var plan = cfg.StartPlan;

            return new BattleStartPlan(
                worldId: plan != null ? plan.WorldId : "room_1",
                worldType: plan != null ? plan.WorldType : "battle",
                clientId: plan != null ? plan.ClientId : "battle_client",
                playerId: req.PlayerId.Value,
                autoConnect: plan != null && plan.AutoConnect,
                autoCreateWorld: plan != null && plan.AutoCreateWorld,
                autoJoin: plan != null && plan.AutoJoin,
                autoReady: plan != null && plan.AutoReady,
                syncMode: plan != null ? plan.SyncMode : BattleSyncMode.Lockstep,
                viewEventSourceMode: plan != null ? plan.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly,
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
                if (asset != null) return asset;
            }
#endif
            return ScriptableObject.CreateInstance<BattleStartConfig>();
        }
    }
}
