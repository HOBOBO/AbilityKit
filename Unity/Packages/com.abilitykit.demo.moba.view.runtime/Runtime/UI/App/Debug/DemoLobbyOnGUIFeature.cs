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
        private string _selectionMessage;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _autoEntryAttempted = false;
            _selectionMessage = null;
            LoadAssets(ctx.Entry as GameEntry);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _loaded = false;
            _config = null;
            _autoEntryAttempted = false;
            _selectionMessage = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_autoEntryAttempted || ctx.Entry == null) return;

            var selection = ctx.Entry.Get<LobbyBattleEntrySelection>();
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
                    hasBattleConfig: true,
                    alreadyAttempted: _autoEntryAttempted,
                    enableSyncModeSelection: _config.EnableSyncModeSelection))
                return;

            _autoEntryAttempted = true;
            EnterBattle(ctx, null);
        }

        internal static bool ShouldAutoEnterLocalBattle(
            LobbyBattleEntrySelection selection,
            bool alreadyInBattle,
            bool hasBattleConfig,
            bool alreadyAttempted,
            bool enableSyncModeSelection = false)
        {
            return selection?.IsRemoteSelected != true &&
                   !alreadyInBattle &&
                   hasBattleConfig &&
                   !alreadyAttempted &&
                   !enableSyncModeSelection;
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (ctx.Entry == null || _config == null || !_config.EnableSyncModeSelection)
                return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle)
                return;

            var selection = ctx.Entry.Get<LobbyBattleEntrySelection>();
            if (selection != null && selection.IsRemoteSelected)
                return;

            DrawSyncModeSelection(in ctx);
        }

        private void EnterBattle(in GamePhaseContext ctx, BattleStartPresetSO preset)
        {
            if (ctx.Entry == null) return;

            // The legacy automatic path may use BattleStartConfig directly.
            // Keep that path valid while the explicit selector always uses a
            // named local preset.
            if (preset == null && _config != null && _config.Preset != null)
                preset = _config.Preset;

            var selection = ctx.Entry.Get<LobbyBattleEntrySelection>();
            if (IsRemotePreset(preset))
            {
                selection?.SelectRemote(_config, preset);
                return;
            }

            var reason = string.Empty;
            bool canLaunch;
            if (preset != null)
            {
                canLaunch = MobaBattleSyncModePolicy.CanSelectInLocalDemo(preset, out reason);
            }
            else
            {
                canLaunch = _config != null && MobaBattleSyncModePolicy.TryValidateCombination(
                    _config.HostMode,
                    _config.SyncMode,
                    _config.GatewaySO != null && _config.GatewaySO.UseGatewayTransport,
                    out reason);
            }
            if (!canLaunch)
            {
                _selectionMessage = string.IsNullOrEmpty(reason)
                    ? "未找到可用的本地同步预设。"
                    : reason;
                return;
            }

            selection?.Clear();
            var flow = ctx.Entry.Get<GameFlowDomain>();
            flow?.EnterBattle(new ConfiguredBattleBootstrapper(_config, preset));
        }

        private void DrawSyncModeSelection(in GamePhaseContext ctx)
        {
            GUILayout.BeginArea(
                new Rect(20f, 20f, 410f, 255f),
                "MOBA 同步模式",
                GUI.skin.window);
            GUILayout.Label("进入战斗前选择本局同步模式；进入后整局固定。");

            DrawSyncModeOption(in ctx, BattleSyncMode.Lockstep);
            DrawSyncModeOption(in ctx, BattleSyncMode.SnapshotAuthority);

            if (!string.IsNullOrEmpty(_selectionMessage))
                GUILayout.Label(_selectionMessage);
            GUILayout.EndArea();
        }

        private void DrawSyncModeOption(in GamePhaseContext ctx, BattleSyncMode syncMode)
        {
            var preset = FindLocalPreset(ctx.Entry as GameEntry, syncMode);
            var previousEnabled = GUI.enabled;
            var reason = string.Empty;
            var selectable = preset != null &&
                             MobaBattleSyncModePolicy.CanSelectInSyncModeLobby(preset, out reason);
            GUI.enabled = selectable;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MobaBattleSyncModePolicy.GetDisplayName(syncMode), GUILayout.Width(145f)))
            {
                _selectionMessage = null;
                EnterBattle(in ctx, preset);
            }
            GUILayout.Label(MobaBattleSyncModePolicy.GetProfileDescription(syncMode));
            GUILayout.EndHorizontal();

            GUI.enabled = previousEnabled;
            if (!selectable)
            {
                GUILayout.Label(preset == null
                    ? "No BattleStartPreset is configured for this sync mode."
                    : reason);
            }
        }

        private static BattleStartPresetSO FindLocalPreset(GameEntry entry, BattleSyncMode syncMode)
        {
            if (entry == null) return null;

            var presets = entry.BattleStartPresets;
            if (presets != null)
            {
                for (var i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    if (preset != null && preset.HostMode == BattleHostMode.Local &&
                        preset.SyncMode == syncMode)
                        return preset;
                }
            }

            var config = entry.BattleStartConfig;
            if (config != null && config.Preset != null &&
                config.Preset.HostMode == BattleHostMode.Local &&
                config.Preset.SyncMode == syncMode)
                return config.Preset;

            // Route StateSync through a configured GatewayRemote preset. Local
            // StateSync remains disabled until a local authority transport exists.
            if (presets != null)
            {
                for (var i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    if (preset != null && preset.HostMode == BattleHostMode.GatewayRemote &&
                        preset.SyncMode == syncMode)
                        return preset;
                }
            }

            return null;
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
