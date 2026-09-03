#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Inspectors;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.UI;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Windows
{
    /// <summary>
    /// Ability-owned project/module navigation panel. It binds concrete Ability authoring assets;
    /// the generic search and layout primitives remain owned by Editor.Platform.
    /// </summary>
    internal sealed class TriggerAuthoringProjectTreePanel
    {
        private Vector2 _scroll;
        private string _search = string.Empty;

        internal void Draw(
            IReadOnlyList<TriggerAuthoringProjectAsset> projects,
            IReadOnlyList<TriggerAuthoringModuleAsset> unassignedModules,
            TriggerAuthoringModuleAsset selectedModule,
            Action<TriggerAuthoringModuleAsset> selectModule,
            float width)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            if (unassignedModules == null) throw new ArgumentNullException(nameof(unassignedModules));
            if (selectModule == null) throw new ArgumentNullException(nameof(selectModule));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width));
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20f)) &&
                !string.IsNullOrEmpty(_search))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var filter = (_search ?? string.Empty).Trim();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null) continue;
                GUILayout.Label(project.name, EditorStyles.boldLabel);
                var modules = project.Modules;
                for (var m = 0; m < modules.Count; m++)
                {
                    if (modules[m] != null)
                        DrawModuleRow(modules[m], selectedModule, filter, selectModule);
                }
            }

            if (unassignedModules.Count > 0)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"Unassigned Modules ({unassignedModules.Count})", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "These modules are not registered in any project; the build gate skips them.",
                    MessageType.Warning);
                for (var i = 0; i < unassignedModules.Count; i++)
                    DrawModuleRow(unassignedModules[i], selectedModule, filter, selectModule);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawModuleRow(
            TriggerAuthoringModuleAsset module,
            TriggerAuthoringModuleAsset selectedModule,
            string filter,
            Action<TriggerAuthoringModuleAsset> selectModule)
        {
            var moduleId = module.Module != null ? module.Module.ModuleId : null;
            var summary = string.IsNullOrWhiteSpace(moduleId) ? module.name : moduleId;
            var triggerCount = module.Module != null && module.Module.Triggers != null
                ? module.Module.Triggers.Count
                : 0;
            var label = summary + "  (" + triggerCount + ")";
            if (filter.Length > 0 &&
                label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                module.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var oldBackground = GUI.backgroundColor;
            if (module == selectedModule) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(24f)))
                selectModule(module);
            GUI.backgroundColor = oldBackground;
        }
    }

    internal sealed class TriggerAuthoringModuleContentPanel
    {
        private Vector2 _scroll;

        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            TriggerAuthoringModuleDrawer drawer)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (selectedModule == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a module on the left to start editing.\n" +
                    "No project yet? Use Create Project in the toolbar (catalogs + starter module).",
                    MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            drawer?.Draw();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }

    internal sealed class TriggerAuthoringSourceSyncPanel
    {
        private TriggerAuthoringSyncInspection _inspection;
        private double _nextInspectionAt;

        internal void Invalidate()
        {
            _inspection = null;
            _nextInspectionAt = 0d;
        }

        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            Action importSource,
            Action exportSource)
        {
            if (selectedModule == null)
            {
                var localization =
                    TriggerAuthoringEditorIntegration
                        .Localization;
                SirenixEditorGUI.BeginBox(
                    localization.Get(
                        "abilitykit.trigger.sourceSync.title"));
                EditorGUILayout.LabelField(
                    localization.Get(
                        "abilitykit.trigger.sourceSync.noModule"),
                    EditorStyles.miniLabel);
                SirenixEditorGUI.EndBox();
                return;
            }

            if (_inspection == null || _nextInspectionAt <= EditorApplication.timeSinceStartup)
            {
                _inspection = TriggerAuthoringSourceSync.Inspect(selectedModule);
                _nextInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
            }

            var inspection = _inspection;
            EditorImGuiControls.DrawSourceSyncCard(
                new EditorSourceSyncCardModel(
                    inspection.PlatformInspection,
                    importSource,
                    exportSource,
                    copyPath: () => EditorGUIUtility.systemCopyBuffer = inspection.SourcePath ?? string.Empty,
                    revealPath: () => EditorUtility.RevealInFinder(inspection.SourcePath),
                    title: TriggerAuthoringEditorIntegration.Localization.Get(
                        "abilitykit.trigger.sourceSync.title"),
                    localization: TriggerAuthoringEditorIntegration.Localization));
        }
    }

    internal sealed class TriggerAuthoringProjectValidationPanel
    {
        internal void Draw(
            TriggerAuthoringModuleAsset selectedModule,
            TriggerAuthoringProjectValidationResult validation,
            TriggerAuthoringProjectAsset validationProject,
            EditorDiagnosticCollection diagnostics,
            EditorCommandRegistry commands,
            UnityEngine.Object commandOwner)
        {
            SirenixEditorGUI.BeginBox("Project Validation");
            var project = selectedModule != null ? selectedModule.Project : null;
            if (project == null)
            {
                EditorGUILayout.HelpBox(
                    "The selected module is not registered in a project.",
                    MessageType.Warning);
                SirenixEditorGUI.EndBox();
                return;
            }

            EditorGUILayout.LabelField(project.name, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", EditorStyles.miniButtonLeft))
                commands.Execute(
                    TriggerAuthoringCommandIds.ValidateProject,
                    new EditorCommandContext(commandOwner, project));
            if (GUILayout.Button("Export Runtime", EditorStyles.miniButtonRight))
                commands.Execute(
                    TriggerAuthoringCommandIds.ExportProject,
                    new EditorCommandContext(commandOwner, project));
            EditorGUILayout.EndHorizontal();

            if (validation != null && validationProject == project)
            {
                EditorGUILayout.LabelField(
                    $"{validation.ModuleCount} modules, {validation.TemplateCount} templates  " +
                    $"(E{diagnostics.ErrorCount} W{diagnostics.WarningCount})",
                    EditorStyles.miniLabel);
                for (var i = 0; i < diagnostics.Items.Count; i++)
                {
                    var diagnostic = diagnostics.Items[i];
                    var icon = diagnostic.Severity == EditorDiagnosticSeverity.Error
                        ? EditorGUIUtility.IconContent("console.erroricon.sml")
                        : diagnostic.Severity == EditorDiagnosticSeverity.Warning
                            ? EditorGUIUtility.IconContent("console.warnicon.sml")
                            : EditorGUIUtility.IconContent("console.infoicon.sml");
                    var content = new GUIContent(
                        diagnostic.Code + " " + diagnostic.Path + "\n" + diagnostic.Message,
                        icon != null ? icon.image : null,
                        diagnostic.Locate != null ? "Locate related asset" : string.Empty);
                    using (new EditorGUI.DisabledScope(diagnostic.Locate == null))
                    {
                        if (GUILayout.Button(content, EditorStyles.helpBox, GUILayout.MinHeight(38f)))
                            diagnostic.Locate?.Invoke();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Not validated yet.", EditorStyles.miniLabel);
            }

            SirenixEditorGUI.EndBox();
        }
    }
}
#endif
