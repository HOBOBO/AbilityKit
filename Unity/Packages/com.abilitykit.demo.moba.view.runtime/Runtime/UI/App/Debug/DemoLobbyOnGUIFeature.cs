using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AbilityKit.Game.Flow
{
    public sealed class DemoLobbyOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleStartConfig _config;
        private bool _loaded;
        private bool _autoEntryAttempted;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _autoEntryAttempted = false;
            LoadAssets(ctx.Entry as GameEntry);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _loaded = false;
            _config = null;
            _autoEntryAttempted = false;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_autoEntryAttempted || ctx.Entry == null) return;

            var selection = ctx.Entry.Get<LobbyBattleEntrySelection>();
            // A remote launch is owned by FormalLobbyFeature and must keep its
            // room/ready/start flow. Local mode can enter through the same path
            // used by the old Start Default Battle button.
            if (!_loaded)
            {
                LoadAssets(ctx.Entry as GameEntry);
            }
            if (_config == null) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            var alreadyInBattle = sink != null && sink.CurrentRootPhase == MobaRootState.Battle;
            if (!ShouldAutoEnterLocalBattle(
                    selection,
                    alreadyInBattle,
                    _config != null,
                    _autoEntryAttempted))
                return;

            _autoEntryAttempted = true;
            EnterBattle(ctx, null);
        }

        internal static bool ShouldAutoEnterLocalBattle(
            LobbyBattleEntrySelection selection,
            bool alreadyInBattle,
            bool hasBattleConfig,
            bool alreadyAttempted)
        {
            return selection?.IsRemoteSelected != true &&
                   !alreadyInBattle &&
                   hasBattleConfig &&
                   !alreadyAttempted;
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            // Local mode enters automatically in Tick; multiplayer has its
            // dedicated lobby UI.
        }

        private void EnterBattle(in GamePhaseContext ctx, BattleStartPresetSO preset)
        {
            if (ctx.Entry == null) return;

            var selection = ctx.Entry.Get<LobbyBattleEntrySelection>();
            if (IsRemotePreset(preset))
            {
                selection?.SelectRemote(_config, preset);
                return;
            }

            selection?.Clear();
            var flow = ctx.Entry.Get<GameFlowDomain>();
            flow?.EnterBattle(new ConfiguredBattleBootstrapper(_config, preset));
        }

        internal static bool IsRemotePreset(BattleStartPresetSO preset)
        {
            return preset != null &&
                   preset.HostMode == BattleHostMode.GatewayRemote;
        }

        private void LoadAssets(GameEntry entry)
        {
            _loaded = true;
            _config = entry != null ? entry.BattleStartConfig : null;

#if UNITY_EDITOR
            if (_config == null)
            {
                _config = LoadFirstAsset<BattleStartConfig>();
            }
#endif
        }

#if UNITY_EDITOR
        private static T LoadFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }

            return null;
        }

#endif
    }
}
