#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 可读格式的触发器计划导入导出工具
    /// </summary>
    internal static class ReadableTriggerPlanExporter
    {
        private const string ReadableFileName = "ability_trigger_plans_readable.json";
        private const string InternalFileName = "ability_trigger_plans.json";

        /// <summary>
        /// 导出可读格式
        /// </summary>
        [MenuItem("AbilityKit/Ability/Export Readable Trigger Plan Json")]
        public static void ExportReadable()
        {
            ExportReadableFromInternal();
        }

        /// <summary>
        /// 从可读格式导入
        /// </summary>
        [MenuItem("AbilityKit/Ability/Import Readable Trigger Plan Json")]
        public static void ImportReadable()
        {
            ImportReadableToInternal();
        }

        /// <summary>
        /// 双向转换：可读格式 <-> 内部格式
        /// </summary>
        [MenuItem("AbilityKit/Ability/Convert Trigger Plan Format")]
        public static void ConvertFormat()
        {
            var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
            var readablePath = Path.Combine(outputDir, ReadableFileName);
            var internalPath = Path.Combine(outputDir, InternalFileName);

            // 确保目录存在
            Directory.CreateDirectory(outputDir);

            // 检查哪个文件更新
            var readableExists = File.Exists(readablePath);
            var internalExists = File.Exists(internalPath);

            if (!readableExists && !internalExists)
            {
                Debug.LogError($"[ReadableTriggerPlan] Neither readable nor internal format found at:\n{outputDir}");
                return;
            }

            if (readableExists && internalExists)
            {
                var readableTime = File.GetLastWriteTime(readablePath);
                var internalTime = File.GetLastWriteTime(internalPath);

                if (readableTime > internalTime)
                {
                    // 可读格式更新，转换为内部格式
                    Debug.Log($"[ReadableTriggerPlan] Readable format is newer. Converting to internal format...");
                    ConvertReadableToInternal(readablePath, internalPath);
                }
                else
                {
                    // 内部格式更新，转换为可读格式
                    Debug.Log($"[ReadableTriggerPlan] Internal format is newer. Converting to readable format...");
                    ConvertInternalToReadable(internalPath, readablePath);
                }
            }
            else if (readableExists)
            {
                // 只有可读格式，转换为内部格式
                Debug.Log($"[ReadableTriggerPlan] Only readable format found. Converting to internal format...");
                ConvertReadableToInternal(readablePath, internalPath);
            }
            else
            {
                // 只有内部格式，转换为可读格式
                Debug.Log($"[ReadableTriggerPlan] Only internal format found. Converting to readable format...");
                ConvertInternalToReadable(internalPath, readablePath);
            }

            AssetDatabase.Refresh();
        }

        private static void ExportReadableFromInternal()
        {
            var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
            Directory.CreateDirectory(outputDir);

            var internalPath = Path.Combine(outputDir, InternalFileName);

            if (!File.Exists(internalPath))
            {
                Debug.LogError($"[ReadableTriggerPlan] Internal format not found at: {internalPath}");
                return;
            }

            try
            {
                var internalJson = File.ReadAllText(internalPath);
                var internalDto = Newtonsoft.Json.JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(internalJson);

                if (internalDto == null)
                {
                    Debug.LogError("[ReadableTriggerPlan] Failed to parse internal format JSON");
                    return;
                }

                var readableJson = ReadableTriggerPlanConverter.ToReadable(internalDto);
                var readablePath = Path.Combine(outputDir, ReadableFileName);
                File.WriteAllText(readablePath, readableJson);

                Debug.Log($"[ReadableTriggerPlan] Exported readable format to:\n{readablePath}");
                AssetDatabase.Refresh();

                EditorUtility.RevealInFinder(readablePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ReadableTriggerPlan] Export failed: {ex.Message}");
            }
        }

        private static void ImportReadableToInternal()
        {
            var outputDir = Path.Combine(Application.dataPath, "Resources", "ability");
            Directory.CreateDirectory(outputDir);

            var readablePath = Path.Combine(outputDir, ReadableFileName);

            if (!File.Exists(readablePath))
            {
                Debug.LogError($"[ReadableTriggerPlan] Readable format not found at: {readablePath}");
                return;
            }

            try
            {
                var readableJson = File.ReadAllText(readablePath);
                var internalDto = ReadableTriggerPlanConverter.FromReadable(readableJson);

                if (internalDto == null)
                {
                    Debug.LogError("[ReadableTriggerPlan] Failed to convert readable format");
                    return;
                }

                var internalJson = Newtonsoft.Json.JsonConvert.SerializeObject(internalDto, Newtonsoft.Json.Formatting.Indented);
                var internalPath = Path.Combine(outputDir, InternalFileName);
                File.WriteAllText(internalPath, internalJson);

                Debug.Log($"[ReadableTriggerPlan] Imported readable format to:\n{internalPath}");
                AssetDatabase.Refresh();

                EditorUtility.RevealInFinder(internalPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ReadableTriggerPlan] Import failed: {ex.Message}");
            }
        }

        private static void ConvertInternalToReadable(string internalPath, string readablePath)
        {
            var internalJson = File.ReadAllText(internalPath);
            var internalDto = Newtonsoft.Json.JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(internalJson);

            if (internalDto == null)
            {
                Debug.LogError("[ReadableTriggerPlan] Failed to parse internal format");
                return;
            }

            var readableJson = ReadableTriggerPlanConverter.ToReadable(internalDto);
            File.WriteAllText(readablePath, readableJson);

            Debug.Log($"[ReadableTriggerPlan] Converted internal -> readable:\n{readablePath}");
        }

        private static void ConvertReadableToInternal(string readablePath, string internalPath)
        {
            var readableJson = File.ReadAllText(readablePath);
            var internalDto = ReadableTriggerPlanConverter.FromReadable(readableJson);

            if (internalDto == null)
            {
                Debug.LogError("[ReadableTriggerPlan] Failed to convert readable format");
                return;
            }

            var internalJson = Newtonsoft.Json.JsonConvert.SerializeObject(internalDto, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(internalPath, internalJson);

            Debug.Log($"[ReadableTriggerPlan] Converted readable -> internal:\n{internalPath}");
        }
    }
}
#endif
