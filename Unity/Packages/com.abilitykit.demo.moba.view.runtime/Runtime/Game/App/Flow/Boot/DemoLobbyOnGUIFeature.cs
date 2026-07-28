using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AbilityKit.Game.Flow
{
    public sealed class DemoLobbyOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private readonly List<BattleStartPresetSO> _presets = new List<BattleStartPresetSO>(8);
        private BattleStartConfig _config;
        private Vector2 _scroll;
        private bool _loaded;
        private bool _show = true;

        public void OnAttach(in GamePhaseContext ctx)
        {
            LoadAssets(ctx.Entry as GameEntry);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            if (ctx.Entry == null) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink != null && sink.CurrentRootPhase == MobaRootState.Battle) return;

            if (!_loaded)
            {
                LoadAssets(ctx.Entry as GameEntry);
            }

            GUILayout.BeginArea(new Rect(10, 10, 360, 260), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("MOBA Demo Lobby");
            if (GUILayout.Button("Hide", GUILayout.Width(56)))
            {
                _show = false;
            }
            GUILayout.EndHorizontal();

            if (_config == null)
            {
                GUILayout.Label("BattleStartConfig asset not found.");
            }
            else
            {
                DrawPresetButtons(ctx);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Start Default Battle", GUILayout.Height(30)))
            {
                EnterBattle(ctx, null);
            }

            GUILayout.EndArea();
        }

        private void DrawPresetButtons(in GamePhaseContext ctx)
        {
            if (_presets.Count == 0)
            {
                GUILayout.Label("No BattleStartPreset assets found.");
                return;
            }

            GUILayout.Label("Presets");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(150));
            for (int i = 0; i < _presets.Count; i++)
            {
                var preset = _presets[i];
                if (preset == null) continue;

                var label = string.IsNullOrEmpty(preset.name) ? $"Preset {i + 1}" : preset.name;
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    EnterBattle(ctx, preset);
                }
            }
            GUILayout.EndScrollView();
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
                   preset.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote;
        }

        private void LoadAssets(GameEntry entry)
        {
            _loaded = true;
            _presets.Clear();
            _config = entry != null ? entry.BattleStartConfig : null;

            var runtimePresets = entry?.BattleStartPresets;
            if (runtimePresets != null)
            {
                for (var i = 0; i < runtimePresets.Count; i++)
                {
                    var preset = runtimePresets[i];
                    if (preset != null && !_presets.Contains(preset))
                    {
                        _presets.Add(preset);
                    }
                }
            }

#if UNITY_EDITOR
            if (_config == null)
            {
                _config = LoadFirstAsset<BattleStartConfig>();
            }

            if (_presets.Count == 0)
            {
                LoadAllAssets(_presets);
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

        private static void LoadAllAssets<T>(List<T> results) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && !results.Contains(asset))
                {
                    results.Add(asset);
                }
            }
        }
#endif
    }
}
