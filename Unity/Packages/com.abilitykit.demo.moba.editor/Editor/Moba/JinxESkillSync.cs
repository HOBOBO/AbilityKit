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
    /// <summary>Publishes the authored Jinx E three-trap placeholder slice.</summary>
    public static class JinxESkillSync
    {
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string FlowSourcePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-e-flow.json";

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Publish E")]
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
            Debug.Log("[JinxESkillSync] Published and checked Jinx E placeholder configuration.");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Check E")]
        public static void CheckPublished()
        {
            MobaSkillPipelineExporter.Check();
            InvokeTriggerAuthoring("CheckRuntimeBatch");
            Debug.Log("[JinxESkillSync] Jinx E Flow and Trigger checks passed.");
        }

        private static void SyncAuthoredFlow()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FlowSourcePath));
            if (!File.Exists(sourcePath)) throw new InvalidDataException("Jinx E flow source is missing: " + sourcePath);
            var sourceText = File.ReadAllText(sourcePath).Trim();
            var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(sourceText, typeof(SkillFlowDTO));
            if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) || dto.Id != 30020301)
                throw new InvalidDataException("Jinx E flow source must contain exactly flow 30020301.");
            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            var matches = flows.Select((flow, index) => new { flow, index }).Where(x => x.flow != null && x.flow.Id == dto.Id).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("SkillFlowCO contains duplicate Jinx E flow.");
            var authored = SkillFlowDef.FromDto(dto, matches.Length == 1 ? matches[0].flow : null);
            asset.dataList = matches.Length == 1
                ? flows.Select((flow, index) => index == matches[0].index ? authored : flow).ToArray()
                : flows.Concat(new[] { authored }).ToArray();
            EditorUtility.SetDirty(asset);
        }

        private static void InvokeTriggerAuthoring(string methodName)
        {
            var migration = typeof(TriggerAuthoringProjectAsset).Assembly.GetType("AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            if (migration == null) throw new MissingMemberException("Trigger Authoring MOBA migration type is unavailable.");
            var method = migration.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(migration.FullName, methodName);
            try { method.Invoke(null, null); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { throw new InvalidOperationException("Trigger Authoring " + methodName + " failed.", exception.InnerException); }
        }
    }
}
