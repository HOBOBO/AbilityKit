using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Config.Core;
using System.Reflection;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Publishes the authored Annie passive/Q/W slice through the shared MOBA editors.</summary>
    public static class AnnieSkillSync
    {
        private const string FlowAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string FlowSourcePath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/annie-r-flow.json";

        public static void SyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PublishSummonPresentation();
            SyncAuthoredFlows();
            AssetDatabase.SaveAssets();
            InvokeTriggerAuthoring("ImportProductionSourcesBatch");
            // Luban config tables are published before this method so the
            // Trigger Authoring semantic references resolve against the new IDs.
            // The full pipeline apply runs after both authoring publishers.
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            InvokeTriggerAuthoring("PublishRuntimeBatch");
            InvokeTriggerAuthoring("CheckRuntimeBatch");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AnnieSkillSync] Published and checked Annie passive/Q/W/E authored configuration.");
        }

        /// <summary>
        /// Derives the autonomous landing effect from the showcase particle branch.
        /// </summary>
        public static void PublishSummonPresentation()
        {
            const string root = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/";
            const string source = root + "ChampionVfx/14.Void_Annie/Void_Annie_Tibbers_Spawn.prefab";
            const string destination = root + "MobaVfx/Annie/Tibbers_Spawn.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source)
                ?? throw new InvalidDataException("Missing Tibbers landing source: " + source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                if (particles.Length == 0) throw new InvalidDataException("Missing Tibbers landing particles.");
                foreach (var particle in particles)
                {
                    var main = particle.main;
                    main.playOnAwake = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(particle);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (PrefabUtility.SaveAsPrefabAsset(instance, destination) == null)
                    throw new InvalidDataException("Could not publish Tibbers landing variant: " + destination);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void SyncAuthoredFlows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null) throw new InvalidDataException("SkillFlowCO asset is missing: " + FlowAssetPath);
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FlowSourcePath));
            if (!File.Exists(sourcePath)) throw new InvalidDataException("Annie R flow source is missing: " + sourcePath);
            var sourceText = File.ReadAllText(sourcePath).Trim();
            if (sourceText.StartsWith("{", StringComparison.Ordinal)) sourceText = "[" + sourceText + "]";
            var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(sourceText, typeof(SkillFlowDTO));
            if (values == null || values.Length != 1 || !(values.GetValue(0) is SkillFlowDTO dto) || dto.Id != 30030501)
                throw new InvalidDataException("Annie R flow source must contain exactly flow 30030501.");
            var flows = asset.dataList ?? Array.Empty<SkillFlowDef>();
            var existing = flows.SingleOrDefault(flow => flow != null && flow.Id == dto.Id);
            if (existing == null) flows = flows.Concat(new[] { SkillFlowDef.FromDto(dto) }).ToArray();
            else flows[Array.IndexOf(flows, existing)] = SkillFlowDef.FromDto(dto, existing);
            asset.dataList = flows;
            EditorUtility.SetDirty(asset);
        }

        public static void CheckPublished()
        {
            InvokeTriggerAuthoring("CheckRuntimeBatch");
            MobaSkillPipelineExporter.Check();
            Debug.Log("[AnnieSkillSync] Annie passive/Q/W/E published checks passed.");
        }

        private static void InvokeTriggerAuthoring(string methodName)
        {
            var migration = typeof(TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration");
            if (migration == null) throw new MissingMemberException("Trigger Authoring MOBA migration type is unavailable.");
            var method = migration.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method == null) throw new MissingMethodException(migration.FullName, methodName);
            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException($"Trigger Authoring {methodName} failed.", exception.InnerException);
            }
        }

    }
}
