#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanExportPipeline
    {
        public static TriggerPlanDatabaseDto BuildPlanDto(
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

            var guids = AbilityTriggerJsonExporter.FindAbilityModuleGuids(assetFolder);
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
                        if (!AbilityTriggerJsonExporter.TryCompilePlanFromEditor(tr, db.Strings, out var plan, out var phase, out var priority, out var failReason))
                        {
                            if (string.Equals(failReason, "no_actions", StringComparison.Ordinal))
                            {
                                skippedNoActionsCount++;
                            }
                            else if (string.Equals(failReason, "action_compile_failed", StringComparison.Ordinal))
                            {
                                skippedActionCompileFailCount++;
                                CountOne(actionCompileFailByType, AbilityTriggerJsonExporter.ExtractRootActionType(tr));
                            }
                            else if (string.Equals(failReason, "condition_compile_failed", StringComparison.Ordinal))
                            {
                                skippedConditionCompileFailCount++;
                                CountOne(conditionCompileFailByType, AbilityTriggerJsonExporter.ExtractRootConditionType(tr));
                            }
                            else
                            {
                                skippedExceptionCount++;
                            }

                            continue;
                        }

                        var triggerDto = AbilityTriggerJsonExporter.BuildTriggerPlanDto(tr, in plan, phase, priority);
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
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Action compile failures by type: {AbilityTriggerExportUtils.FormatCounterMap(actionCompileFailByType)}");
            }
            if (conditionCompileFailByType.Count > 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Condition compile failures by type: {AbilityTriggerExportUtils.FormatCounterMap(conditionCompileFailByType)}");
            }

            if (emptyEventIdCount > 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] Empty EventId triggers (active by TriggerId) (up to 32): {AbilityTriggerExportUtils.FormatIntList(emptyEventIdTriggerIds)}");
            }

            return db;
        }

        private static void CountOne(Dictionary<string, int> map, string key)
        {
            if (map == null) return;
            if (string.IsNullOrEmpty(key)) key = "<null>";
            map.TryGetValue(key, out var v);
            map[key] = v + 1;
        }
    }
}
#endif
