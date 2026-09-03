#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;
using UnityEditor;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Stable localization keys and bilingual resources for Behavior Tree editor chrome.
    /// Domain descriptor names, categories, and tooltips intentionally remain descriptor-owned.
    /// </summary>
    [InitializeOnLoad]
    public static class BtEditorLocalization
    {
        public const string ModuleId = "abilitykit.behaviortree";

        private static readonly IDisposable Registration;

        static BtEditorLocalization()
        {
            Registration = AbilityKitEditorPlatform.Localization.RegisterSource(CreateSource());
        }

        public static IEditorLocalization Localization
        {
            get
            {
                _ = Registration;
                return AbilityKitEditorPlatform.Localization;
            }
        }

        public static IEditorLocalizationSource CreateSource()
        {
            return new DictionaryEditorLocalizationSource(
                ModuleId,
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en"] = new Dictionary<string, string>
                    {
                        ["abilitykit.behaviortree.mode.edit"] = "Behavior Tree",
                        ["abilitykit.behaviortree.mode.observation"] = "Observation (Read-only)",
                        ["abilitykit.behaviortree.command.close"] = "Close",
                        ["abilitykit.behaviortree.command.close.tooltip"] = "Close the current observation graph",
                        ["abilitykit.behaviortree.command.pause"] = "Freeze",
                        ["abilitykit.behaviortree.command.pause.tooltip"] = "Freeze the display while runtime continues",
                        ["abilitykit.behaviortree.command.pause-observation"] = "Freeze",
                        ["abilitykit.behaviortree.command.pause-observation.tooltip"] = "Freeze the display while runtime continues",
                        ["abilitykit.behaviortree.command.resume"] = "Resume",
                        ["abilitykit.behaviortree.command.copy-snapshot"] = "Copy Snapshot",
                        ["abilitykit.behaviortree.command.copy-snapshot.tooltip"] = "Copy the current runtime state as JSON",
                        ["abilitykit.behaviortree.command.save"] = "Save",
                        ["abilitykit.behaviortree.command.save.tooltip"] = "Save the authoring document (Ctrl+S)",
                        ["abilitykit.behaviortree.command.export"] = "Export",
                        ["abilitykit.behaviortree.command.export.tooltip"] = "Save and export runtime-only IR (Ctrl+Shift+E)",
                        ["abilitykit.behaviortree.command.undo"] = "Undo",
                        ["abilitykit.behaviortree.command.undo.tooltip"] = "Undo the previous change (Ctrl+Z)",
                        ["abilitykit.behaviortree.command.redo"] = "Redo",
                        ["abilitykit.behaviortree.command.redo.tooltip"] = "Redo the next change (Ctrl+Y)",
                        ["abilitykit.behaviortree.command.add-root"] = "Add Root",
                        ["abilitykit.behaviortree.command.add-root.tooltip"] = "Create a runnable root for an empty tree",
                        ["abilitykit.behaviortree.command.group"] = "Group",
                        ["abilitykit.behaviortree.command.group.tooltip"] = "Place selected nodes in a new group",
                        ["abilitykit.behaviortree.command.note"] = "Note",
                        ["abilitykit.behaviortree.command.note.tooltip"] = "Add a non-runtime note at the canvas center",
                        ["abilitykit.behaviortree.command.auto-layout"] = "Auto Layout",
                        ["abilitykit.behaviortree.command.auto-layout.tooltip"] = "Arrange nodes by parent-child depth (Ctrl+L)",
                        ["abilitykit.behaviortree.command.frame-all"] = "Frame All",
                        ["abilitykit.behaviortree.command.frame-all.tooltip"] = "Show all nodes",
                        ["abilitykit.behaviortree.command.validate"] = "Validate",
                        ["abilitykit.behaviortree.command.validate.tooltip"] = "Validate structure, properties, and blackboard references",
                        ["abilitykit.behaviortree.search.tooltip"] = "Find by display name, node ID, or type (Ctrl+F)",
                        ["abilitykit.behaviortree.state.dirty"] = "Unsaved",
                        ["abilitykit.behaviortree.state.saved"] = "Saved",
                        ["abilitykit.behaviortree.validation.success"] = "✔ Validation passed",
                        ["abilitykit.behaviortree.validation.errors"] = "✘ {0} errors",
                        ["abilitykit.behaviortree.validation.locate"] = "Locate node {0}",
                        ["abilitykit.behaviortree.observation.stopped"] = "Observation (instance stopped)",
                        ["abilitykit.behaviortree.observation.frame"] = "Observation  frame {0}",
                        ["abilitykit.behaviortree.observation.frame-frozen"] = "Observation  frame {0} (frozen)",
                        ["abilitykit.behaviortree.observation.snapshot-copied"] = "Runtime snapshot copied",
                        ["abilitykit.behaviortree.observation.snapshot-failed"] = "Snapshot copy failed"
                    },
                    ["zh-CN"] = new Dictionary<string, string>
                    {
                        ["abilitykit.behaviortree.mode.edit"] = "Behavior Tree",
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
                        ["abilitykit.behaviortree.command.save.tooltip"] = "保存授权文档 (Ctrl+S)",
                        ["abilitykit.behaviortree.command.export"] = "导出",
                        ["abilitykit.behaviortree.command.export.tooltip"] = "保存并导出纯运行时 IR (Ctrl+Shift+E)",
                        ["abilitykit.behaviortree.command.undo"] = "撤销",
                        ["abilitykit.behaviortree.command.undo.tooltip"] = "撤销上一步 (Ctrl+Z)",
                        ["abilitykit.behaviortree.command.redo"] = "重做",
                        ["abilitykit.behaviortree.command.redo.tooltip"] = "重做下一步 (Ctrl+Y)",
                        ["abilitykit.behaviortree.command.add-root"] = "添加根",
                        ["abilitykit.behaviortree.command.add-root.tooltip"] = "为空树创建可直接运行的根节点",
                        ["abilitykit.behaviortree.command.group"] = "分组",
                        ["abilitykit.behaviortree.command.group.tooltip"] = "将当前选中节点放入新分组",
                        ["abilitykit.behaviortree.command.note"] = "注释",
                        ["abilitykit.behaviortree.command.note.tooltip"] = "在画布中心添加不参与运行时导出的说明",
                        ["abilitykit.behaviortree.command.auto-layout"] = "自动布局",
                        ["abilitykit.behaviortree.command.auto-layout.tooltip"] = "按父子层级整理全部节点 (Ctrl+L)",
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
                        ["abilitykit.behaviortree.observation.frame"] = "观察模式  frame {0}",
                        ["abilitykit.behaviortree.observation.frame-frozen"] = "观察模式  frame {0}（已冻结）",
                        ["abilitykit.behaviortree.observation.snapshot-copied"] = "运行快照已复制",
                        ["abilitykit.behaviortree.observation.snapshot-failed"] = "快照复制失败"
                    }
                });
        }
    }

    public static class BtEditorCommandIds
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

    public static class BtEditorCommandFactory
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
                Command(BtEditorCommandIds.Close, close),
                Command(BtEditorCommandIds.PauseObservation, pauseObservation),
                Command(BtEditorCommandIds.CopySnapshot, copySnapshot),
                Command(BtEditorCommandIds.Save, save, Editable),
                Command(BtEditorCommandIds.Export, export, Editable),
                Command(BtEditorCommandIds.Undo, undo, context => Editable(context) && canUndo()),
                Command(BtEditorCommandIds.Redo, redo, context => Editable(context) && canRedo()),
                Command(BtEditorCommandIds.AddRoot, addRoot, Editable),
                Command(BtEditorCommandIds.Group, group, Editable),
                Command(BtEditorCommandIds.Note, note, Editable),
                Command(BtEditorCommandIds.AutoLayout, autoLayout, Editable),
                Command(BtEditorCommandIds.FrameAll, frameAll),
                Command(BtEditorCommandIds.Validate, validate, Editable)
            };
        }
    }
}
