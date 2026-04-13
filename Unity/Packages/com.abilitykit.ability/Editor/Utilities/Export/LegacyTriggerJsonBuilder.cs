#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class LegacyTriggerJsonBuilder
    {
        public static AbilityTriggerDatabaseDTO BuildDto(
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

            try
            {
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

                return db;
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, $"Legacy trigger export BuildDto failed. assetFolder='{assetFolder}'");
                throw;
            }
        }

        private static Dictionary<string, object> BuildInitialLocalVars(TriggerEditorConfig editor)
        {
            if (editor == null || editor.LocalVars == null || editor.LocalVars.Count == 0) return null;

            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < editor.LocalVars.Count; i++)
            {
                var e = editor.LocalVars[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                dict[e.Key] = e.ToArgRuntimeEntryCore().GetBoxedValue();
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

                var cfg = c.ToRuntimeConfig() as AbilityKit.Ability.Config.ConditionConfigBase;
                if (cfg == null) continue;
                var def = cfg.ToConditionDef();
                if (def == null) continue;
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

                var cfg = a.ToRuntimeConfig() as AbilityKit.Ability.Config.ActionConfigBase;
                if (cfg == null) continue;
                var def = cfg.ToActionDef();
                if (def == null) continue;
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

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList items))
                {
                    throw new InvalidOperationException($"Condition '{def.Type}' requires args['items'] as IList<ConditionDef>");
                }

                node.Items = new List<ConditionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is AbilityKit.Ability.Triggering.Definitions.ConditionDef childDef)) continue;
                    var child = BuildConditionDto(childDef);
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

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList items))
                {
                    throw new InvalidOperationException("seq action requires args['items'] as IList<ActionDef>");
                }

                dto.Items = new List<ActionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is AbilityKit.Ability.Triggering.Definitions.ActionDef childDef)) continue;
                    var child = BuildActionDto(childDef);
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
