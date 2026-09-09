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
                        ["abilitykit.trigger.command.create-project"] = "创建项目",
                        ["abilitykit.trigger.command.create-project.tooltip"] = "运行项目初始化向导（目录与起始模块）",
                        ["abilitykit.trigger.command.validate-all"] = "校验全部",
                        ["abilitykit.trigger.command.validate-all.tooltip"] = "校验全部触发器编辑项目",
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
                        ["abilitykit.trigger.ui.module-id"] = "模块 ID",
                        ["abilitykit.trigger.ui.display-name"] = "显示名称",
                        ["abilitykit.trigger.ui.kind"] = "类型",
                        ["abilitykit.trigger.ui.author"] = "作者",
                        ["abilitykit.trigger.ui.description"] = "描述",
                        ["abilitykit.trigger.ui.group"] = "分组",
                        ["abilitykit.trigger.ui.filter"] = "筛选",
                        ["abilitykit.trigger.ui.no-triggers-match"] = "没有触发器匹配当前搜索条件。",
                        ["abilitykit.trigger.ui.duplicate"] = "复制",
                        ["abilitykit.trigger.ui.delete"] = "删除",
                        ["abilitykit.trigger.ui.visible"] = "显示 {0}",
                        ["abilitykit.trigger.ui.hidden-by-filter"] = "所选触发器已被当前搜索或筛选条件隐藏。",
                        ["abilitykit.trigger.ui.select-or-add-trigger"] = "请选择或新增一个触发器。",
                        ["abilitykit.trigger.ui.create-trigger"] = "创建触发器",
                        ["abilitykit.trigger.ui.event"] = "事件",
                        ["abilitykit.trigger.ui.allow-external"] = "允许外部调用",
                        ["abilitykit.trigger.ui.note"] = "备注",
                        ["abilitykit.trigger.ui.cue-id"] = "表现 ID",
                        ["abilitykit.trigger.ui.stop-on-success"] = "成功时停止",
                        ["abilitykit.trigger.ui.stop-on-failure"] = "失败时停止",
                        ["abilitykit.trigger.ui.template-ref-missing"] = "项目目录中的模板引用缺失或不明确。",
                        ["abilitykit.trigger.ui.template-bindings-null"] = "模板绑定集合为空。",
                        ["abilitykit.trigger.ui.binding-value-null"] = "绑定值为空。",
                        ["abilitykit.trigger.ui.using-template-default"] = "使用模板默认值",
                        ["abilitykit.trigger.ui.required-binding-missing"] = "缺少必需的绑定。",
                        ["abilitykit.trigger.ui.add-format"] = "添加 {0}",
                        ["abilitykit.trigger.ui.paste-format"] = "粘贴 {0}",
                        ["abilitykit.trigger.ui.disabled-nodes-note"] = "禁用的节点仍会保留在 Source JSON 中，但校验和 Runtime Plan 导出会忽略它们。",
                        ["abilitykit.trigger.ui.unknown-node-descriptor"] = "未知节点描述符，现有参数将继续保留。",
                        ["abilitykit.trigger.ui.add"] = "添加",
                        ["abilitykit.trigger.ui.add-raw-argument"] = "添加原始参数",
                        ["abilitykit.trigger.ui.value"] = "值",
                        ["abilitykit.trigger.ui.values"] = "值列表",
                        ["abilitykit.trigger.ui.choose-value-type"] = "请选择值类型。",
                        ["abilitykit.trigger.ui.fields"] = "字段",
                        ["abilitykit.trigger.ui.type"] = "类型",
                        ["abilitykit.trigger.ui.add-field"] = "添加字段",
                        ["abilitykit.trigger.ui.add-extra-field"] = "添加额外字段",
                        ["abilitykit.trigger.ui.default-value"] = "默认值",
                        ["abilitykit.trigger.ui.local-var-key-required"] = "必须填写本地变量 Key。",
                        ["abilitykit.trigger.ui.duplicate-local-var"] = "当前作用域中存在重复的本地变量 Key。",
                        ["abilitykit.trigger.ui.shadows-module-var"] = "此触发器的本地变量与模块中同名的本地变量冲突。",
                        ["abilitykit.trigger.ui.add-root"] = "添加根节点",
                        ["abilitykit.trigger.ui.disabled-groups-note"] = "禁用的分组引用仍会保留在 Source JSON 中，但校验和 Runtime Plan 导出会忽略它们。",
                        ["abilitykit.trigger.ui.focused-format"] = "当前定位：{0}",
                        ["abilitykit.trigger.ui.clear"] = "清除",
                        ["abilitykit.trigger.ui.no-diagnostics"] = "暂无诊断。",
                        ["abilitykit.trigger.ui.template-id"] = "模板 ID",
                        ["abilitykit.trigger.ui.version"] = "版本",
                        ["abilitykit.trigger.ui.source-note"] = "来源备注",
                        ["abilitykit.trigger.ui.has-default"] = "包含默认值",
                        ["abilitykit.trigger.ui.add-parameter"] = "添加参数",
                        ["abilitykit.trigger.ui.template-trees-note"] = "模板树不能引用模块级分组。",
                        ["abilitykit.trigger.ui.group-reference"] = "分组引用",
                        ["abilitykit.trigger.ui.clear-group-reference"] = "清除分组引用",
                        ["abilitykit.trigger.ui.children-format"] = "子节点（{0}）",
                        ["abilitykit.trigger.ui.unknown-argument-format"] = "未知参数“{0}”已保留。",
                        ["abilitykit.trigger.ui.output-root"] = "输出根目录",
                        ["abilitykit.trigger.ui.browse"] = "浏览",
                        ["abilitykit.trigger.ui.export-all-runtime-plans"] = "导出全部 Runtime Plan",
                        ["abilitykit.trigger.ui.validate"] = "校验",
                        ["abilitykit.trigger.ui.export-runtime"] = "导出运行时",
                        ["abilitykit.trigger.ui.not-validated-yet"] = "尚未校验。",
                        ["abilitykit.trigger.ui.no-references"] = "当前项目中未找到引用。",
                        ["abilitykit.trigger.ui.select"] = "选择",
                        ["abilitykit.trigger.ui.sync-state"] = "同步状态",
                        ["abilitykit.trigger.ui.open-source-json"] = "打开 Source JSON",
                        ["abilitykit.trigger.ui.reveal-source"] = "定位源文件",
                        ["abilitykit.trigger.ui.overwrite-warning"] = "本次导入会覆盖本地资产改动。",
                        ["abilitykit.trigger.ui.summary"] = "摘要",
                        ["abilitykit.trigger.ui.no-structural-changes"] = "未检测到结构变更。",
                        ["abilitykit.trigger.ui.cancel"] = "取消",
                        ["abilitykit.trigger.ui.load-moba-defaults"] = "加载 MOBA 默认配置",
                        ["abilitykit.trigger.ui.scan-assemblies"] = "扫描程序集",
                        ["abilitykit.trigger.ui.expression"] = "表达式",
                        ["abilitykit.trigger.ui.path"] = "路径",
                        ["abilitykit.trigger.ui.search"] = "搜索",
                        ["abilitykit.trigger.ui.id"] = "ID",
                        ["abilitykit.trigger.ui.ok"] = "确定"
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.trigger.command.create-project"] = "创建项目",
                        ["abilitykit.trigger.command.create-project.tooltip"] = "运行项目初始化向导（目录与起始模块）",
                        ["abilitykit.trigger.command.validate-all"] = "校验全部",
                        ["abilitykit.trigger.command.validate-all.tooltip"] = "校验全部触发器编辑项目",
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
