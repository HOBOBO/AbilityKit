#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Json;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerJsonExporter
    {
        private const string OutputResourcesDir = "ability";
        private const string OutputFileWithoutExt = "ability_triggers";
        private const string DefaultAbilityConfigFolder = "Assets/Configs/Ability";

        [MenuItem("AbilityKit/Ability/Export Trigger Json")]
        public static void ExportSelectedFolder()
        {
            var folder = TryGetSelectedFolderPath();
            ExportFromFolder(folder);
        }

        [MenuItem("AbilityKit/Ability/Export Trigger Json (Configs/Ability)")]
        public static void ExportDefaultFolder()
        {
            ExportFromFolder(DefaultAbilityConfigFolder);
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

                        var runtime = tr.ToRuntime();
                        var triggerDto = new TriggerDTO
                        {
                            TriggerId = tr.TriggerId,
                            EventId = runtime.EventId,
                            InitialLocalVars = BuildInitialLocalVars(runtime),
                            Conditions = BuildConditions(runtime),
                            Actions = BuildActions(runtime)
                        };
                        db.Triggers.Add(triggerDto);
                        exportedTriggerCount++;
                    }
                }
            }

            return db;
        }

        private static Dictionary<string, object> BuildInitialLocalVars(Configs.TriggerRuntimeConfig runtime)
        {
            if (runtime == null || runtime.LocalVars == null || runtime.LocalVars.Count == 0) return null;

            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < runtime.LocalVars.Count; i++)
            {
                var e = runtime.LocalVars[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                dict[e.Key] = e.GetBoxedValue();
            }
            return dict.Count > 0 ? dict : null;
        }

        private static List<ConditionDTO> BuildConditions(Configs.TriggerRuntimeConfig runtime)
        {
            var list = new List<ConditionDTO>();
            if (runtime?.ConditionsStrong == null) return list;

            for (int i = 0; i < runtime.ConditionsStrong.Count; i++)
            {
                var c = runtime.ConditionsStrong[i];
                if (c == null) continue;

                var def = c.ToConditionDef();
                list.Add(new ConditionDTO
                {
                    Type = def.Type,
                    Args = CopyArgs(def.Args)
                });
            }

            return list;
        }

        private static List<ActionDTO> BuildActions(Configs.TriggerRuntimeConfig runtime)
        {
            var list = new List<ActionDTO>();
            if (runtime?.ActionsStrong == null) return list;

            for (int i = 0; i < runtime.ActionsStrong.Count; i++)
            {
                var a = runtime.ActionsStrong[i];
                if (a == null) continue;

                var def = a.ToActionDef();
                list.Add(new ActionDTO
                {
                    Type = def.Type,
                    Args = CopyArgs(def.Args)
                });
            }

            return list;
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
