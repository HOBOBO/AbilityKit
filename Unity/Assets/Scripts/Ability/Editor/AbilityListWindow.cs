using System.Collections.Generic;
using AbilityKit.Configs;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityListWindow : OdinMenuEditorWindow
    {
        private const string PrefKey = "AbilityKit.AbilityEditor.RootFolder";
        private const string PrefFilterKey = "AbilityKit.AbilityEditor.Filter";

        private const string PrefAutoExportSelectedKey = "AbilityKit.AbilityEditor.AutoExportSelected";

        private double _nextAutoExportAt;
        private const double AutoExportDebounceSeconds = 0.35;

        [MenuItem("Tools/AbilityKit/Ability/Ability List")]
        private static void Open()
        {
            GetWindow<AbilityListWindow>("Ability List");
        }

        protected override void DrawMenu()
        {
            AbilityListLeftPanel.Draw(this);

            var exts = AbilityListWindowExtensionRegistry.GetAll();
            for (int i = 0; i < exts.Count; i++)
            {
                exts[i].DrawLeftPanel(this);
            }

            GUILayout.Space(4);
            base.DrawMenu();
        }

        [ShowInInspector]
        [PropertyOrder(0)]
        [FolderPath(AbsolutePath = false, RequireExistingPath = true)]
        public string RootFolder
        {
            get => EditorPrefs.GetString(PrefKey, "Assets");
            set
            {
                EditorPrefs.SetString(PrefKey, value);
                ForceMenuTreeRebuild();
            }
        }

        [ShowInInspector]
        [PropertyOrder(1)]
        [LabelText("筛选")]
        [CustomValueDrawer(nameof(DrawFilterField))]
        public string Filter
        {
            get => EditorPrefs.GetString(PrefFilterKey, string.Empty);
            set
            {
                EditorPrefs.SetString(PrefFilterKey, value ?? string.Empty);
                ForceMenuTreeRebuild();
            }
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false);

            var folder = RootFolder;
            if (string.IsNullOrEmpty(folder)) folder = "Assets";

            var filter = EditorPrefs.GetString(PrefFilterKey, string.Empty);
            filter = string.IsNullOrWhiteSpace(filter) ? string.Empty : filter.Trim();

            AddAbilityModulesToTree(tree, folder, filter);

            return tree;
        }

        protected override void OnBeginDrawEditors()
        {
            var selected = MenuTree.Selection.SelectedValue as AbilityModuleSO;

            var autoExportSelected = EditorPrefs.GetBool(PrefAutoExportSelectedKey, false);
            AbilityListToolbar.Draw(
                this,
                selected,
                autoExportSelected,
                out var newAutoExportSelected
            );

            var exts = AbilityListWindowExtensionRegistry.GetAll();
            for (int i = 0; i < exts.Count; i++)
            {
                exts[i].DrawToolbar(this);
            }

            if (newAutoExportSelected != autoExportSelected)
            {
                EditorPrefs.SetBool(PrefAutoExportSelectedKey, newAutoExportSelected);
            }

            if (selected != null && selected.Triggers != null)
            {
                AbilityEditorVarKeyContext.CurrentModule = selected;

                for (int i = 0; i < selected.Triggers.Count; i++)
                {
                    var t = selected.Triggers[i];
                    if (t == null) continue;
                    t.Owner = selected;
                }

                if (newAutoExportSelected && EditorUtility.IsDirty(selected))
                {
                    var now = EditorApplication.timeSinceStartup;
                    if (now >= _nextAutoExportAt)
                    {
                        AbilityEditorExporter.Export(selected);
                        EditorUtility.ClearDirty(selected);
                        _nextAutoExportAt = now + AutoExportDebounceSeconds;
                    }
                }
            }

            base.OnBeginDrawEditors();

            AbilityEditorVarKeyContext.CurrentModule = null;
            AbilityEditorVarKeyContext.CurrentTrigger = null;
        }

        internal void RequestRebuild()
        {
            ForceMenuTreeRebuild();
        }

        internal void ExecuteCreate()
        {
            CreateAbilityModuleAsset();
            ForceMenuTreeRebuild();
        }

        internal void ExecuteExportSelected(AbilityModuleSO selected)
        {
            if (selected == null) return;
            AbilityEditorExporter.Export(selected);
        }

        internal void ExecuteExportAll()
        {
            AbilityEditorExporter.ExportAll(RootFolder);
        }

        private void CreateAbilityModuleAsset()
        {
            var folder = RootFolder;
            if (string.IsNullOrEmpty(folder)) folder = "Assets";

            var path = EditorUtility.SaveFilePanelInProject(
                "Create AbilityModuleSO",
                "AbilityModule",
                "asset",
                "",
                folder
            );

            if (string.IsNullOrEmpty(path)) return;

            var so = CreateInstance<AbilityModuleSO>();
            so.AbilityId = so.name;
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = so;
        }

        private static bool MatchesFilter(AbilityModuleSO asset, string path, string filter)
        {
            return AbilityEditorFilterSyntax.MatchesFilter(asset, path, filter);
        }

        private void AddAbilityModulesToTree(OdinMenuTree tree, string folder, string filter)
        {
            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                if (asset == null) continue;

                if (!MatchesFilter(asset, path, filter)) continue;
                tree.Add(path, asset);
            }
        }

        private string DrawFilterField(string value, GUIContent label)
        {
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label(label, GUILayout.Width(35));
            var newValue = SirenixEditorGUI.ToolbarSearchField(value);
            if (SirenixEditorGUI.ToolbarButton("Clear"))
            {
                newValue = string.Empty;
            }
            SirenixEditorGUI.EndHorizontalToolbar();
            return newValue;
        }
    }
}
