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

        internal static string T(string key) => Localization.Get("abilitykit.trigger.ui." + key);

        internal static string F(string key, params object[] args) => Localization.Format("abilitykit.trigger.ui." + key, args);

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
                        ["abilitykit.trigger.sourceSync.noModule"] = "No module selected.",
                        ["abilitykit.trigger.ui.source"] = "Source",
                        ["abilitykit.trigger.ui.import"] = "Import",
                        ["abilitykit.trigger.ui.dismiss"] = "Dismiss",
                        ["abilitykit.trigger.ui.module-id"] = "Module Id",
                        ["abilitykit.trigger.ui.display-name"] = "Display Name",
                        ["abilitykit.trigger.ui.kind"] = "Kind",
                        ["abilitykit.trigger.ui.author"] = "Author",
                        ["abilitykit.trigger.ui.description"] = "Description",
                        ["abilitykit.trigger.ui.group"] = "Group",
                        ["abilitykit.trigger.ui.filter"] = "Filter",
                        ["abilitykit.trigger.ui.no-triggers-match"] = "No triggers match the current search.",
                        ["abilitykit.trigger.ui.duplicate"] = "Duplicate",
                        ["abilitykit.trigger.ui.delete"] = "Delete",
                        ["abilitykit.trigger.ui.visible"] = "Visible {0}",
                        ["abilitykit.trigger.ui.hidden-by-filter"] = "Selected trigger is hidden by current search or filter.",
                        ["abilitykit.trigger.ui.select-or-add-trigger"] = "Select or add a trigger.",
                        ["abilitykit.trigger.ui.create-trigger"] = "Create Trigger",
                        ["abilitykit.trigger.ui.event"] = "Event",
                        ["abilitykit.trigger.ui.allow-external"] = "Allow External",
                        ["abilitykit.trigger.ui.note"] = "Note",
                        ["abilitykit.trigger.ui.cue-id"] = "Cue Id",
                        ["abilitykit.trigger.ui.stop-on-success"] = "Stop On Success",
                        ["abilitykit.trigger.ui.stop-on-failure"] = "Stop On Failure",
                        ["abilitykit.trigger.ui.template-ref-missing"] = "Template reference is missing or ambiguous in the project catalog.",
                        ["abilitykit.trigger.ui.template-bindings-null"] = "Template bindings collection is null.",
                        ["abilitykit.trigger.ui.binding-value-null"] = "Binding value is null.",
                        ["abilitykit.trigger.ui.using-template-default"] = "Using template default",
                        ["abilitykit.trigger.ui.required-binding-missing"] = "Required binding is missing.",
                        ["abilitykit.trigger.ui.add-format"] = "Add {0}",
                        ["abilitykit.trigger.ui.paste-format"] = "Paste {0}",
                        ["abilitykit.trigger.ui.disabled-nodes-note"] = "Disabled nodes stay in source JSON but are ignored by validation and Runtime Plan export.",
                        ["abilitykit.trigger.ui.unknown-node-descriptor"] = "Unknown node descriptor. Existing arguments are preserved.",
                        ["abilitykit.trigger.ui.add"] = "Add",
                        ["abilitykit.trigger.ui.add-raw-argument"] = "Add Raw Argument",
                        ["abilitykit.trigger.ui.value"] = "Value",
                        ["abilitykit.trigger.ui.values"] = "Values",
                        ["abilitykit.trigger.ui.choose-value-type"] = "Choose a value type.",
                        ["abilitykit.trigger.ui.fields"] = "Fields",
                        ["abilitykit.trigger.ui.type"] = "Type",
                        ["abilitykit.trigger.ui.add-field"] = "Add Field",
                        ["abilitykit.trigger.ui.add-extra-field"] = "Add Extra Field",
                        ["abilitykit.trigger.ui.default-value"] = "Default Value",
                        ["abilitykit.trigger.ui.local-var-key-required"] = "Local var key is required.",
                        ["abilitykit.trigger.ui.duplicate-local-var"] = "Duplicate local var key in this scope.",
                        ["abilitykit.trigger.ui.shadows-module-var"] = "This trigger local var shadows a module local var with the same key.",
                        ["abilitykit.trigger.ui.add-root"] = "Add Root",
                        ["abilitykit.trigger.ui.disabled-groups-note"] = "Disabled group references stay in source JSON but are ignored by validation and Runtime Plan export.",
                        ["abilitykit.trigger.ui.focused-format"] = "Focused: {0}",
                        ["abilitykit.trigger.ui.clear"] = "Clear",
                        ["abilitykit.trigger.ui.no-diagnostics"] = "No diagnostics.",
                        ["abilitykit.trigger.ui.template-id"] = "Template Id",
                        ["abilitykit.trigger.ui.version"] = "Version",
                        ["abilitykit.trigger.ui.source-note"] = "Source Note",
                        ["abilitykit.trigger.ui.has-default"] = "Has Default",
                        ["abilitykit.trigger.ui.add-parameter"] = "Add Parameter",
                        ["abilitykit.trigger.ui.template-trees-note"] = "Template trees cannot reference module-local groups.",
                        ["abilitykit.trigger.ui.group-reference"] = "Group Reference",
                        ["abilitykit.trigger.ui.clear-group-reference"] = "Clear Group Reference",
                        ["abilitykit.trigger.ui.children-format"] = "Children ({0})",
                        ["abilitykit.trigger.ui.unknown-argument-format"] = "Unknown argument '{0}' is preserved.",
                        ["abilitykit.trigger.ui.output-root"] = "Output Root",
                        ["abilitykit.trigger.ui.browse"] = "Browse",
                        ["abilitykit.trigger.ui.export-all-runtime-plans"] = "Export All Runtime Plans",
                        ["abilitykit.trigger.ui.validate"] = "Validate",
                        ["abilitykit.trigger.ui.export-runtime"] = "Export Runtime",
                        ["abilitykit.trigger.ui.not-validated-yet"] = "Not validated yet.",
                        ["abilitykit.trigger.ui.no-references"] = "No references found in this project.",
                        ["abilitykit.trigger.ui.select"] = "Select",
                        ["abilitykit.trigger.ui.sync-state"] = "Sync State",
                        ["abilitykit.trigger.ui.open-source-json"] = "Open Source JSON",
                        ["abilitykit.trigger.ui.reveal-source"] = "Reveal Source",
                        ["abilitykit.trigger.ui.overwrite-warning"] = "This import will overwrite local asset changes.",
                        ["abilitykit.trigger.ui.summary"] = "Summary",
                        ["abilitykit.trigger.ui.no-structural-changes"] = "No structural changes detected.",
                        ["abilitykit.trigger.ui.cancel"] = "Cancel",
                        ["abilitykit.trigger.ui.load-moba-defaults"] = "Load MOBA Defaults",
                        ["abilitykit.trigger.ui.scan-assemblies"] = "Scan Assemblies",
                        ["abilitykit.trigger.ui.expression"] = "Expression",
                        ["abilitykit.trigger.ui.path"] = "Path",
                        ["abilitykit.trigger.ui.search"] = "Search",
                        ["abilitykit.trigger.ui.id"] = "Id",
                        ["abilitykit.trigger.ui.ok"] = "OK"
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
                        ["abilitykit.trigger.sourceSync.noModule"] = "未选择模块。",
                        ["abilitykit.trigger.ui.source"] = "源",
                        ["abilitykit.trigger.ui.import"] = "导入",
                        ["abilitykit.trigger.ui.dismiss"] = "忽略",
                        ["abilitykit.trigger.ui.module-id"] = "模块 Id",
                        ["abilitykit.trigger.ui.display-name"] = "显示名",
                        ["abilitykit.trigger.ui.kind"] = "类型",
                        ["abilitykit.trigger.ui.author"] = "作者",
                        ["abilitykit.trigger.ui.description"] = "描述",
                        ["abilitykit.trigger.ui.group"] = "分组",
                        ["abilitykit.trigger.ui.filter"] = "筛选",
                        ["abilitykit.trigger.ui.no-triggers-match"] = "没有触发器匹配当前搜索。",
                        ["abilitykit.trigger.ui.duplicate"] = "复制",
                        ["abilitykit.trigger.ui.delete"] = "删除",
                        ["abilitykit.trigger.ui.visible"] = "可见 {0}",
                        ["abilitykit.trigger.ui.hidden-by-filter"] = "选中的触发器被当前搜索或筛选隐藏。",
                        ["abilitykit.trigger.ui.select-or-add-trigger"] = "选择或新增一个触发器。",
                        ["abilitykit.trigger.ui.create-trigger"] = "创建触发器",
                        ["abilitykit.trigger.ui.event"] = "事件",
                        ["abilitykit.trigger.ui.allow-external"] = "允许外部",
                        ["abilitykit.trigger.ui.note"] = "备注",
                        ["abilitykit.trigger.ui.cue-id"] = "表现 Id",
                        ["abilitykit.trigger.ui.stop-on-success"] = "成功时停止",
                        ["abilitykit.trigger.ui.stop-on-failure"] = "失败时停止",
                        ["abilitykit.trigger.ui.template-ref-missing"] = "项目目录中的模板引用缺失或不明确。",
                        ["abilitykit.trigger.ui.template-bindings-null"] = "模板绑定集合为空。",
                        ["abilitykit.trigger.ui.binding-value-null"] = "绑定值为空。",
                        ["abilitykit.trigger.ui.using-template-default"] = "使用模板默认值",
                        ["abilitykit.trigger.ui.required-binding-missing"] = "缺少必需的绑定。",
                        ["abilitykit.trigger.ui.add-format"] = "添加 {0}",
                        ["abilitykit.trigger.ui.paste-format"] = "粘贴 {0}",
                        ["abilitykit.trigger.ui.disabled-nodes-note"] = "禁用的节点保留在源 JSON 中，但校验和运行时计划导出会忽略它们。",
                        ["abilitykit.trigger.ui.unknown-node-descriptor"] = "未知节点描述符，保留现有参数。",
                        ["abilitykit.trigger.ui.add"] = "添加",
                        ["abilitykit.trigger.ui.add-raw-argument"] = "添加原始参数",
                        ["abilitykit.trigger.ui.value"] = "值",
                        ["abilitykit.trigger.ui.values"] = "值列表",
                        ["abilitykit.trigger.ui.choose-value-type"] = "选择一个值类型。",
                        ["abilitykit.trigger.ui.fields"] = "字段",
                        ["abilitykit.trigger.ui.type"] = "类型",
                        ["abilitykit.trigger.ui.add-field"] = "添加字段",
                        ["abilitykit.trigger.ui.add-extra-field"] = "添加额外字段",
                        ["abilitykit.trigger.ui.default-value"] = "默认值",
                        ["abilitykit.trigger.ui.local-var-key-required"] = "需要本地变量 key。",
                        ["abilitykit.trigger.ui.duplicate-local-var"] = "此作用域内本地变量 key 重复。",
                        ["abilitykit.trigger.ui.shadows-module-var"] = "此触发器的本地变量与同 key 的模块本地变量冲突。",
                        ["abilitykit.trigger.ui.add-root"] = "添加根节点",
                        ["abilitykit.trigger.ui.disabled-groups-note"] = "禁用的分组引用保留在源 JSON 中，但校验和运行时计划导出会忽略它们。",
                        ["abilitykit.trigger.ui.focused-format"] = "聚焦: {0}",
                        ["abilitykit.trigger.ui.clear"] = "清除",
                        ["abilitykit.trigger.ui.no-diagnostics"] = "无诊断。",
                        ["abilitykit.trigger.ui.template-id"] = "模板 Id",
                        ["abilitykit.trigger.ui.version"] = "版本",
                        ["abilitykit.trigger.ui.source-note"] = "源备注",
                        ["abilitykit.trigger.ui.has-default"] = "有默认值",
                        ["abilitykit.trigger.ui.add-parameter"] = "添加参数",
                        ["abilitykit.trigger.ui.template-trees-note"] = "模板树不能引用模块级分组。",
                        ["abilitykit.trigger.ui.group-reference"] = "分组引用",
                        ["abilitykit.trigger.ui.clear-group-reference"] = "清除分组引用",
                        ["abilitykit.trigger.ui.children-format"] = "子节点 ({0})",
                        ["abilitykit.trigger.ui.unknown-argument-format"] = "未知参数 '{0}' 已保留。",
                        ["abilitykit.trigger.ui.output-root"] = "输出根目录",
                        ["abilitykit.trigger.ui.browse"] = "浏览",
                        ["abilitykit.trigger.ui.export-all-runtime-plans"] = "导出全部运行时计划",
                        ["abilitykit.trigger.ui.validate"] = "校验",
                        ["abilitykit.trigger.ui.export-runtime"] = "导出运行时",
                        ["abilitykit.trigger.ui.not-validated-yet"] = "尚未校验。",
                        ["abilitykit.trigger.ui.no-references"] = "此项目未找到引用。",
                        ["abilitykit.trigger.ui.select"] = "选择",
                        ["abilitykit.trigger.ui.sync-state"] = "同步状态",
                        ["abilitykit.trigger.ui.open-source-json"] = "打开源 JSON",
                        ["abilitykit.trigger.ui.reveal-source"] = "定位源文件",
                        ["abilitykit.trigger.ui.overwrite-warning"] = "此导入会覆盖本地资产改动。",
                        ["abilitykit.trigger.ui.summary"] = "摘要",
                        ["abilitykit.trigger.ui.no-structural-changes"] = "未检测到结构变更。",
                        ["abilitykit.trigger.ui.cancel"] = "取消",
                        ["abilitykit.trigger.ui.load-moba-defaults"] = "加载 MOBA 默认",
                        ["abilitykit.trigger.ui.scan-assemblies"] = "扫描程序集",
                        ["abilitykit.trigger.ui.expression"] = "表达式",
                        ["abilitykit.trigger.ui.path"] = "路径",
                        ["abilitykit.trigger.ui.search"] = "搜索",
                        ["abilitykit.trigger.ui.id"] = "ID",
                        ["abilitykit.trigger.ui.ok"] = "确定"
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
