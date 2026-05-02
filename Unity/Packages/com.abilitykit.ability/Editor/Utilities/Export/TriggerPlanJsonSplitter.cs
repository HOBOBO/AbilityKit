#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Editor.Utilities;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Trigger Plan JSON 拆分/合并工具
    /// 
    /// 功能：
    /// 1. 拆分：将单个 ability_trigger_plans.json 拆分为多个文件
    /// 2. 合并：将多个文件合并回单个文件
    /// 3. 导入：支持从拆分目录导入
    /// 4. 导出：支持导出到拆分目录
    /// </summary>
    internal static class TriggerPlanJsonSplitter
    {
        private const string TriggersDirName = "triggers";
        private const string SingleFileName = "ability_trigger_plans.json";
        private const string ReadableSingleFileName = "ability_trigger_plans_readable.json";

        /// <summary>
        /// 将单个 JSON 文件拆分为多个文件
        /// </summary>
        [MenuItem("AbilityKit/Ability/Split Trigger Plan JSON")]
        public static void Split()
        {
            var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
            var inputPath = Path.Combine(outputDir, SingleFileName);

            if (!File.Exists(inputPath))
            {
                Debug.LogError($"[TriggerPlanSplitter] 文件不存在: {inputPath}");
                return;
            }

            var triggersDir = Path.Combine(outputDir, TriggersDirName);
            Directory.CreateDirectory(triggersDir);

            try
            {
                var json = File.ReadAllText(inputPath);
                var dto = JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(json);

                if (dto == null)
                {
                    Debug.LogError("[TriggerPlanSplitter] 解析 JSON 失败");
                    return;
                }

                int count = 0;
                foreach (var trigger in dto.Triggers)
                {
                    var fileName = $"{trigger.TriggerId}.json";
                    var filePath = Path.Combine(triggersDir, fileName);

                    var triggerJson = JsonConvert.SerializeObject(trigger, Formatting.Indented);
                    File.WriteAllText(filePath, triggerJson);
                    count++;
                }

                Debug.Log($"[TriggerPlanSplitter] 已拆分 {count} 个 Trigger 到:\n{triggersDir}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TriggerPlanSplitter] 拆分失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将多个文件合并为单个 JSON 文件
        /// </summary>
        [MenuItem("AbilityKit/Ability/Merge Trigger Plan JSON")]
        public static void Merge()
        {
            var inputDir = Path.Combine(Application.dataPath, "Resources", "ability", TriggersDirName);

            if (!Directory.Exists(inputDir))
            {
                Debug.LogError($"[TriggerPlanSplitter] 目录不存在: {inputDir}");
                return;
            }

            var outputPath = Path.Combine(Path.GetDirectoryName(inputDir), SingleFileName);

            try
            {
                var jsonFiles = Directory.GetFiles(inputDir, "*.json")
                    .OrderBy(f => f)
                    .ToList();

                if (jsonFiles.Count == 0)
                {
                    Debug.LogError($"[TriggerPlanSplitter] 没有找到 JSON 文件");
                    return;
                }

                var triggers = new List<TriggerPlanDto>();
                var allStrings = new Dictionary<int, string>();

                foreach (var file in jsonFiles)
                {
                    var json = File.ReadAllText(file);
                    var trigger = JsonConvert.DeserializeObject<TriggerPlanDto>(json);
                    if (trigger != null)
                    {
                        triggers.Add(trigger);
                    }
                }

                var dto = new TriggerPlanDatabaseDto();
                dto.Triggers.Clear();
                dto.Triggers.AddRange(triggers);

                // 合并字符串表
                var mergedJson = JsonConvert.SerializeObject(dto, Formatting.Indented);
                File.WriteAllText(outputPath, mergedJson);

                Debug.Log($"[TriggerPlanSplitter] 已合并 {triggers.Count} 个 Trigger 到:\n{outputPath}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TriggerPlanSplitter] 合并失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出可读格式到拆分目录
        /// </summary>
        [MenuItem("AbilityKit/Ability/Export Readable Split")]
        public static void ExportReadableSplit()
        {
            var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
            var inputPath = Path.Combine(outputDir, SingleFileName);

            if (!File.Exists(inputPath))
            {
                Debug.LogError($"[TriggerPlanSplitter] 文件不存在: {inputPath}");
                return;
            }

            var triggersDir = Path.Combine(outputDir, TriggersDirName + "_readable");
            Directory.CreateDirectory(triggersDir);

            try
            {
                var json = File.ReadAllText(inputPath);
                var dto = JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(json);

                if (dto == null)
                {
                    Debug.LogError("[TriggerPlanSplitter] 解析 JSON 失败");
                    return;
                }

                // 转换所有触发器为可读格式
                var readable = new ReadableTriggerPlanDatabase
                {
                    ActionDefs = ReadableTriggerPlanConverter.CollectActionDefs(dto),
                    Strings = dto.Strings.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value
                    ),
                    Triggers = dto.Triggers.Select(ToReadableTrigger).ToList()
                };

                int count = 0;
                foreach (var trigger in readable.Triggers)
                {
                    var fileName = $"{trigger.TriggerId}.json";
                    var filePath = Path.Combine(triggersDir, fileName);

                    // 创建单个可读触发器的数据结构
                    var singleReadable = new ReadableTriggerPlanDatabase
                    {
                        ActionDefs = readable.ActionDefs,
                        Strings = readable.Strings,
                        Triggers = new List<ReadableTriggerPlan> { trigger }
                    };

                    var triggerJson = JsonConvert.SerializeObject(singleReadable, Formatting.Indented);
                    File.WriteAllText(filePath, triggerJson);
                    count++;
                }

                Debug.Log($"[TriggerPlanSplitter] 已导出可读格式 {count} 个 Trigger 到:\n{triggersDir}");
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(triggersDir);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TriggerPlanSplitter] 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从拆分目录导入可读格式
        /// </summary>
        [MenuItem("AbilityKit/Ability/Import Readable Split")]
        public static void ImportReadableSplit()
        {
            var inputDir = Path.Combine(Application.dataPath, "Resources", "ability", TriggersDirName + "_readable");

            if (!Directory.Exists(inputDir))
            {
                Debug.LogError($"[TriggerPlanSplitter] 目录不存在: {inputDir}");
                return;
            }

            var outputPath = Path.Combine(Path.GetDirectoryName(inputDir), ReadableSingleFileName);

            try
            {
                var jsonFiles = Directory.GetFiles(inputDir, "*.json")
                    .OrderBy(f => f)
                    .ToList();

                if (jsonFiles.Count == 0)
                {
                    Debug.LogError($"[TriggerPlanSplitter] 没有找到 JSON 文件");
                    return;
                }

                // 收集所有 ActionDefs 和 Strings
                var allActionDefs = new Dictionary<string, ReadableActionDef>();
                var allStrings = new Dictionary<string, string>();
                var allTriggers = new List<ReadableTriggerPlan>();

                foreach (var file in jsonFiles)
                {
                    var json = File.ReadAllText(file);
                    var readable = JsonConvert.DeserializeObject<ReadableTriggerPlanDatabase>(json);

                    if (readable != null)
                    {
                        // 合并 ActionDefs
                        if (readable.ActionDefs != null)
                        {
                            foreach (var kvp in readable.ActionDefs)
                            {
                                if (!allActionDefs.ContainsKey(kvp.Key))
                                {
                                    allActionDefs[kvp.Key] = kvp.Value;
                                }
                            }
                        }

                        // 合并 Strings
                        if (readable.Strings != null)
                        {
                            foreach (var kvp in readable.Strings)
                            {
                                if (!allStrings.ContainsKey(kvp.Key))
                                {
                                    allStrings[kvp.Key] = kvp.Value;
                                }
                            }
                        }

                        // 收集 Triggers
                        if (readable.Triggers != null)
                        {
                            allTriggers.AddRange(readable.Triggers);
                        }
                    }
                }

                var merged = new ReadableTriggerPlanDatabase
                {
                    ActionDefs = allActionDefs,
                    Strings = allStrings,
                    Triggers = allTriggers
                };

                var mergedJson = JsonConvert.SerializeObject(merged, Formatting.Indented);
                File.WriteAllText(outputPath, mergedJson);

                Debug.Log($"[TriggerPlanSplitter] 已导入 {allTriggers.Count} 个 Trigger 到:\n{outputPath}");
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TriggerPlanSplitter] 导入失败: {ex.Message}");
            }
        }

        private static ReadableTriggerPlan ToReadableTrigger(TriggerPlanDto trigger)
        {
            return new ReadableTriggerPlan
            {
                TriggerId = trigger.TriggerId,
                Event = trigger.EventName ?? "",
                AllowExternal = trigger.AllowExternal,
                Phase = trigger.Phase,
                Priority = trigger.Priority,
                Predicate = ToReadablePredicate(trigger.Predicate),
                Actions = trigger.Actions?.Select(ToReadableAction).ToList() ?? new List<ReadableActionCall>()
            };
        }

        private static ReadablePredicate ToReadablePredicate(PredicatePlanDto predicate)
        {
            if (predicate == null || predicate.Kind == "none")
                return ReadablePredicate.None();

            if (predicate.Kind == "expr" && predicate.Nodes != null)
            {
                var nodes = predicate.Nodes.Select(ToReadableBoolExprNode).ToList();
                return ReadablePredicate.Expr(nodes);
            }

            return new ReadablePredicate { Kind = predicate.Kind ?? "none" };
        }

        private static ReadableBoolExprNode ToReadableBoolExprNode(BoolExprNodeDto node)
        {
            return new ReadableBoolExprNode
            {
                Kind = node.Kind ?? "Compare",
                ConstValue = node.ConstValue,
                CompareOp = node.CompareOp,
                Left = ToReadableValueRef(node.Left),
                Right = ToReadableValueRef(node.Right)
            };
        }

        private static ReadableValueRef ToReadableValueRef(NumericValueRefDto dto)
        {
            if (dto == null) return ReadableValueRef.Const(0);

            return new ReadableValueRef
            {
                Kind = dto.Kind ?? "Const",
                ConstValue = dto.ConstValue,
                BoardId = dto.BoardId,
                KeyId = dto.KeyId,
                FieldId = dto.FieldId,
                DomainId = dto.DomainId,
                Key = dto.Key,
                ExprText = dto.ExprText
            };
        }

        private static ReadableActionCall ToReadableAction(ActionCallPlanDto action)
        {
            var args = new Dictionary<string, object>();

            if (action.Args != null && action.Args.Count > 0)
            {
                foreach (var kvp in action.Args)
                {
                    args[kvp.Key] = ValueRefToObject(kvp.Value);
                }
            }

            return new ReadableActionCall
            {
                Action = $"action_{action.ActionId}",
                Args = args,
                Children = action.Children?.Select(ToReadableAction).ToList()
            };
        }

        private static object ValueRefToObject(NumericValueRefDto dto)
        {
            if (dto == null) return 0;

            switch (dto.Kind)
            {
                case "Const":
                    if (dto.ConstValue == Math.Floor(dto.ConstValue))
                        return (int)dto.ConstValue;
                    return dto.ConstValue;
                case "Board":
                    return new { Kind = "Board", BoardId = dto.BoardId, KeyId = dto.KeyId };
                case "Field":
                    return new { Kind = "Field", FieldId = dto.FieldId, KeyId = dto.KeyId };
                case "Domain":
                    return new { Kind = "Domain", DomainId = dto.DomainId, Key = dto.Key };
                case "Expr":
                    return new { Kind = "Expr", ExprText = dto.ExprText };
                default:
                    return dto.ConstValue;
            }
        }
    }
}
#endif
