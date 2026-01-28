#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerJsonExporter
    {
        private const string OutputResourcesDir = "ability";
        private const string OutputFileWithoutExt = "ability_triggers";
        private const string OutputPlanFileWithoutExt = "ability_trigger_plans";
        private const string DefaultAbilityConfigFolder = "Assets/Configs/Ability";

        [MenuItem("AbilityKit/Ability/Export Trigger Json")]
        public static void ExportSelectedFolder()
        {
            var folder = TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Plan Json")]
        public static void ExportSelectedFolderPlans()
        {
            var folder = TryGetSelectedFolderPath();
            ExportPlanFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Json (Configs/Ability)")]
        public static void ExportDefaultFolder()
        {
            ExportFromFolder(DefaultAbilityConfigFolder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Plan Json (Configs/Ability)")]
        public static void ExportDefaultFolderPlans()
        {
            ExportPlanFromFolder(DefaultAbilityConfigFolder);
        }

        public static void ExportFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            Debug.Log($"[AbilityTriggerJsonExporter] ExportFromFolder: {assetFolder}");

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var dto = BuildDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                Debug.Log($"[AbilityTriggerJsonExporter] No triggers exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = BuildDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
            }

            Debug.Log($"[AbilityTriggerJsonExporter] Modules={moduleCount}, ExportedTriggers={exportedTriggerCount}, SkippedDisabled={skippedDisabledCount}, SkippedTriggerId<=0={skippedInvalidIdCount}");

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[AbilityTriggerJsonExporter] Exported to: {outputPath}");
        }

        public static void ExportPlanFromFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            Debug.Log($"[AbilityTriggerJsonExporter] ExportPlanFromFolder: {assetFolder}");

            var outputDir = Path.Combine(Application.dataPath, "Resources", OutputResourcesDir);
            Directory.CreateDirectory(outputDir);

            var dto = BuildPlanDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                Debug.Log($"[AbilityTriggerJsonExporter] No trigger plans exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = BuildPlanDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
            }

            Debug.Log($"[AbilityTriggerJsonExporter] Plan Modules={moduleCount}, ExportedTriggers={exportedTriggerCount}, SkippedDisabled={skippedDisabledCount}, SkippedTriggerId<=0={skippedInvalidIdCount}");

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            var outputPath = Path.Combine(outputDir, OutputPlanFileWithoutExt + ".json");
            File.WriteAllText(outputPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[AbilityTriggerJsonExporter] Exported plans to: {outputPath}");
        }

        private static AbilityTriggerDatabaseDTO BuildDto(
            string assetFolder,
            out int moduleCount,
            out int exportedTriggerCount,
            out int skippedDisabledCount,
            out int skippedInvalidIdCount)
        {
            var db = new AbilityTriggerDatabaseDTO();
            moduleCount = 0;
            exportedTriggerCount = 0;
            skippedDisabledCount = 0;
            skippedInvalidIdCount = 0;

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { assetFolder });
            if (guids == null || guids.Length == 0) return db;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                if (asset == null) continue;

                moduleCount++;

                if (asset.Triggers != null)
                {
                    for (int t = 0; t < asset.Triggers.Count; t++)
                    {
                        var tr = asset.Triggers[t];
                        if (tr == null) continue;
                        if (!tr.Enabled)
                        {
                            skippedDisabledCount++;
                            continue;
                        }

                        if (tr.TriggerId <= 0)
                        {
                            skippedInvalidIdCount++;
                            continue;
                        }

                        var triggerDto = new TriggerDTO
                        {
                            TriggerId = tr.TriggerId,
                            EventId = tr.EventId,
                            AllowExternal = tr.AllowExternal,
                            InitialLocalVars = BuildInitialLocalVars(tr),
                            Conditions = BuildConditions(tr),
                            Actions = BuildActions(tr)
                        };
                        db.Triggers.Add(triggerDto);
                        exportedTriggerCount++;
                    }
                }
            }

            return db;
        }

        [Serializable]
        private sealed class TriggerPlanDatabaseDto
        {
            public readonly List<TriggerPlanDto> Triggers = new List<TriggerPlanDto>();
        }

        [Serializable]
        private sealed class TriggerPlanDto
        {
            public int TriggerId;
            public int EventId;
            public bool AllowExternal;
            public int Phase;
            public int Priority;
            public PredicatePlanDto Predicate;
            public List<ActionCallPlanDto> Actions;
            public LegacyPredicateDto LegacyPredicate;
            public List<LegacyActionDto> LegacyActions;
        }

        [Serializable]
        private sealed class LegacyPredicateDto
        {
            public string Type;
            public Dictionary<string, object> Args;
        }

        [Serializable]
        private sealed class LegacyActionDto
        {
            public string Type;
            public Dictionary<string, object> Args;
        }

        [Serializable]
        private sealed class PredicatePlanDto
        {
            public string Kind;
            public List<BoolExprNodeDto> Nodes;
        }

        [Serializable]
        private sealed class BoolExprNodeDto
        {
            public string Kind;
            public bool ConstValue;
            public string CompareOp;
            public IntValueRefDto Left;
            public IntValueRefDto Right;
        }

        [Serializable]
        private sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public IntValueRefDto Arg0;
            public IntValueRefDto Arg1;
        }

        [Serializable]
        private sealed class IntValueRefDto
        {
            public string Kind;
            public int ConstValue;
            public int BoardId;
            public int KeyId;
            public int FieldId;
        }

        private static TriggerPlanDatabaseDto BuildPlanDto(
            string assetFolder,
            out int moduleCount,
            out int exportedTriggerCount,
            out int skippedDisabledCount,
            out int skippedInvalidIdCount)
        {
            var db = new TriggerPlanDatabaseDto();
            moduleCount = 0;
            exportedTriggerCount = 0;
            skippedDisabledCount = 0;
            skippedInvalidIdCount = 0;

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { assetFolder });
            if (guids == null || guids.Length == 0) return db;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(path);
                if (asset == null) continue;

                moduleCount++;

                if (asset.Triggers == null) continue;

                for (int t = 0; t < asset.Triggers.Count; t++)
                {
                    var tr = asset.Triggers[t];
                    if (tr == null) continue;
                    if (!tr.Enabled)
                    {
                        skippedDisabledCount++;
                        continue;
                    }

                    if (tr.TriggerId <= 0)
                    {
                        skippedInvalidIdCount++;
                        continue;
                    }

                    var plan = CompilePlanFromEditor(tr, out var phase, out var priority);
                    var triggerDto = BuildTriggerPlanDto(tr, plan, phase, priority);
                    db.Triggers.Add(triggerDto);
                    exportedTriggerCount++;
                }
            }

            return db;
        }

        private static TriggerPlan<object> CompilePlanFromEditor(TriggerEditorConfig tr, out int phase, out int priority)
        {
            phase = 0;
            priority = 0;

            JsonConditionEditorConfig cond = null;
            if (tr.ConditionsStrong != null && tr.ConditionsStrong.Count > 0)
            {
                if (tr.ConditionsStrong.Count == 1)
                {
                    cond = tr.ConditionsStrong[0] as JsonConditionEditorConfig;
                }
                else
                {
                    var all = new JsonConditionEditorConfig { TypeValue = TriggerConditionTypes.All, Items = new List<ConditionEditorConfigBase>(tr.ConditionsStrong) };
                    cond = all;
                }
            }

            JsonActionEditorConfig act = null;
            if (tr.ActionsStrong != null && tr.ActionsStrong.Count > 0)
            {
                if (tr.ActionsStrong.Count == 1)
                {
                    act = tr.ActionsStrong[0] as JsonActionEditorConfig;
                }
                else
                {
                    var seq = new JsonActionEditorConfig { TypeValue = TriggerActionTypes.Seq, Items = new List<ActionEditorConfigBase>(tr.ActionsStrong) };
                    act = seq;
                }
            }

            return TriggerPlanCompilerFromStrong.Compile<object>(phase, priority, cond, act);
        }

        private static TriggerPlanDto BuildTriggerPlanDto(TriggerEditorConfig tr, in TriggerPlan<object> plan, int phase, int priority)
        {
            var eventName = tr.EventId;
            var eventId = !string.IsNullOrEmpty(eventName) ? StableStringId.Get("event:" + eventName) : 0;

            var dto = new TriggerPlanDto
            {
                TriggerId = tr.TriggerId,
                EventId = eventId,
                AllowExternal = tr.AllowExternal,
                Phase = phase,
                Priority = priority,
                Predicate = BuildPredicateDto(in plan),
                Actions = BuildActionsDto(in plan),
                LegacyPredicate = BuildLegacyPredicateDto(in plan),
                LegacyActions = BuildLegacyActionsDto(in plan)
            };
            return dto;
        }

        private static LegacyPredicateDto BuildLegacyPredicateDto(in TriggerPlan<object> plan)
        {
            if (!plan.HasPredicate || plan.PredicateKind != EPredicateKind.Legacy) return null;
            if (!plan.LegacyPredicate.HasValue) return null;
            var p = plan.LegacyPredicate.Value;
            return new LegacyPredicateDto { Type = p.Type, Args = p.Args != null ? new Dictionary<string, object>(p.Args, StringComparer.Ordinal) : null };
        }

        private static List<LegacyActionDto> BuildLegacyActionsDto(in TriggerPlan<object> plan)
        {
            if (plan.LegacyActions == null || plan.LegacyActions.Length == 0) return null;
            var list = new List<LegacyActionDto>(plan.LegacyActions.Length);
            for (int i = 0; i < plan.LegacyActions.Length; i++)
            {
                var a = plan.LegacyActions[i];
                if (string.IsNullOrEmpty(a.Type)) continue;
                list.Add(new LegacyActionDto { Type = a.Type, Args = a.Args != null ? new Dictionary<string, object>(a.Args, StringComparer.Ordinal) : null });
            }
            return list.Count > 0 ? list : null;
        }

        private static PredicatePlanDto BuildPredicateDto(in TriggerPlan<object> plan)
        {
            if (!plan.HasPredicate || plan.PredicateKind == EPredicateKind.None)
            {
                return new PredicatePlanDto { Kind = "none", Nodes = null };
            }

            if (plan.PredicateKind == EPredicateKind.Legacy)
            {
                return new PredicatePlanDto { Kind = "legacy", Nodes = null };
            }

            if (plan.PredicateKind == EPredicateKind.Expr)
            {
                var nodes = plan.PredicateExpr.Nodes;
                var list = nodes != null ? new List<BoolExprNodeDto>(nodes.Length) : null;
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var n = nodes[i];
                        list.Add(new BoolExprNodeDto
                        {
                            Kind = n.Kind.ToString(),
                            ConstValue = n.ConstValue,
                            CompareOp = n.CompareOp.ToString(),
                            Left = BuildIntValueRefDto(in n.Left),
                            Right = BuildIntValueRefDto(in n.Right)
                        });
                    }
                }

                return new PredicatePlanDto { Kind = "expr", Nodes = list };
            }

            return new PredicatePlanDto { Kind = "function", Nodes = null };
        }

        private static List<ActionCallPlanDto> BuildActionsDto(in TriggerPlan<object> plan)
        {
            var actions = plan.Actions;
            if (actions == null || actions.Length == 0) return null;

            var list = new List<ActionCallPlanDto>(actions.Length);
            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                list.Add(new ActionCallPlanDto
                {
                    ActionId = a.Id.Value,
                    Arity = a.Arity,
                    Arg0 = BuildIntValueRefDto(in a.Arg0),
                    Arg1 = BuildIntValueRefDto(in a.Arg1)
                });
            }
            return list;
        }

        private static IntValueRefDto BuildIntValueRefDto(in IntValueRef r)
        {
            return new IntValueRefDto
            {
                Kind = r.Kind.ToString(),
                ConstValue = r.ConstValue,
                BoardId = r.BoardId,
                KeyId = r.KeyId,
                FieldId = r.FieldId
            };
        }

        private static Dictionary<string, object> BuildInitialLocalVars(TriggerEditorConfig editor)
        {
            if (editor == null || editor.LocalVars == null || editor.LocalVars.Count == 0) return null;

            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < editor.LocalVars.Count; i++)
            {
                var e = editor.LocalVars[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                dict[e.Key] = e.ToArgRuntimeEntry().GetBoxedValue();
            }
            return dict.Count > 0 ? dict : null;
        }

        private static List<ConditionDTO> BuildConditions(TriggerEditorConfig editor)
        {
            var list = new List<ConditionDTO>();
            if (editor?.ConditionsStrong == null) return list;

            for (int i = 0; i < editor.ConditionsStrong.Count; i++)
            {
                var c = editor.ConditionsStrong[i];
                if (c == null) continue;

                var rt = c.ToRuntimeStrong();
                if (rt == null) continue;
                var def = rt.ToConditionDef();
                list.Add(BuildConditionDto(def));
            }

            return list;
        }

        private static List<ActionDTO> BuildActions(TriggerEditorConfig editor)
        {
            var list = new List<ActionDTO>();
            if (editor?.ActionsStrong == null) return list;

            for (int i = 0; i < editor.ActionsStrong.Count; i++)
            {
                var a = editor.ActionsStrong[i];
                if (a == null) continue;

                var rt = a.ToRuntimeStrong();
                if (rt == null) continue;
                var def = rt.ToActionDef();
                list.Add(BuildActionDto(def));
            }

            return list;
        }

        private static ConditionDTO BuildConditionDto(AbilityKit.Ability.Triggering.Definitions.ConditionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(def.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type,
                    Items = new List<ConditionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList<AbilityKit.Ability.Triggering.Definitions.ConditionDef> items))
                {
                    throw new InvalidOperationException($"Condition '{def.Type}' requires args['items'] as IList<ConditionDef>");
                }

                node.Items = new List<ConditionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var child = BuildConditionDto(items[i]);
                    if (child != null) node.Items.Add(child);
                }
                return node;
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Item, out var itemObj) || !(itemObj is AbilityKit.Ability.Triggering.Definitions.ConditionDef item))
                {
                    throw new InvalidOperationException("Condition 'not' requires args['item'] as ConditionDef");
                }

                node.Item = BuildConditionDto(item);
                return node;
            }

            var dto = new ConditionDTO
            {
                Type = def.Type,
                Args = CopyArgs(def.Args)
            };
            return dto;
        }

        private static ActionDTO BuildActionDto(AbilityKit.Ability.Triggering.Definitions.ActionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var dto = new ActionDTO
                {
                    Type = def.Type,
                    Items = new List<ActionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList<AbilityKit.Ability.Triggering.Definitions.ActionDef> items))
                {
                    throw new InvalidOperationException("seq action requires args['items'] as IList<ActionDef>");
                }

                dto.Items = new List<ActionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var child = BuildActionDto(items[i]);
                    if (child != null) dto.Items.Add(child);
                }
                return dto;
            }

            var node = new ActionDTO
            {
                Type = def.Type,
                Args = CopyArgs(def.Args)
            };
            return node;
        }

        private static Dictionary<string, object> CopyArgs(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) return null;
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in args)
            {
                if (kv.Key == null) continue;
                dict[kv.Key] = kv.Value;
            }
            return dict.Count > 0 ? dict : null;
        }

        private static string TryGetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return "Assets";

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return "Assets";

            if (AssetDatabase.IsValidFolder(path)) return path;

            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
        }
    }
}
#endif
