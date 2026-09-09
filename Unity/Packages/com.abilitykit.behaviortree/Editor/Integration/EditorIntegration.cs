#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;

using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor.Bootstrap;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Stable localization keys and bilingual resources for Behavior Tree editor chrome.
    /// Domain descriptor names, categories, and tooltips intentionally remain descriptor-owned.
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorLocalization")]
    public static class EditorLocalization
    {
        public const string ModuleId = EditorModule.ModuleId;

        public static IEditorLocalization Localization
        {
            get
            {
                EditorModuleBootstrap.EnsureRegistered();
                return AbilityKitEditorPlatform.Localization;
            }
        }

        internal static IDisposable RegisterSource()
        {
            return AbilityKitEditorPlatform.Localization.RegisterSource(CreateSource());
        }

        public static IEditorLocalizationSource CreateSource()
        {
            return new DictionaryEditorLocalizationSource(
                ModuleId,
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["abilitykit.behaviortree.module.name"] = "行为树",
                        ["abilitykit.behaviortree.panel.observation"] = "行为树运行时观察",
                        ["abilitykit.behaviortree.panel.observation.open"] = "打开运行时观察器",
                        ["abilitykit.behaviortree.panel.create"] = "行为树编辑器",
                        ["abilitykit.behaviortree.panel.create.open"] = "创建行为树",
                        ["abilitykit.behaviortree.mode.edit"] = "行为树",
                        ["abilitykit.behaviortree.mode.observation"] = "观察模式（只读）",
                        ["abilitykit.behaviortree.command.close"] = "关闭",
                        ["abilitykit.behaviortree.command.close.tooltip"] = "关闭当前观察图",
                        ["abilitykit.behaviortree.command.pause"] = "冻结",
                        ["abilitykit.behaviortree.command.pause.tooltip"] = "冻结显示，运行时继续推进",
                        ["abilitykit.behaviortree.command.pause-observation"] = "冻结",
                        ["abilitykit.behaviortree.command.pause-observation.tooltip"] = "冻结显示，运行时继续推进",
                        ["abilitykit.behaviortree.command.resume"] = "继续",
                        ["abilitykit.behaviortree.command.copy-snapshot"] = "复制快照",
                        ["abilitykit.behaviortree.command.copy-snapshot.tooltip"] = "将当前运行状态复制为 JSON",
                        ["abilitykit.behaviortree.command.save"] = "保存",
                        ["abilitykit.behaviortree.command.save.tooltip"] = "保存行为树文档 (Ctrl+S)",
                        ["abilitykit.behaviortree.command.export"] = "导出",
                        ["abilitykit.behaviortree.command.export.tooltip"] = "保存并导出纯运行时 IR (Ctrl+Shift+E)",
                        ["abilitykit.behaviortree.command.undo"] = "撤销",
                        ["abilitykit.behaviortree.command.undo.tooltip"] = "撤销上一步 (Ctrl+Z)",
                        ["abilitykit.behaviortree.command.redo"] = "重做",
                        ["abilitykit.behaviortree.command.redo.tooltip"] = "重做下一步 (Ctrl+Y)",
                        ["abilitykit.behaviortree.command.add-root"] = "添加根节点",
                        ["abilitykit.behaviortree.command.add-root.tooltip"] = "为空树创建可直接运行的根节点",
                        ["abilitykit.behaviortree.command.group"] = "分组",
                        ["abilitykit.behaviortree.command.group.tooltip"] = "将当前选中节点放入新分组",
                        ["abilitykit.behaviortree.command.note"] = "注释",
                        ["abilitykit.behaviortree.command.note.tooltip"] = "在画布中心添加不参与运行时导出的说明",
                        ["abilitykit.behaviortree.command.auto-layout"] = "自动布局",
                        ["abilitykit.behaviortree.command.auto-layout.tooltip"] = "多选时仅整理选中节点，否则整理整棵树 (Ctrl+L)",
                        ["abilitykit.behaviortree.command.frame-all"] = "适应画布",
                        ["abilitykit.behaviortree.command.frame-all.tooltip"] = "显示全部节点",
                        ["abilitykit.behaviortree.command.validate"] = "校验",
                        ["abilitykit.behaviortree.command.validate.tooltip"] = "校验结构、属性和黑板引用",
                        ["abilitykit.behaviortree.search.tooltip"] = "按显示名、节点 ID 或类型查找 (Ctrl+F)",
                        ["abilitykit.behaviortree.state.dirty"] = "未保存",
                        ["abilitykit.behaviortree.state.saved"] = "已保存",
                        ["abilitykit.behaviortree.validation.success"] = "✔ 校验通过",
                        ["abilitykit.behaviortree.validation.errors"] = "✘ {0} 个错误",
                        ["abilitykit.behaviortree.validation.locate"] = "定位节点 {0}",
                        ["abilitykit.behaviortree.observation.stopped"] = "观察模式（实例已停止）",
                        ["abilitykit.behaviortree.observation.frame"] = "观察模式  帧 {0}",
                        ["abilitykit.behaviortree.observation.frame-frozen"] = "观察模式  帧 {0}（已冻结）",
                        ["abilitykit.behaviortree.observation.frame-disconnected"] = "观察模式  帧 {0}（已断开）",
                        ["abilitykit.behaviortree.observation.snapshot-copied"] = "运行快照已复制",
                        ["abilitykit.behaviortree.observation.snapshot-failed"] = "快照复制失败"
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.behaviortree.module.name"] = "行为树",
                        ["abilitykit.behaviortree.panel.observation"] = "行为树运行时观察",
                        ["abilitykit.behaviortree.panel.observation.open"] = "打开运行时观察器",
                        ["abilitykit.behaviortree.panel.create"] = "行为树编辑器",
                        ["abilitykit.behaviortree.panel.create.open"] = "创建行为树",
                        ["abilitykit.behaviortree.mode.edit"] = "行为树",
                        ["abilitykit.behaviortree.mode.observation"] = "观察模式（只读）",
                        ["abilitykit.behaviortree.command.close"] = "关闭",
                        ["abilitykit.behaviortree.command.close.tooltip"] = "关闭当前观察图",
                        ["abilitykit.behaviortree.command.pause"] = "冻结",
                        ["abilitykit.behaviortree.command.pause.tooltip"] = "冻结显示，运行时继续推进",
                        ["abilitykit.behaviortree.command.pause-observation"] = "冻结",
                        ["abilitykit.behaviortree.command.pause-observation.tooltip"] = "冻结显示，运行时继续推进",
                        ["abilitykit.behaviortree.command.resume"] = "继续",
                        ["abilitykit.behaviortree.command.copy-snapshot"] = "复制快照",
                        ["abilitykit.behaviortree.command.copy-snapshot.tooltip"] = "复制当前运行状态 JSON",
                        ["abilitykit.behaviortree.command.save"] = "保存",
                        ["abilitykit.behaviortree.command.save.tooltip"] = "保存行为树文档 (Ctrl+S)",
                        ["abilitykit.behaviortree.command.export"] = "导出",
                        ["abilitykit.behaviortree.command.export.tooltip"] = "保存并导出纯运行时 IR (Ctrl+Shift+E)",
                        ["abilitykit.behaviortree.command.undo"] = "撤销",
                        ["abilitykit.behaviortree.command.undo.tooltip"] = "撤销上一步 (Ctrl+Z)",
                        ["abilitykit.behaviortree.command.redo"] = "重做",
                        ["abilitykit.behaviortree.command.redo.tooltip"] = "重做下一步 (Ctrl+Y)",
                        ["abilitykit.behaviortree.command.add-root"] = "添加根节点",
                        ["abilitykit.behaviortree.command.add-root.tooltip"] = "为空树创建可直接运行的根节点",
                        ["abilitykit.behaviortree.command.group"] = "分组",
                        ["abilitykit.behaviortree.command.group.tooltip"] = "将当前选中节点放入新分组",
                        ["abilitykit.behaviortree.command.note"] = "注释",
                        ["abilitykit.behaviortree.command.note.tooltip"] = "在画布中心添加不参与运行时导出的说明",
                        ["abilitykit.behaviortree.command.auto-layout"] = "自动布局",
                        ["abilitykit.behaviortree.command.auto-layout.tooltip"] = "多选时仅整理选中节点，否则整理整棵树 (Ctrl+L)",
                        ["abilitykit.behaviortree.command.frame-all"] = "适应画布",
                        ["abilitykit.behaviortree.command.frame-all.tooltip"] = "显示全部节点",
                        ["abilitykit.behaviortree.command.validate"] = "校验",
                        ["abilitykit.behaviortree.command.validate.tooltip"] = "校验结构、属性和黑板引用",
                        ["abilitykit.behaviortree.search.tooltip"] = "按显示名、节点 ID 或类型查找 (Ctrl+F)",
                        ["abilitykit.behaviortree.state.dirty"] = "未保存",
                        ["abilitykit.behaviortree.state.saved"] = "已保存",
                        ["abilitykit.behaviortree.validation.success"] = "✔ 校验通过",
                        ["abilitykit.behaviortree.validation.errors"] = "✘ {0} 个错误",
                        ["abilitykit.behaviortree.validation.locate"] = "定位节点 {0}",
                        ["abilitykit.behaviortree.observation.stopped"] = "观察模式（实例已停止）",
                        ["abilitykit.behaviortree.observation.frame"] = "观察模式  帧 {0}",
                        ["abilitykit.behaviortree.observation.frame-frozen"] = "观察模式  帧 {0}（已冻结）",
                        ["abilitykit.behaviortree.observation.frame-disconnected"] = "观察模式  帧 {0}（已断开）",
                        ["abilitykit.behaviortree.observation.snapshot-copied"] = "运行快照已复制",
                        ["abilitykit.behaviortree.observation.snapshot-failed"] = "快照复制失败"
                    }
                });
        }
    }

    internal static class EditorDisplayText
    {
        public static string NodeState(NodeState state) => state switch
        {
            AbilityKit.BehaviorTree.Definition.NodeState.Inactive => "未激活",
            AbilityKit.BehaviorTree.Definition.NodeState.Running => "运行中",
            AbilityKit.BehaviorTree.Definition.NodeState.Success => "成功",
            AbilityKit.BehaviorTree.Definition.NodeState.Failure => "失败",
            AbilityKit.BehaviorTree.Definition.NodeState.Faulted => "故障",
            _ => state.ToString(),
        };

        public static string ObservationState(ObservationSessionState state) => state switch
        {
            ObservationSessionState.NoSample => "等待采样",
            ObservationSessionState.Live => "实时观察",
            ObservationSessionState.Frozen => "已冻结",
            ObservationSessionState.Disconnected => "已断开",
            _ => state.ToString(),
        };

        public static string ChangeKind(ObservationChangeKind kind) => kind switch
        {
            ObservationChangeKind.NodeState => "节点状态",
            ObservationChangeKind.BlackboardValue => "黑板值",
            _ => kind.ToString(),
        };

        public static string ChangeValue(ObservationChangeKind kind, string value)
        {
            if (kind == ObservationChangeKind.NodeState
                && Enum.TryParse<AbilityKit.BehaviorTree.Definition.NodeState>(value, out var state))
            {
                return NodeState(state);
            }
            return value;
        }

        public static string ExportStatus(ExportStatus status) => status switch
        {
            AbilityKit.BehaviorTree.Authoring.ExportStatus.Exported => "已导出",
            AbilityKit.BehaviorTree.Authoring.ExportStatus.Unchanged => "未变化",
            AbilityKit.BehaviorTree.Authoring.ExportStatus.Error => "错误",
            AbilityKit.BehaviorTree.Authoring.ExportStatus.SkippedNoTargets => "已跳过（无目标）",
            _ => status.ToString(),
        };

        public static string PropertyName(string name) => name switch
        {
            "abortType" => "中止类型",
            "successPolicy" => "成功策略",
            "count" => "次数",
            "durationSeconds" => "持续时间（秒）",
            "cooldownSeconds" => "冷却时间（秒）",
            "resultOnCooldown" => "冷却期间结果",
            "resultAfterFirst" => "首次执行后结果",
            "leftKey" => "左侧黑板键",
            "op" => "比较运算符",
            "rightKind" => "右操作数来源",
            "rightKey" => "右侧黑板键",
            "rightBool" => "右侧 Bool 常量",
            "rightInt64" => "右侧 Int64 常量",
            "rightFixed64Raw" => "右侧 Fixed64 常量",
            "rightString" => "右侧字符串常量",
            "percent" => "通过概率（%）",
            "key" => "黑板键",
            "mode" => "计时模式",
            "durationFrames" => "持续帧数",
            "valueKind" => "值来源",
            "fromKey" => "来源黑板键",
            "constBool" => "Bool 常量",
            "constInt64" => "Int64 常量",
            "constFixed64" => "Fixed64 常量",
            "constString" => "字符串常量",
            "message" => "日志内容",
            "level" => "日志级别",
            "treeId" => "引用树 ID",
            _ => name,
        };
    }



    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorCommandIds")]
    public static class EditorCommandIds
    {
        public const string Close = "bt.graph.close";
        public const string PauseObservation = "bt.graph.pause-observation";
        public const string CopySnapshot = "bt.graph.copy-snapshot";
        public const string Save = "bt.graph.save";
        public const string Export = "bt.graph.export";
        public const string Undo = "bt.graph.undo";
        public const string Redo = "bt.graph.redo";
        public const string AddRoot = "bt.graph.add-root";
        public const string Group = "bt.graph.group";
        public const string Note = "bt.graph.note";
        public const string AutoLayout = "bt.graph.auto-layout";
        public const string FrameAll = "bt.graph.frame-all";
        public const string Validate = "bt.graph.validate";
    }



    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorCommandFactory")]
    public static class EditorCommandFactory
    {
        public static IReadOnlyList<EditorCommand> Create(
            Action close,
            Action pauseObservation,
            Action copySnapshot,
            Action save,
            Action export,
            Action undo,
            Action redo,
            Action addRoot,
            Action group,
            Action note,
            Action autoLayout,
            Action frameAll,
            Action validate,
            Func<bool> isReadOnly,
            Func<bool> canUndo,
            Func<bool> canRedo)
        {
            if (isReadOnly == null) throw new ArgumentNullException(nameof(isReadOnly));
            if (canUndo == null) throw new ArgumentNullException(nameof(canUndo));
            if (canRedo == null) throw new ArgumentNullException(nameof(canRedo));

            bool Editable(EditorCommandContext _) => !isReadOnly();
            EditorCommand Command(string id, Action action, Func<EditorCommandContext, bool>? canExecute = null)
                => new(id, "abilitykit.behaviortree.command." + id.Substring("bt.graph.".Length), _ => action(), canExecute: canExecute);

            return new[]
            {
                Command(EditorCommandIds.Close, close),
                Command(EditorCommandIds.PauseObservation, pauseObservation),
                Command(EditorCommandIds.CopySnapshot, copySnapshot),
                Command(EditorCommandIds.Save, save, Editable),
                Command(EditorCommandIds.Export, export, Editable),
                Command(EditorCommandIds.Undo, undo, context => Editable(context) && canUndo()),
                Command(EditorCommandIds.Redo, redo, context => Editable(context) && canRedo()),
                Command(EditorCommandIds.AddRoot, addRoot, Editable),
                Command(EditorCommandIds.Group, group, Editable),
                Command(EditorCommandIds.Note, note, Editable),
                Command(EditorCommandIds.AutoLayout, autoLayout, Editable),
                Command(EditorCommandIds.FrameAll, frameAll),
                Command(EditorCommandIds.Validate, validate, Editable)
            };
        }
    }
}
