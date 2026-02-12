#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
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
            var folder = AbilityTriggerExportUtils.TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Plan Json")]
        public static void ExportSelectedFolderPlans()
        {
            var folder = AbilityTriggerExportUtils.TryGetSelectedFolderPath();
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

            var dto = TriggerPlanExportPipeline.BuildPlanDto(assetFolder, out var moduleCount, out var exportedTriggerCount, out var skippedDisabledCount, out var skippedInvalidIdCount);
            if (assetFolder != "Assets" && (moduleCount == 0 || exportedTriggerCount == 0))
            {
                Debug.Log($"[AbilityTriggerJsonExporter] No trigger plans exported from '{assetFolder}'. Fallback to scan whole 'Assets'.");
                dto = TriggerPlanExportPipeline.BuildPlanDto("Assets", out moduleCount, out exportedTriggerCount, out skippedDisabledCount, out skippedInvalidIdCount);
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

            var guids = FindAbilityModuleGuids(assetFolder);
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

        internal static string[] FindAbilityModuleGuids(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) assetFolder = "Assets";

            var guids = AssetDatabase.FindAssets("t:AbilityModuleSO", new[] { assetFolder });
            var primaryCount = guids != null ? guids.Length : 0;
            if (guids != null && guids.Length > 0)
            {
                Debug.Log($"[AbilityTriggerJsonExporter] FindAssets('t:AbilityModuleSO') found {primaryCount} under '{assetFolder}'.");
                return guids;
            }

            Debug.LogWarning($"[AbilityTriggerJsonExporter] FindAssets('t:AbilityModuleSO') found 0 under '{assetFolder}'. Trying fallback scan...");

            // Fallback: some Unity setups may fail to resolve t:AbilityModuleSO queries for types defined in packages.
            // Scan ScriptableObjects and filter by main asset type.
            var soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { assetFolder });
            var soCount = soGuids != null ? soGuids.Length : 0;
            if (soGuids == null || soGuids.Length == 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] No ScriptableObject assets found under '{assetFolder}'. primaryCount={primaryCount}");
                return Array.Empty<string>();
            }

            var list = new List<string>();
            for (int i = 0; i < soGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(soGuids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                Type t;
                try { t = AssetDatabase.GetMainAssetTypeAtPath(path); }
                catch { continue; }
                if (t == typeof(AbilityModuleSO))
                {
                    list.Add(soGuids[i]);
                }
            }

            if (list.Count == 0)
            {
                var examplePath = AssetDatabase.GUIDToAssetPath(soGuids[0]);
                Type exampleType = null;
                try { exampleType = AssetDatabase.GetMainAssetTypeAtPath(examplePath); }
                catch { }

                Debug.LogWarning(
                    $"[AbilityTriggerJsonExporter] No AbilityModuleSO assets found under '{assetFolder}'. " +
                    $"primaryCount={primaryCount}, soCount={soCount}, matched=0, example='{examplePath}', exampleType='{exampleType?.FullName ?? "<null>"}'. " +
                    $"This usually means Unity can't resolve AbilityModuleSO type (assembly not loaded / compile errors / domain reload pending)."
                );
                return Array.Empty<string>();
            }

            var exampleFoundPath = AssetDatabase.GUIDToAssetPath(list[0]);
            Debug.LogWarning($"[AbilityTriggerJsonExporter] FindAssets('t:AbilityModuleSO') returned 0; fallback scan found {list.Count} AbilityModuleSO (soCount={soCount}). example='{exampleFoundPath}'");
            return list.ToArray();
        }

        internal static bool TryCompilePlanFromEditor(
            TriggerEditorConfig tr,
            Dictionary<int, string> stringTable,
            out TriggerPlan<object> plan,
            out int phase,
            out int priority,
            out string failReason)
        {
            plan = default;
            phase = 0;
            priority = 0;
            failReason = null;

            JsonConditionEditorConfig cond = null;
            if (tr.ConditionsStrong != null && tr.ConditionsStrong.Count > 0)
            {
                if (tr.ConditionsStrong.Count == 1)
                {
                    cond = ToJsonConditionNode(tr.ConditionsStrong[0]);
                }
                else
                {
                    var items = new List<ConditionEditorConfigBase>(tr.ConditionsStrong.Count);
                    for (int i = 0; i < tr.ConditionsStrong.Count; i++)
                    {
                        var n = ToJsonConditionNode(tr.ConditionsStrong[i]);
                        if (n != null) items.Add(n);
                    }
                    cond = new JsonConditionEditorConfig { TypeValue = TriggerConditionTypes.All, Items = items };
                }
            }

            JsonActionEditorConfig act = null;
            if (tr.ActionsStrong != null && tr.ActionsStrong.Count > 0)
            {
                if (tr.ActionsStrong.Count == 1)
                {
                    act = ToJsonActionNode(tr.ActionsStrong[0]);
                }
                else
                {
                    var items = new List<ActionEditorConfigBase>(tr.ActionsStrong.Count);
                    for (int i = 0; i < tr.ActionsStrong.Count; i++)
                    {
                        var n = ToJsonActionNode(tr.ActionsStrong[i]);
                        if (n != null) items.Add(n);
                    }
                    act = new JsonActionEditorConfig { TypeValue = TriggerActionTypes.Seq, Items = items };
                }
            }

            if (act == null)
            {
                failReason = "no_actions";
                return false;
            }

            if (!TryCompileActionTree(
                    act,
                    stringTable,
                    TriggerPlanCompilerResolvers.ResolvePayloadFieldId,
                    TriggerPlanCompilerResolvers.ResolveActionId,
                    out var actions) || actions == null || actions.Length == 0)
            {
                failReason = "action_compile_failed";
                return false;
            }

            if (cond != null)
            {
                if (!TryCompileConditionTree(
                        cond,
                        TriggerPlanCompilerResolvers.ResolvePayloadFieldId,
                        out var predExpr))
                {
                    failReason = "condition_compile_failed";
                    return false;
                }

                plan = new TriggerPlan<object>(phase, priority, predExpr, actions);
                return true;
            }

            plan = new TriggerPlan<object>(phase, priority, actions);
            return true;
        }

        private static bool TryCompileActionTree(
            JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            if (action == null) return false;

            // Custom: seq flatten
            if (string.Equals(action.TypeValue, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                if (action.Items == null || action.Items.Count == 0)
                {
                    plans = Array.Empty<ActionCallPlan>();
                    return true;
                }

                var list = new List<ActionCallPlan>(action.Items.Count);
                for (int i = 0; i < action.Items.Count; i++)
                {
                    if (!(action.Items[i] is JsonActionEditorConfig child)) return false;
                    if (!TryCompileActionTree(child, stringTable, payloadFieldIdResolver, actionIdResolver, out var childPlans)) return false;
                    if (childPlans == null || childPlans.Length == 0) continue;
                    for (int j = 0; j < childPlans.Length; j++) list.Add(childPlans[j]);
                }

                plans = list.ToArray();
                return true;
            }

            var handlers = TriggerPlanExportActionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryCompileAction(action, stringTable, payloadFieldIdResolver, actionIdResolver, out plans))
                    {
                        return plans != null;
                    }
                }
            }

            // Default: codegen/fallback compiler
            return GeneratedTriggerPlanCompiler.TryCompileActionTree(action, payloadFieldIdResolver, actionIdResolver, out plans);
        }

        private static bool TryCompileConditionTree(
            JsonConditionEditorConfig condition,
            Func<string, int> payloadFieldIdResolver,
            out PredicateExprPlan plan)
        {
            plan = default;
            if (condition == null) return false;

            var handlers = TriggerPlanExportConditionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryCompileCondition(condition, payloadFieldIdResolver, out plan))
                    {
                        return true;
                    }
                }
            }

            return GeneratedTriggerPlanCompiler.TryCompileConditionTree(condition, payloadFieldIdResolver, out plan);
        }

        private static JsonActionEditorConfig ToJsonActionNode(ActionEditorConfigBase node)
        {
            if (node == null) return null;
            if (node is JsonActionEditorConfig j) return j;

            var handlers = TriggerPlanExportActionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryConvertActionNode(node, out var converted) && converted != null)
                    {
                        return converted;
                    }
                }
            }

            try
            {
                var rt = node.ToRuntimeStrong();
                if (rt == null) return null;
                var def = rt.ToActionDef();
                var dto = BuildActionDto(def);
                return JsonActionEditorConfig.FromDto(dto);
            }
            catch
            {
                return null;
            }
        }

        private static JsonConditionEditorConfig ToJsonConditionNode(ConditionEditorConfigBase node)
        {
            if (node == null) return null;
            if (node is JsonConditionEditorConfig j) return j;

            var handlers = TriggerPlanExportConditionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryConvertConditionNode(node, out var converted) && converted != null)
                    {
                        return converted;
                    }
                }
            }

            try
            {
                var rt = node.ToRuntimeStrong();
                if (rt == null) return null;
                var def = rt.ToConditionDef();
                var dto = BuildConditionDto(def);
                return JsonConditionEditorConfig.FromDto(dto);
            }
            catch
            {
                return null;
            }
        }

        internal static string ExtractRootActionType(TriggerEditorConfig tr)
        {
            if (tr?.ActionsStrong == null || tr.ActionsStrong.Count == 0) return null;
            if (tr.ActionsStrong.Count == 1) return tr.ActionsStrong[0]?.Type;
            return TriggerActionTypes.Seq;
        }

        internal static string ExtractRootConditionType(TriggerEditorConfig tr)
        {
            if (tr?.ConditionsStrong == null || tr.ConditionsStrong.Count == 0) return null;
            if (tr.ConditionsStrong.Count == 1) return tr.ConditionsStrong[0]?.Type;
            return TriggerConditionTypes.All;
        }

        internal static TriggerPlanDto BuildTriggerPlanDto(TriggerEditorConfig tr, in TriggerPlan<object> plan, int phase, int priority)
        {
            var eventName = tr.EventId;
            var eventId = !string.IsNullOrEmpty(eventName) ? StableStringId.Get("event:" + eventName) : 0;

            var dto = new TriggerPlanDto
            {
                TriggerId = tr.TriggerId,
                EventName = eventName,
                EventId = eventId,
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
            return null;
            //if (!plan.HasPredicate || plan.PredicateKind != EPredicateKind.Legacy) return null;
            //if (!plan.LegacyPredicate.HasValue) return null;
            //var p = plan.LegacyPredicate.Value;
            //return new LegacyPredicateDto { Type = p.Type, Args = p.Args != null ? new Dictionary<string, object>(p.Args, StringComparer.Ordinal) : null };
        }

        private static List<LegacyActionDto> BuildLegacyActionsDto(in TriggerPlan<object> plan)
        {
            //if (plan.LegacyActions == null || plan.LegacyActions.Length == 0) return null;
            //var list = new List<LegacyActionDto>(plan.LegacyActions.Length);
            //for (int i = 0; i < plan.LegacyActions.Length; i++)
            //{
            //    var a = plan.LegacyActions[i];
            //    if (string.IsNullOrEmpty(a.Type)) continue;
            //    list.Add(new LegacyActionDto { Type = a.Type, Args = a.Args != null ? new Dictionary<string, object>(a.Args, StringComparer.Ordinal) : null });
            //}
            //return list.Count > 0 ? list : null;
            return null;
        }

        private static PredicatePlanDto BuildPredicateDto(in TriggerPlan<object> plan)
        {
            if (!plan.HasPredicate || plan.PredicateKind == EPredicateKind.None)
            {
                return new PredicatePlanDto { Kind = "none", Nodes = null };
            }

            //if (plan.PredicateKind == EPredicateKind.Legacy)
            //{
            //    return new PredicatePlanDto { Kind = "legacy", Nodes = null };
            //}

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
                            Left = BuildNumericValueRefDto(in n.Left),
                            Right = BuildNumericValueRefDto(in n.Right)
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
                    Arg0 = BuildNumericValueRefDto(in a.Arg0),
                    Arg1 = BuildNumericValueRefDto(in a.Arg1)
                });
            }
            return list;
        }

        private static NumericValueRefDto BuildNumericValueRefDto(in NumericValueRef r)
        {
            return new NumericValueRefDto
            {
                Kind = r.Kind.ToString(),
                ConstValue = r.ConstValue,
                BoardId = r.BoardId,
                KeyId = r.KeyId,
                FieldId = r.FieldId,
                DomainId = r.DomainId,
                Key = r.Key,
                ExprText = r.ExprText
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
                Args = AbilityTriggerExportUtils.CopyArgs(def.Args)
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
                Args = AbilityTriggerExportUtils.CopyArgs(def.Args)
            };
            return node;
        }

    }
}
#endif
