#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Config.Source;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Triggering.Runtime;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Source JSON 导出/导入编辑器菜单
    /// </summary>
    internal static class SourceJsonExporter
    {
        private const string SourceOutputFileName = "ability_trigger_plans_source.json";
        private const string PlanOutputFileName = "ability_trigger_plans.json";

        [MenuItem("AbilityKit/Ability/Source/Export Source JSON")]
        public static void ExportSourceJson()
        {
            var sourcePath = EditorUtility.OpenFilePanel(
                "选择 Source JSON 文件",
                Application.dataPath,
                "json");

            if (string.IsNullOrEmpty(sourcePath))
            {
                ExportLog.Info("Export cancelled");
                return;
            }

            ExportSourceToPlan(sourcePath);
        }

        [MenuItem("AbilityKit/Ability/Source/Import Plan JSON to Source")]
        public static void ImportPlanToSource()
        {
            var planPath = EditorUtility.OpenFilePanel(
                "选择 Plan JSON 文件",
                Application.dataPath,
                "json");

            if (string.IsNullOrEmpty(planPath))
            {
                ExportLog.Info("Import cancelled");
                return;
            }

            ImportPlanToSourceFile(planPath);
        }

        [MenuItem("AbilityKit/Ability/Source/Export from SO to Source JSON")]
        public static void ExportSoToSource()
        {
            ExportLog.Info("Exporting SO configs to Source JSON...");

            try
            {
                var source = new TriggerSourceConfig
                {
                    Schema = "abilitykit-trigger-source-v1",
                    Version = "1.0",
                    Metadata = new SourceMetadata
                    {
                        Author = "team",
                        Description = "从编辑器 SO 配置导出的源配置"
                    },
                    Variables = new System.Collections.Generic.List<SourceVariable>
                    {
                        new SourceVariable("$caster", "技能释放者"),
                        new SourceVariable("$target", "目标实体"),
                        new SourceVariable("$self", "触发者自身")
                    },
                    Actions = GenerateActionDefinitions(),
                    Conditions = GenerateConditionDefinitions(),
                    Triggers = new System.Collections.Generic.List<SourceTriggerConfig>()
                };

                // 从 SO 收集触发器
                var guids = AbilityTriggerJsonExporter.FindAbilityModuleGuids("Assets");
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                    if (asset?.Triggers == null) continue;

                    foreach (var tr in asset.Triggers)
                    {
                        if (tr == null || !tr.Enabled || tr.TriggerId <= 0) continue;

                        var sourceTrigger = ConvertTriggerToSource(tr);
                        if (sourceTrigger != null)
                        {
                            source.Triggers.Add(sourceTrigger);
                        }
                    }
                }

                // 序列化并保存
                var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
                Directory.CreateDirectory(outputDir);
                var outputPath = Path.Combine(outputDir, SourceOutputFileName);

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(source, settings);
                File.WriteAllText(outputPath, json);

                AssetDatabase.Refresh();
                ExportLog.Info($"Source JSON exported to: {outputPath}");
                EditorUtility.DisplayDialog("Export Complete",
                    $"Source JSON exported successfully!\n\nFile: {outputPath}\nTriggers: {source.Triggers.Count}",
                    "OK");
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, "Failed to export Source JSON");
                EditorUtility.DisplayDialog("Export Failed",
                    $"Error: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// 导出 Source JSON 到 Plan JSON
        /// </summary>
        public static void ExportSourceToPlan(string sourcePath)
        {
            ExportLog.Info($"Exporting Source to Plan: {sourcePath}");

            try
            {
                var json = File.ReadAllText(sourcePath);
                var result = SourceToPlanConverter.Convert(json);

                if (!result.Success)
                {
                    foreach (var err in result.Errors)
                    {
                        ExportLog.Error($"Source conversion error: {err}");
                    }
                    EditorUtility.DisplayDialog("Conversion Failed",
                        $"Errors:\n{string.Join("\n", result.Errors)}",
                        "OK");
                    return;
                }

                // 输出警告
                foreach (var warn in result.Warnings)
                {
                    ExportLog.Warning($"Source conversion warning: {warn}");
                }

                // 序列化 Plan JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var planJson = JsonConvert.SerializeObject(result.PlanDatabase, settings);

                // 保存 Plan JSON
                var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
                Directory.CreateDirectory(outputDir);
                var outputPath = Path.Combine(outputDir, PlanOutputFileName);

                File.WriteAllText(outputPath, planJson);
                AssetDatabase.Refresh();

                ExportLog.Info($"Plan JSON exported to: {outputPath}");
                ExportLog.Info($"Converted {result.PlanDatabase.Triggers.Count} triggers");

                EditorUtility.DisplayDialog("Export Complete",
                    $"Conversion successful!\n\nPlan JSON: {outputPath}\nTriggers: {result.PlanDatabase.Triggers.Count}\nWarnings: {result.Warnings.Count}",
                    "OK");

                // 显示警告对话框
                if (result.Warnings.Count > 0)
                {
                    var warningMsg = string.Join("\n", result.Warnings.GetRange(0, Math.Min(5, result.Warnings.Count)));
                    if (result.Warnings.Count > 5)
                    {
                        warningMsg += $"\n... and {result.Warnings.Count - 5} more";
                    }
                    EditorUtility.DisplayDialog("Warnings",
                        $"Warnings:\n{warningMsg}",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, "Failed to export Source to Plan");
                EditorUtility.DisplayDialog("Export Failed",
                    $"Error: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// 导入 Plan JSON 到 Source JSON
        /// </summary>
        public static void ImportPlanToSourceFile(string planPath)
        {
            ExportLog.Info($"Importing Plan to Source: {planPath}");

            try
            {
                var json = File.ReadAllText(planPath);
                var result = PlanToSourceConverter.Convert(json);

                if (!result.Success)
                {
                    foreach (var err in result.Errors)
                    {
                        ExportLog.Error($"Plan conversion error: {err}");
                    }
                    EditorUtility.DisplayDialog("Conversion Failed",
                        $"Errors:\n{string.Join("\n", result.Errors)}",
                        "OK");
                    return;
                }

                // 输出警告
                foreach (var warn in result.Warnings)
                {
                    ExportLog.Warning($"Plan conversion warning: {warn}");
                }

                // 序列化 Source JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var sourceJson = JsonConvert.SerializeObject(result.Source, settings);

                // 保存 Source JSON
                var outputDir = Path.GetDirectoryName(planPath);
                var sourceFileName = Path.GetFileNameWithoutExtension(planPath) + "_source.json";
                var outputPath = Path.Combine(outputDir ?? Application.dataPath, sourceFileName);

                File.WriteAllText(outputPath, sourceJson);
                AssetDatabase.Refresh();

                ExportLog.Info($"Source JSON exported to: {outputPath}");
                ExportLog.Info($"Converted {result.Source.Triggers.Count} triggers");

                EditorUtility.DisplayDialog("Import Complete",
                    $"Conversion successful!\n\nSource JSON: {outputPath}\nTriggers: {result.Source.Triggers.Count}",
                    "OK");
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, "Failed to import Plan to Source");
                EditorUtility.DisplayDialog("Import Failed",
                    $"Error: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// 从 TriggerEditorConfig 转换到 SourceTriggerConfig
        /// </summary>
        private static SourceTriggerConfig ConvertTriggerToSource(TriggerEditorConfig tr)
        {
            if (tr == null) return null;

            var source = new SourceTriggerConfig
            {
                Id = tr.TriggerId,
                Event = tr.EventId,
                Phase = "immediate",
                Enabled = tr.Enabled,
                AllowExternal = false,
                Conditions = new System.Collections.Generic.List<SourceConditionConfig>(),
                Actions = new System.Collections.Generic.List<SourceActionConfig>()
            };

            // 转换条件
            if (tr.ConditionsStrong != null)
            {
                foreach (var cond in tr.ConditionsStrong)
                {
                    var sourceCond = ConvertConditionToSource(cond);
                    if (sourceCond != null)
                    {
                        source.Conditions.Add(sourceCond);
                    }
                }
            }

            // 转换动作
            if (tr.ActionsStrong != null)
            {
                foreach (var act in tr.ActionsStrong)
                {
                    var sourceAct = ConvertActionToSource(act);
                    if (sourceAct != null)
                    {
                        source.Actions.Add(sourceAct);
                    }
                }
            }

            return source;
        }

        private static SourceConditionConfig ConvertConditionToSource(ConditionEditorConfigBase cond)
        {
            if (cond == null) return null;

            var source = new SourceConditionConfig
            {
                Type = cond.Type,
                Args = new System.Collections.Generic.Dictionary<string, object>()
            };

            // 从配置提取参数（需要具体实现）
            // 这里简化处理
            return source;
        }

        private static SourceActionConfig ConvertActionToSource(ActionEditorConfigBase act)
        {
            if (act == null) return null;

            var source = new SourceActionConfig
            {
                Type = act.Type,
                Args = new System.Collections.Generic.Dictionary<string, object>()
            };

            // 从配置提取参数（需要具体实现）
            // 这里简化处理
            return source;
        }

        /// <summary>
        /// 生成动作类型定义
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, ActionTypeDefinition> GenerateActionDefinitions()
        {
            var definitions = new System.Collections.Generic.Dictionary<string, ActionTypeDefinition>();

            definitions["debug_log"] = new ActionTypeDefinition
            {
                Type = "debug_log",
                DisplayName = "调试日志",
                Description = "输出调试信息到控制台",
                Category = "调试",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("message", "string", true),
                    new ParameterDefinition("dump_args", "bool", false) { DefaultValue = false }
                }
            };

            definitions["shoot_projectile"] = new ActionTypeDefinition
            {
                Type = "shoot_projectile",
                DisplayName = "发射弹道",
                Description = "从发射者向目标发射弹道",
                Category = "战斗",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("launcher", "entity", true),
                    new ParameterDefinition("target", "entity", true),
                    new ParameterDefinition("projectile_id", "int", true),
                    new ParameterDefinition("speed", "float", false) { DefaultValue = 300.0 }
                }
            };

            definitions["give_damage"] = new ActionTypeDefinition
            {
                Type = "give_damage",
                DisplayName = "造成伤害",
                Description = "对目标造成伤害",
                Category = "战斗",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("from", "entity", true),
                    new ParameterDefinition("to", "entity", true),
                    new ParameterDefinition("amount", "expr", true),
                    new ParameterDefinition("reason", "int", false) { DefaultValue = 0 }
                }
            };

            definitions["add_buff"] = new ActionTypeDefinition
            {
                Type = "add_buff",
                DisplayName = "添加Buff",
                Description = "为目标添加Buff效果",
                Category = "Buff",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("target", "entity", true),
                    new ParameterDefinition("buff_id", "int", true),
                    new ParameterDefinition("duration", "float", false) { DefaultValue = -1.0 }
                }
            };

            definitions["seq"] = new ActionTypeDefinition
            {
                Type = "seq",
                DisplayName = "顺序执行",
                Description = "按顺序执行多个动作",
                Category = "流程",
                IsComposite = true,
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("items", "action[]", true)
                }
            };

            return definitions;
        }

        /// <summary>
        /// 生成条件类型定义
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, ConditionTypeDefinition> GenerateConditionDefinitions()
        {
            var definitions = new System.Collections.Generic.Dictionary<string, ConditionTypeDefinition>();

            definitions["all"] = new ConditionTypeDefinition
            {
                Type = "all",
                DisplayName = "全部满足",
                Description = "所有子条件都必须满足",
                Category = "复合",
                IsComposite = true,
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("items", "condition[]", true)
                }
            };

            definitions["any"] = new ConditionTypeDefinition
            {
                Type = "any",
                DisplayName = "任一满足",
                Description = "任一子条件满足即可",
                Category = "复合",
                IsComposite = true,
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("items", "condition[]", true)
                }
            };

            definitions["not"] = new ConditionTypeDefinition
            {
                Type = "not",
                DisplayName = "取反",
                Description = "对子条件取反",
                Category = "复合",
                IsComposite = true,
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("item", "condition", true)
                }
            };

            definitions["arg_eq"] = new ConditionTypeDefinition
            {
                Type = "arg_eq",
                DisplayName = "参数等于",
                Description = "检查参数值是否等于指定值",
                Category = "参数",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("arg_name", "string", true),
                    new ParameterDefinition("value", "number", true)
                }
            };

            definitions["arg_gt"] = new ConditionTypeDefinition
            {
                Type = "arg_gt",
                DisplayName = "参数大于",
                Description = "检查参数值是否大于指定值",
                Category = "参数",
                Params = new System.Collections.Generic.List<ParameterDefinition>
                {
                    new ParameterDefinition("arg_name", "string", true),
                    new ParameterDefinition("value", "number", true)
                }
            };

            return definitions;
        }
    }
}
#endif
