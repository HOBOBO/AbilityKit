using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Editor;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Publishes Jinx R's production Flow and Trigger authoring sources.</summary>
    public static class JinxRSkillSync
    {
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string FlowSourcePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-r-flow.json";

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Publish R")]
        public static void SyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            InvokeTriggerAuthoring("ImportProductionSourcesBatch");
            SyncAuthoredFlow();
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            InvokeTriggerAuthoring("PublishRuntimeBatch");
            InvokeTriggerAuthoring("CheckRuntimeBatch");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[JinxRSkillSync] Published and checked Jinx R Flow and Trigger configuration.");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Check R")]
        public static void CheckPublished()
        {
            MobaSkillPipelineExporter.Check();
            InvokeTriggerAuthoring("CheckRuntimeBatch");
            Debug.Log("[JinxRSkillSync] Jinx R Flow and Trigger checks passed.");
        }

        private static void SyncAuthoredFlow()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FlowSourcePath));
            if (!File.Exists(sourcePath)) throw new InvalidDataException("Jinx R flow source is missing: " + sourcePath);
            var text = File.ReadAllText(sourcePath).Trim();
            var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(text, typeof(SkillFlowDTO));
            if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) || dto.Id != 30020401)
                throw new InvalidDataException("Jinx R flow source must contain exactly flow 30020401.");
            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            var matches = flows.Select((flow, index) => new { flow, index })
                .Where(item => item.flow != null && item.flow.Id == dto.Id).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("SkillFlowCO contains duplicate Jinx R flows.");
            var authored = SkillFlowDef.FromDto(dto);
            asset.dataList = matches.Length == 1
                ? flows.Select((flow, index) => index == matches[0].index ? authored : flow).ToArray()
                : flows.Concat(new[] { authored }).ToArray();
            EditorUtility.SetDirty(asset);
        }

        private static void InvokeTriggerAuthoring(string methodName)
        {
            var migration = typeof(TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            if (migration == null) throw new MissingMemberException("Trigger Authoring MOBA migration type is unavailable.");
            var method = migration.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(migration.FullName, methodName);
            try { method.Invoke(null, null); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { throw new InvalidOperationException("Trigger Authoring " + methodName + " failed.", exception.InnerException); }
        }
    }
}
