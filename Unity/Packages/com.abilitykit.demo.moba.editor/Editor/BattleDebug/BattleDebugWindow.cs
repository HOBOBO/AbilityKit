using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Game.Battle;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public sealed class BattleDebugWindow : EditorWindow
    {
        private string _filter;
        private string _jumpId;
        private Vector2 _entityScroll;
        private Vector2 _detailScroll;

        private readonly List<EcsEntityId> _visibleEntities = new List<EcsEntityId>(256);
        private int _selectedIndex = -1;
        private double _nextRefreshAt;

        private int _selectedPanelIndex;

        [MenuItem("Tools/AbilityKit/Battle/战斗调试")]
        private static void Open()
        {
            GetWindow<BattleDebugWindow>("战斗调试");
        }

        private void OnEnable()
        {
            _nextRefreshAt = EditorApplication.timeSinceStartup;
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("进入播放模式后才能使用战斗调试窗口。", MessageType.Info);
                return;
            }

            var facade = BattleDebugFacadeProvider.Current;
            if (facade == null)
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("BattleDebugFacadeProvider.Current 为空。请通过 BattleLogicSessionHost.Start() 启动 BattleLogicSession。", MessageType.Warning);
                return;
            }

            if (!facade.TryGetSession(out _))
            {
                DrawToolbar(default);
                EditorGUILayout.HelpBox("当前没有活动中的 BattleLogicSession，请先启动会话。", MessageType.Info);
                return;
            }

            var hasSelection = _selectedIndex >= 0 && _selectedIndex < _visibleEntities.Count;
            var selectedId = hasSelection ? _visibleEntities[_selectedIndex] : default;

            IUnitFacade selectedUnit = null;
            if (hasSelection)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                requestRepaint: Repaint
            );

            DrawToolbar(in ctx);

            EditorGUILayout.BeginHorizontal();
            DrawEntityList(facade);
            DrawEntityDetails(facade);
            EditorGUILayout.EndHorizontal();

            AutoRefresh();
        }

        private void DrawToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("过滤", GUILayout.Width(35));
            var newFilter = GUILayout.TextField(_filter ?? string.Empty, GUI.skin.textField, GUILayout.MinWidth(100));
            if (!string.Equals(newFilter, _filter, StringComparison.Ordinal))
            {
                _filter = newFilter;
                RefreshEntities();
            }

            GUILayout.FlexibleSpace();

            var cmds = BattleDebugToolbarCommandRegistry.GetAll();
            for (int i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];
                if (cmd == null) continue;
                if (!cmd.IsVisible(in ctx)) continue;

                EditorGUI.BeginDisabledGroup(!cmd.IsEnabled(in ctx));
                if (GUILayout.Button(cmd.Label, EditorStyles.toolbarButton))
                {
                    cmd.Execute(in ctx);
                }
                EditorGUI.EndDisabledGroup();
            }

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshEntities();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntityList(IBattleDebugFacade facade)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ID", GUILayout.Width(22));
            _jumpId = GUILayout.TextField(_jumpId ?? string.Empty, GUILayout.MinWidth(60));
            if (GUILayout.Button("跳转", GUILayout.Width(40)))
            {
                if (int.TryParse(_jumpId, out var actorId) && actorId > 0)
                {
                    for (int i = 0; i < _visibleEntities.Count; i++)
                    {
                        if (_visibleEntities[i].ActorId == actorId)
                        {
                            _selectedIndex = i;
                            _entityScroll.y = Mathf.Max(0f, i * 18f);
                            GUI.FocusControl(null);
                            break;
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            _entityScroll = EditorGUILayout.BeginScrollView(_entityScroll);

            if (_visibleEntities.Count == 0)
            {
                EditorGUILayout.LabelField("暂无实体", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < _visibleEntities.Count; i++)
                {
                    var id = _visibleEntities[i];
                    var selected = i == _selectedIndex;
                    var label = id.ToString();

                    if (facade != null && facade.TryResolveUnit(id, out var unit) && unit != null)
                    {
                        var tags = unit.Tags?.Count ?? 0;
                        var effects = unit.Effects?.Active?.Count ?? 0;
                        label = $"{label}  T{tags} E{effects}";
                    }

                    var style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(label, style))
                    {
                        _selectedIndex = i;
                        GUI.FocusControl(null);
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawEntityDetails(IBattleDebugFacade facade)
        {
            EditorGUILayout.BeginVertical();

            var hasSelection = _selectedIndex >= 0 && _selectedIndex < _visibleEntities.Count;
            var selectedId = hasSelection ? _visibleEntities[_selectedIndex] : default;

            IUnitFacade selectedUnit = null;
            if (hasSelection)
            {
                facade.TryResolveUnit(selectedId, out selectedUnit);
            }

            var ctx = new BattleDebugContext(
                facade,
                selectedId,
                selectedUnit,
                requestRepaint: Repaint
            );

            DrawPanelTabs(in ctx);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            DrawSelectedPanel(in ctx);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawPanelTabs(in BattleDebugContext ctx)
        {
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null || panels.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有可用面板。", MessageType.Warning);
                return;
            }

            var visible = new List<IBattleDebugPanel>(panels.Count);
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p == null) continue;
                if (!p.IsVisible(in ctx)) continue;
                visible.Add(p);
            }

            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有可显示的面板。", MessageType.Info);
                return;
            }

            if (_selectedPanelIndex >= visible.Count) _selectedPanelIndex = visible.Count - 1;
            if (_selectedPanelIndex < 0) _selectedPanelIndex = 0;

            var names = new string[visible.Count];
            for (int i = 0; i < visible.Count; i++) names[i] = visible[i].Name;

            _selectedPanelIndex = GUILayout.Toolbar(_selectedPanelIndex, names);
        }

        private void DrawSelectedPanel(in BattleDebugContext ctx)
        {
            var panels = BattleDebugPanelRegistry.GetAll();
            if (panels == null || panels.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有可用面板。", MessageType.Warning);
                return;
            }

            var visible = new List<IBattleDebugPanel>(panels.Count);
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p == null) continue;
                if (!p.IsVisible(in ctx)) continue;
                visible.Add(p);
            }

            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有可显示的面板。", MessageType.Info);
                return;
            }

            if (_selectedPanelIndex >= visible.Count) _selectedPanelIndex = visible.Count - 1;
            if (_selectedPanelIndex < 0) _selectedPanelIndex = 0;

            var selected = visible[_selectedPanelIndex];
            selected.Draw(in ctx);
        }

        private void AutoRefresh()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshAt) return;

            _nextRefreshAt = now + 0.25;
            RefreshEntities();
            Repaint();
        }

        private void RefreshEntities()
        {
            _visibleEntities.Clear();

            var facade = BattleDebugFacadeProvider.Current;
            if (facade == null) return;
            if (!facade.TryListEntities(out var ids) || ids == null) return;

            var filter = string.IsNullOrWhiteSpace(_filter) ? string.Empty : _filter.Trim();

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!global::AbilityKit.Game.Editor.BattleDebugEntityFilter.Matches(facade, id, filter)) continue;

                _visibleEntities.Add(id);
            }

            _visibleEntities.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));

            if (_selectedIndex >= _visibleEntities.Count)
            {
                _selectedIndex = _visibleEntities.Count - 1;
            }
        }
    }
}
