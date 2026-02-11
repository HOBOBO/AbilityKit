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

        [Serializable]
        private sealed class TriggerPlanDatabaseDto
        {
            public readonly List<TriggerPlanDto> Triggers = new List<TriggerPlanDto>();

            // Optional: string table for actions like debug_log.
            public readonly Dictionary<int, string> Strings = new Dictionary<int, string>();
        }

        [Serializable]
        private sealed class TriggerPlanDto
        {
            public int TriggerId;
            public string EventName;
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
            public NumericValueRefDto Left;
            public NumericValueRefDto Right;
        }

        [Serializable]
        private sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public NumericValueRefDto Arg0;
            public NumericValueRefDto Arg1;
        }

        [Serializable]
        private sealed class NumericValueRefDto
        {
            public string Kind;
            public double ConstValue;
            public int BoardId;
            public int KeyId;
            public int FieldId;
            public string DomainId;
            public string Key;
            public string ExprText;
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

            var emptyEventIdCount = 0;
            var emptyEventIdTriggerIds = new List<int>();
            var skippedNoActionsCount = 0;
            var skippedActionCompileFailCount = 0;
            var skippedConditionCompileFailCount = 0;
            var skippedExceptionCount = 0;
            var actionCompileFailByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var conditionCompileFailByType = new Dictionary<string, int>(StringComparer.Ordinal);

            var guids = FindAbilityModuleGuids(assetFolder);
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

                    if (string.IsNullOrEmpty(tr.EventId))
                    {
                        // Active triggers: allow empty EventId, they will be executed by TriggerId.
                        emptyEventIdCount++;
                        if (emptyEventIdTriggerIds.Count < 32)
                        {
                            emptyEventIdTriggerIds.Add(tr.TriggerId);
                        }
                    }

                    try
                    {
                        if (!TryCompilePlanFromEditor(tr, db.Strings, out var plan, out var phase, out var priority, out var failReason))
                        {
                            if (string.Equals(failReason, "no_actions", StringComparison.Ordinal))
                            {
                                skippedNoActionsCount++;
                            }
                            else if (string.Equals(failReason, "action_compile_failed", StringComparison.Ordinal))
                            {
                                skippedActionCompileFailCount++;
                                CountOne(actionCompileFailByType, ExtractRootActionType(tr));
                            }
                            else if (string.Equals(failReason, "condition_compile_failed", StringComparison.Ordinal))
                            {
                                skippedConditionCompileFailCount++;
                                CountOne(conditionCompileFailByType, ExtractRootConditionType(tr));
                            }
                            else
                            {
                                skippedExceptionCount++;
                            }

                            continue;
                        }

                        var triggerDto = BuildTriggerPlanDto(tr, plan, phase, priority);
                        db.Triggers.Add(triggerDto);
                        exportedTriggerCount++;
                    }
                    catch (Exception ex)
                    {
                        skippedExceptionCount++;
                        Debug.LogWarning($"[AbilityTriggerJsonExporter] Plan export failed (exception). triggerId={tr.TriggerId} eventId='{tr.EventId}' err={ex.Message}");
                    }
                }
            }

            Debug.Log(
                $"[AbilityTriggerJsonExporter] Plan export summary: " +
                $"exported={exportedTriggerCount}, " +
                $"skippedDisabled={skippedDisabledCount}, skippedTriggerId<=0={skippedInvalidIdCount}, " +
                $"emptyEventId={emptyEventIdCount}, skippedNoActions={skippedNoActionsCount}, " +
                $"skippedActionCompileFail={skippedActionCompileFailCount}, skippedConditionCompileFail={skippedConditionCompileFailCount}, " +
                $"skippedException={skippedExceptionCount}");

            if (actionCompileFailByType.Count > 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Action compile failures by type: {FormatCounterMap(actionCompileFailByType)}");
            }
            if (conditionCompileFailByType.Count > 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Condition compile failures by type: {FormatCounterMap(conditionCompileFailByType)}");
            }

            if (emptyEventIdCount > 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Empty EventId triggers (active by TriggerId) (up to 32): {FormatIntList(emptyEventIdTriggerIds)}");
            }

            return db;
        }

        private static bool TryCompilePlanFromEditor(
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

            if (!TryCompileActionTreeWithOverrides(
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
                if (!GeneratedTriggerPlanCompiler.TryCompileConditionTree(
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

        private static bool TryCompileActionTreeWithOverrides(
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
                    if (!TryCompileActionTreeWithOverrides(child, stringTable, payloadFieldIdResolver, actionIdResolver, out var childPlans)) return false;
                    if (childPlans == null || childPlans.Length == 0) continue;
                    for (int j = 0; j < childPlans.Length; j++) list.Add(childPlans[j]);
                }

                plans = list.ToArray();
                return true;
            }

            // Custom: debug_log(message, dump_args)
            if (string.Equals(action.TypeValue, TriggerActionTypes.DebugLog, StringComparison.Ordinal))
            {
                if (actionIdResolver == null) return false;
                var id = actionIdResolver(action.TypeValue);
                if (id.Value == 0) return false;

                var msg = string.Empty;
                var dump = false;

                if (action.Args != null)
                {
                    if (action.Args.TryGetValue("message", out var mObj) && mObj != null)
                    {
                        msg = mObj as string ?? mObj.ToString();
                    }

                    if (action.Args.TryGetValue("dump_args", out var dObj) && dObj != null)
                    {
                        if (dObj is bool b) dump = b;
                        else
                        {
                            try { dump = Convert.ToBoolean(dObj); }
                            catch { dump = false; }
                        }
                    }
                }

                var strId = StableStringId.Get("str:" + (msg ?? string.Empty));
                if (stringTable != null)
                {
                    if (!stringTable.TryGetValue(strId, out var existing))
                    {
                        stringTable[strId] = msg ?? string.Empty;
                    }
                    else if (!string.Equals(existing, msg ?? string.Empty, StringComparison.Ordinal))
                    {
                        // collision should be impossible due to StableStringId check, but keep last just in case.
                        stringTable[strId] = msg ?? string.Empty;
                    }
                }

                plans = new[]
                {
                    new ActionCallPlan(id,
                        NumericValueRef.Const(strId),
                        NumericValueRef.Const(dump ? 1d : 0d))
                };
                return true;
            }

            // Default: codegen/fallback compiler
            return GeneratedTriggerPlanCompiler.TryCompileActionTree(action, payloadFieldIdResolver, actionIdResolver, out plans);
        }

        private static JsonActionEditorConfig ToJsonActionNode(ActionEditorConfigBase node)
        {
            if (node == null) return null;
            if (node is JsonActionEditorConfig j) return j;

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

        private static void CountOne(Dictionary<string, int> map, string key)
        {
            if (map == null) return;
            if (string.IsNullOrEmpty(key)) key = "<null>";
            map.TryGetValue(key, out var v);
            map[key] = v + 1;
        }

        private static string ExtractRootActionType(TriggerEditorConfig tr)
        {
            if (tr?.ActionsStrong == null || tr.ActionsStrong.Count == 0) return null;
            if (tr.ActionsStrong.Count == 1) return tr.ActionsStrong[0]?.Type;
            return TriggerActionTypes.Seq;
        }

        private static string ExtractRootConditionType(TriggerEditorConfig tr)
        {
            if (tr?.ConditionsStrong == null || tr.ConditionsStrong.Count == 0) return null;
            if (tr.ConditionsStrong.Count == 1) return tr.ConditionsStrong[0]?.Type;
            return TriggerConditionTypes.All;
        }

        private static string FormatCounterMap(Dictionary<string, int> map)
        {
            if (map == null || map.Count == 0) return "(empty)";
            var s = "";
            var first = true;
            foreach (var kv in map)
            {
                if (!first) s += ", ";
                first = false;
                s += kv.Key + "=" + kv.Value;
            }
            return s;
        }

        private static string FormatIntList(List<int> list)
        {
            if (list == null || list.Count == 0) return "(empty)";
            var s = "";
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) s += ",";
                s += list[i];
            }
            return s;
        }

        private static TriggerPlanDto BuildTriggerPlanDto(TriggerEditorConfig tr, in TriggerPlan<object> plan, int phase, int priority)
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
