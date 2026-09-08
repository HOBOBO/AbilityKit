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
    /// The single navigation entry point for AbilityKit editor tooling. It renders the
    /// registries owned by <see cref="AbilityKitEditorPlatform"/> (modules, menus, panels,
    /// commands) and reflectively surfaces windows still registered via raw
    /// <c>[MenuItem]</c> as a visible migration backlog.
    /// </summary>
    public sealed class EditorHubWindow : EditorWindow
    {
        private const string HubMenuPath = "Window/AbilityKit/Hub";

        private readonly EditorSearchState _search = new EditorSearchState();
        private Vector2 _scroll;
        private IReadOnlyList<EditorMenuItemInfo> _allDiscovered = Array.Empty<EditorMenuItemInfo>();
        private bool _discoveryLoaded;

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
            AbilityKitEditorPlatform.Commands.CommandsChanged += Repaint;
        }

        private void OnDisable()
        {
            _search.Changed -= Repaint;
            AbilityKitEditorPlatform.Localization.LanguageChanged -= Repaint;
            AbilityKitEditorPlatform.Modules.ModulesChanged -= Repaint;
            AbilityKitEditorPlatform.Menus.Changed -= Repaint;
            AbilityKitEditorPlatform.Panels.Changed -= Repaint;
            AbilityKitEditorPlatform.Commands.CommandsChanged -= Repaint;
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
            DrawDiscovered(localization);
            EditorGUILayout.EndScrollView();

            DrawStatusBar(localization);
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
            if (GUILayout.Button(localization.Get("abilitykit.editor.hub.refresh"), EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                RefreshDiscovery();
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
                .Where(module => module?.Descriptor != null
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
                .Where(menu => menu != null && _search.Matches(menu.Path))
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
                .Where(panel => panel != null && _search.Matches(localization.Get(panel.TitleKey), panel.Id))
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
            var discovered = CurrentDiscovered().Where(item => _search.Matches(item.Path)).ToList();

            DrawSectionHeader(localization.Get("abilitykit.editor.hub.section.discovered"), discovered.Count);
            if (discovered.Count == 0)
            {
                DrawInlineEmpty(localization);
                return;
            }

            EditorGUILayout.HelpBox(localization.Get("abilitykit.editor.hub.discoveredNote"), MessageType.Info);
            foreach (var group in GroupByTopRoot(discovered))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (var item in group)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(localization.Get("abilitykit.editor.hub.open"), EditorStyles.miniButton, GUILayout.Width(52f)))
                    {
                        TryOpenPath(item.Path);
                    }
                    EditorGUILayout.LabelField(
                        EditorMenuItemDiscovery.StripRoot(item.Path, EditorMenuItemDiscovery.DefaultRoots),
                        EditorStyles.label);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawStatusBar(IEditorLocalization localization)
        {
            var modules = AbilityKitEditorPlatform.Modules.Modules.Count;
            var menus = AbilityKitEditorPlatform.Menus.Items.Count;
            var panels = AbilityKitEditorPlatform.Panels.Items.Count;
            var commands = AbilityKitEditorPlatform.Commands.Commands.Count;
            var discovered = CurrentDiscovered().Count;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                localization.Format("abilitykit.editor.hub.status", modules, menus, panels, commands, discovered),
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private IReadOnlyList<EditorMenuItemInfo> CurrentDiscovered()
        {
            if (!_discoveryLoaded) RefreshDiscovery();

            return EditorMenuItemDiscovery.Exclude(
                _allDiscovered,
                AbilityKitEditorPlatform.Menus.Items
                    .Where(menu => menu != null)
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

        private static void TryOpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                EditorApplication.ExecuteMenuItem(path);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
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
