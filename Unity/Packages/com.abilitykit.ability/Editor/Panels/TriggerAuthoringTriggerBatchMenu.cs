#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Panels
{
    internal static class TriggerAuthoringTriggerBatchMenu
    {
        public static void Show(
            TriggerAuthoringModuleAsset asset,
            List<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            string scopeLabel,
            Action<int> selectTrigger,
            Action changed,
            Action<string> notify)
        {
            var targets = indices != null ? new List<int>(indices) : new List<int>();
            var menu = new GenericMenu();
            if (targets.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可操作的触发器"));
                menu.ShowAsContext();
                return;
            }

            scopeLabel = string.IsNullOrWhiteSpace(scopeLabel) ? "目标项" : scopeLabel;
            var count = targets.Count;
            menu.AddItem(new GUIContent("定位首项"), false, () => selectTrigger?.Invoke(targets[0]));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("启用全部" + scopeLabel), false, () =>
                ConfirmAndApply(
                    asset,
                    "启用" + scopeLabel,
                    "确定启用 " + count + " 个" + scopeLabel + "吗？",
                    "启用",
                    () => TriggerAuthoringTriggerBatchOperations.SetEnabled(triggers, targets, true),
                    changed,
                    notify));
            menu.AddItem(new GUIContent("停用全部" + scopeLabel), false, () =>
                ConfirmAndApply(
                    asset,
                    "停用" + scopeLabel,
                    "确定停用 " + count + " 个" + scopeLabel + "吗？",
                    "停用",
                    () => TriggerAuthoringTriggerBatchOperations.SetEnabled(triggers, targets, false),
                    changed,
                    notify));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("业务分组/设置" + scopeLabel + "..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "设置" + scopeLabel + "的业务分组",
                    "业务分组路径",
                    GuessGroupPath(triggers, targets),
                    value => Apply(
                        asset,
                        "设置" + scopeLabel + "业务分组",
                        () => TriggerAuthoringTriggerBatchOperations.SetGroupPath(triggers, targets, value),
                        changed,
                        notify)));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("检索关键词/添加到" + scopeLabel + "..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "添加检索关键词",
                    "检索关键词（使用英文逗号分隔）",
                    string.Empty,
                    value => Apply(
                        asset,
                        "添加" + scopeLabel + "检索关键词",
                        () => TriggerAuthoringTriggerBatchOperations.AddTags(triggers, targets, value),
                        changed,
                        notify)));
            menu.AddItem(new GUIContent("检索关键词/从" + scopeLabel + "移除..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "移除检索关键词",
                    "检索关键词（使用英文逗号分隔）",
                    string.Empty,
                    value => Apply(
                        asset,
                        "移除" + scopeLabel + "检索关键词",
                        () => TriggerAuthoringTriggerBatchOperations.RemoveTags(triggers, targets, value),
                        changed,
                        notify)));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("复制" + scopeLabel + " TriggerId"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer =
                    TriggerAuthoringTriggerBatchOperations.BuildTriggerIdList(triggers, targets);
                notify?.Invoke("已复制 " + count + " 个 TriggerId");
            });
            menu.ShowAsContext();
        }

        private static void ConfirmAndApply(
            TriggerAuthoringModuleAsset asset,
            string title,
            string message,
            string confirm,
            Func<int> operation,
            Action changed,
            Action<string> notify)
        {
            if (!EditorUtility.DisplayDialog(title, message, confirm, "取消")) return;
            Apply(asset, title, operation, changed, notify);
        }

        private static void Apply(
            TriggerAuthoringModuleAsset asset,
            string undoName,
            Func<int> operation,
            Action changed,
            Action<string> notify)
        {
            if (asset == null || operation == null) return;
            Undo.RecordObject(asset, undoName);
            var changedCount = operation();
            if (changedCount <= 0)
            {
                notify?.Invoke("没有需要修改的触发器");
                return;
            }

            EditorUtility.SetDirty(asset);
            changed?.Invoke();
            notify?.Invoke("已修改 " + changedCount + " 个触发器");
        }

        private static string GuessGroupPath(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices)
        {
            if (triggers == null || indices == null) return string.Empty;
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index < 0 || index >= triggers.Count) continue;
                var path = triggers[index]?.GroupPath;
                if (!string.IsNullOrWhiteSpace(path)) return path;
            }
            return string.Empty;
        }
    }
}
#endif
