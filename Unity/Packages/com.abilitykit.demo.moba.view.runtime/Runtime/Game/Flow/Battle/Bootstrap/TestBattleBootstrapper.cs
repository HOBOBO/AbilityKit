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
    public sealed class TestBattleBootstrapper : IBattleBootstrapper, IBattleStartConfigProvider
    {
        private BattleStartConfig _config;

        public BattleStartConfig Config => _config;

        public BattleStartPlan Build()
        {
            var cfg = LoadConfig();
            _config = cfg;

            var req = cfg.BuildEnterMobaGameReq();
            var payload = EnterMobaGameCodec.SerializeReq(req);

            var options = cfg.BuildPlanOptions(req, payload, MobaWorldBootstrapModule.InitOpCode);
            return new BattleStartPlan(options);
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
