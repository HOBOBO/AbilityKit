#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// 展示显式准入的编辑器模块、菜单和面板；未准入菜单只供迁移盘点。
    /// </summary>
    public sealed class EditorHubWindow : EditorWindow
    {
        private const string HubMenuPath = "Window/AbilityKit/Hub";

        private readonly EditorSearchState _search = new EditorSearchState();
        private Vector2 _scroll;
        private IReadOnlyList<EditorMenuItemInfo> _allDiscovered = Array.Empty<EditorMenuItemInfo>();
        private bool _discoveryLoaded;
        private bool _showMigrationInventory;

        /// <summary>
        /// 打开编辑器工具导航，不触发菜单迁移扫描或领域操作。
        /// </summary>
        [MenuItem(HubMenuPath, false, 0)]
        public static void Open()
        {
            GetWindow<EditorHubWindow>(true, "AbilityKit Hub");
        }

        private void OnEnable()
        {
            _search.Changed += Repaint;
            AbilityKitEditorPlatform.Localization.LanguageChanged += Repaint;
            AbilityKitEditorPlatform.Modules.ModulesChanged += Repaint;
            AbilityKitEditorPlatform.Menus.Changed += Repaint;
            AbilityKitEditorPlatform.Panels.Changed += Repaint;
        }

        private void OnDisable()
        {
            _search.Changed -= Repaint;
            AbilityKitEditorPlatform.Localization.LanguageChanged -= Repaint;
            AbilityKitEditorPlatform.Modules.ModulesChanged -= Repaint;
            AbilityKitEditorPlatform.Menus.Changed -= Repaint;
            AbilityKitEditorPlatform.Panels.Changed -= Repaint;
        }

        private void OnGUI()
        {
            var localization = AbilityKitEditorPlatform.Localization;
            DrawTopToolbar(localization);
            EditorImGuiControls.DrawSearch(_search);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRegisteredModules(localization);
            DrawRegisteredMenus(localization);
            DrawRegisteredPanels(localization);
            _showMigrationInventory = EditorGUILayout.Foldout(
                _showMigrationInventory, localization.Get("abilitykit.editor.hub.section.discovered"), true);
            if (_showMigrationInventory)
            {
                DrawDiscovered(localization);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTopToolbar(IEditorLocalization localization)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(localization.Get("abilitykit.editor.hub.title"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(localization.Get("abilitykit.editor.hub.language"), EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                ShowLanguageMenu();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowLanguageMenu()
        {
            var menu = new GenericMenu();
            var current = AbilityKitEditorPlatform.Localization.ProjectDefaultLanguage;
            menu.AddItem(new GUIContent("中文"), current == "zh-CN", () => AbilityKitEditorPlatform.SetProjectDefaultLanguage("zh-CN"));
            menu.AddItem(new GUIContent("English"), current == "en", () => AbilityKitEditorPlatform.SetProjectDefaultLanguage("en"));
            menu.ShowAsContext();
        }

        private void DrawRegisteredModules(IEditorLocalization localization)
        {
            var modules = AbilityKitEditorPlatform.Modules.Modules
                .Where(module => module?.Descriptor?.ShowInHub == true
                    && _search.Matches(localization.Get(module.Descriptor.DisplayNameKey), module.Descriptor.Id))
                .ToList();

            DrawSectionHeader(localization.Get("abilitykit.editor.hub.section.registeredModules"), modules.Count);
            if (modules.Count == 0)
            {
                DrawInlineEmpty(localization);
                return;
            }

            foreach (var module in modules)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(localization.Get(module.Descriptor.DisplayNameKey), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(module.Descriptor.Id, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRegisteredMenus(IEditorLocalization localization)
        {
            var menus = AbilityKitEditorPlatform.Menus.Items
                .Where(menu => menu?.ShowInHub == true && _search.Matches(menu.Path))
                .ToList();

            DrawSectionHeader(localization.Get("abilitykit.editor.hub.section.registeredMenus"), menus.Count);
            if (menus.Count == 0)
            {
                DrawInlineEmpty(localization);
                return;
            }

            foreach (var menu in menus)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(localization.Get("abilitykit.editor.hub.open"), EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    menu.Open();
                }
                EditorGUILayout.LabelField(menu.Path, EditorStyles.label);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRegisteredPanels(IEditorLocalization localization)
        {
            var panels = AbilityKitEditorPlatform.Panels.Items
                .Where(panel => panel?.ShowInHub == true && _search.Matches(localization.Get(panel.TitleKey), panel.Id))
                .ToList();

            DrawSectionHeader(localization.Get("abilitykit.editor.hub.section.registeredPanels"), panels.Count);
            if (panels.Count == 0)
            {
                DrawInlineEmpty(localization);
                return;
            }

            foreach (var panel in panels)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(localization.Get("abilitykit.editor.hub.open"), EditorStyles.miniButton, GUILayout.Width(52f)))
                {
                    EditorPanelHostWindow.Show(localization.Get(panel.TitleKey), new[] { panel });
                }
                EditorGUILayout.LabelField(localization.Get(panel.TitleKey), EditorStyles.label);
                EditorGUILayout.LabelField(panel.SupportsUiToolkit ? "UI Toolkit" : "IMGUI", EditorStyles.miniLabel, GUILayout.Width(72f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDiscovered(IEditorLocalization localization)
        {
            EditorGUILayout.HelpBox(localization.Get("abilitykit.editor.hub.discoveredNote"), MessageType.Info);
            if (GUILayout.Button(localization.Get("abilitykit.editor.hub.refresh"), GUILayout.ExpandWidth(false)))
            {
                RefreshDiscovery();
            }

            var discovered = CurrentDiscovered().Where(item => _search.Matches(item.Path)).ToList();

            if (discovered.Count == 0)
            {
                DrawInlineEmpty(localization);
                return;
            }

            foreach (var group in GroupByTopRoot(discovered))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (var item in group)
                {
                    EditorGUILayout.SelectableLabel(item.Path, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                EditorGUI.indentLevel--;
            }
        }

        private IReadOnlyList<EditorMenuItemInfo> CurrentDiscovered()
        {
            if (!_discoveryLoaded) RefreshDiscovery();

            return EditorMenuItemDiscovery.Exclude(
                _allDiscovered,
                AbilityKitEditorPlatform.Menus.Items
                    .Where(menu => menu?.ShowInHub == true)
                    .Select(menu => menu.Path)
                    .Concat(new[] { HubMenuPath }));
        }

        private void RefreshDiscovery()
        {
            _allDiscovered = EditorMenuItemDiscovery.Discover(
                AppDomain.CurrentDomain.GetAssemblies(),
                EditorMenuItemDiscovery.DefaultRoots);
            _discoveryLoaded = true;
        }

        private static void DrawSectionHeader(string title, int count)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title + " (" + count + ")", EditorStyles.boldLabel);
        }

        private static void DrawInlineEmpty(IEditorLocalization localization)
        {
            EditorGUILayout.HelpBox(localization.Get("abilitykit.editor.hub.empty"), MessageType.Info);
        }

        private static IEnumerable<IGrouping<string, EditorMenuItemInfo>> GroupByTopRoot(
            IEnumerable<EditorMenuItemInfo> items)
        {
            return items
                .GroupBy(item => TopSegment(item.Path), StringComparer.Ordinal)
                .OrderBy(group => SegmentRank(group.Key))
                .ThenBy(group => group.Key, StringComparer.Ordinal);
        }

        private static string TopSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var slash = path.IndexOf('/');
            return slash < 0 ? path : path.Substring(0, slash);
        }

        private static int SegmentRank(string segment)
        {
            switch (segment)
            {
                case "Window": return 0;
                case "Tools": return 1;
                case "Assets": return 2;
                default: return 3;
            }
        }
    }
}
#endif
