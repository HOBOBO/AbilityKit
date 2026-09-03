#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    [InitializeOnLoad]
    internal static class TriggerAuthoringEditorIntegration
    {
        internal const string ModuleId = "abilitykit.trigger-authoring";

        private static readonly IDisposable LocalizationRegistration;

        static TriggerAuthoringEditorIntegration()
        {
            LocalizationRegistration =
                AbilityKitEditorPlatform.Localization.RegisterSource(CreateLocalizationSource());
        }

        internal static IEditorLocalization Localization
        {
            get
            {
                _ = LocalizationRegistration;
                return AbilityKitEditorPlatform.Localization;
            }
        }

        internal static IEditorLocalizationSource CreateLocalizationSource()
        {
            return new DictionaryEditorLocalizationSource(
                ModuleId,
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["abilitykit.trigger.command.create-project"] = "Create Project",
                        ["abilitykit.trigger.command.create-project.tooltip"] = "Run the project setup wizard (catalogs + starter module)",
                        ["abilitykit.trigger.command.validate-all"] = "Validate All",
                        ["abilitykit.trigger.command.validate-all.tooltip"] = "Validate every Trigger Authoring project",
                        ["abilitykit.trigger.command.refresh"] = "Refresh",
                        ["abilitykit.trigger.command.refresh.tooltip"] = "Refresh projects and modules",
                        ["abilitykit.trigger.command.import"] = "Import",
                        ["abilitykit.trigger.command.import.tooltip"] = "Import Source JSON into this asset",
                        ["abilitykit.trigger.command.export-source"] = "Export",
                        ["abilitykit.trigger.command.export-source.tooltip"] = "Export this asset to Source JSON",
                        ["abilitykit.trigger.command.export-runtime"] = "Runtime",
                        ["abilitykit.trigger.command.export-runtime.tooltip"] = "Compile and export Runtime Plan JSON",
                        ["abilitykit.trigger.command.validate"] = "Validate",
                        ["abilitykit.trigger.command.validate.tooltip"] = "Refresh validation diagnostics",
                        ["abilitykit.trigger.command.export-project"] = "Export Runtime",
                        ["abilitykit.trigger.command.export-project.tooltip"] = "Compile and export the selected project",
                        ["abilitykit.trigger.sourceSync.title"] = "Source Sync",
                        ["abilitykit.trigger.sourceSync.noModule"] = "No module selected."
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.trigger.command.create-project"] = "创建项目",
                        ["abilitykit.trigger.command.create-project.tooltip"] = "运行项目初始化向导（目录与起始模块）",
                        ["abilitykit.trigger.command.validate-all"] = "校验全部",
                        ["abilitykit.trigger.command.validate-all.tooltip"] = "校验全部 Trigger Authoring 项目",
                        ["abilitykit.trigger.command.refresh"] = "刷新",
                        ["abilitykit.trigger.command.refresh.tooltip"] = "刷新项目与模块",
                        ["abilitykit.trigger.command.import"] = "导入",
                        ["abilitykit.trigger.command.import.tooltip"] = "将 Source JSON 导入当前资产",
                        ["abilitykit.trigger.command.export-source"] = "导出",
                        ["abilitykit.trigger.command.export-source.tooltip"] = "将当前资产导出为 Source JSON",
                        ["abilitykit.trigger.command.export-runtime"] = "运行时",
                        ["abilitykit.trigger.command.export-runtime.tooltip"] = "编译并导出 Runtime Plan JSON",
                        ["abilitykit.trigger.command.validate"] = "校验",
                        ["abilitykit.trigger.command.validate.tooltip"] = "刷新校验诊断",
                        ["abilitykit.trigger.command.export-project"] = "导出运行时",
                        ["abilitykit.trigger.command.export-project.tooltip"] = "编译并导出当前项目",
                        ["abilitykit.trigger.sourceSync.title"] = "源同步",
                        ["abilitykit.trigger.sourceSync.noModule"] = "未选择模块。"
                    }
                });
        }
    }

    internal static class TriggerAuthoringCommandIds
    {
        internal const string CreateProject = "trigger.workspace.create-project";
        internal const string ValidateAll = "trigger.workspace.validate-all";
        internal const string Refresh = "trigger.workspace.refresh";
        internal const string Import = "trigger.module.import";
        internal const string ExportSource = "trigger.module.export-source";
        internal const string ExportRuntime = "trigger.module.export-runtime";
        internal const string Validate = "trigger.module.validate";
        internal const string ValidateProject = "trigger.project.validate";
        internal const string ExportProject = "trigger.project.export-runtime";
    }

    internal static class TriggerAuthoringCommandFactory
    {
        internal static IReadOnlyList<EditorCommand> CreateWorkspace(
            Action createProject,
            Action validateAll,
            Action refresh,
            Action validateProject,
            Action exportProject,
            Func<bool> hasProject)
        {
            if (hasProject == null) throw new ArgumentNullException(nameof(hasProject));
            return new[]
            {
                Command(TriggerAuthoringCommandIds.CreateProject, "create-project", createProject),
                Command(TriggerAuthoringCommandIds.ValidateAll, "validate-all", validateAll),
                Command(TriggerAuthoringCommandIds.Refresh, "refresh", refresh),
                Command(TriggerAuthoringCommandIds.ValidateProject, "validate", validateProject, _ => hasProject()),
                Command(TriggerAuthoringCommandIds.ExportProject, "export-project", exportProject, _ => hasProject())
            };
        }

        internal static IReadOnlyList<EditorCommand> CreateModule(
            Action import,
            Action exportSource,
            Action exportRuntime,
            Action validate,
            Func<bool> hasAsset)
        {
            if (hasAsset == null) throw new ArgumentNullException(nameof(hasAsset));
            Func<EditorCommandContext, bool> enabled = _ => hasAsset();
            return new[]
            {
                Command(TriggerAuthoringCommandIds.Import, "import", import, enabled),
                Command(TriggerAuthoringCommandIds.ExportSource, "export-source", exportSource, enabled),
                Command(TriggerAuthoringCommandIds.ExportRuntime, "export-runtime", exportRuntime, enabled),
                Command(TriggerAuthoringCommandIds.Validate, "validate", validate, enabled)
            };
        }

        private static EditorCommand Command(
            string id,
            string resourceName,
            Action execute,
            Func<EditorCommandContext, bool> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            var key = "abilitykit.trigger.command." + resourceName;
            return new EditorCommand(
                id,
                key,
                _ => execute(),
                key + ".tooltip",
                canExecute: canExecute);
        }
    }

    internal static class TriggerAuthoringDiagnosticAdapter
    {
        internal static EditorDiagnosticCollection Adapt(
            IEnumerable<TriggerAuthoringDiagnostic> source,
            UnityEngine.Object target = null,
            Action<string> locatePath = null)
        {
            var collection = new EditorDiagnosticCollection();
            if (source == null) return collection;

            foreach (var diagnostic in source)
            {
                if (diagnostic == null) continue;
                var path = diagnostic.Path;
                Action locate = locatePath != null && !string.IsNullOrWhiteSpace(path)
                    ? () => locatePath(path)
                    : null;
                collection.Add(new EditorDiagnostic(
                    diagnostic.Code,
                    MapSeverity(diagnostic.Severity),
                    diagnostic.Message,
                    path,
                    target,
                    locate));
            }

            return collection;
        }

        private static EditorDiagnosticSeverity MapSeverity(
            TriggerAuthoringDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case TriggerAuthoringDiagnosticSeverity.Error:
                    return EditorDiagnosticSeverity.Error;
                case TriggerAuthoringDiagnosticSeverity.Warning:
                    return EditorDiagnosticSeverity.Warning;
                default:
                    return EditorDiagnosticSeverity.Info;
            }
        }
    }
}
#endif
