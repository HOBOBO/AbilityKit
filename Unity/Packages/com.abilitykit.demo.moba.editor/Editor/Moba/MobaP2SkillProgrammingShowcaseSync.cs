#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaP2SkillProgrammingShowcaseSync
    {
        public const int ApplyTriggerId = 99_190_020;
        public const int ClearTriggerId = 99_190_021;
        public const int FlowId = 99_200_020;
        public const string TriggerModuleId = "ability.moba.tests.p2_showcase";
        public const string ProjectAssetPath =
            "Assets/AbilityKit/MobaTriggerAuthoring/MobaTriggerAuthoringProject.asset";
        public const string TriggerSourceAssetPath =
            "Assets/AbilityKit/MobaTriggerAuthoring/Showcases/moba-p2-skill-programming.trigger.json";
        public const string TriggerModuleAssetPath =
            "Assets/AbilityKit/MobaTriggerAuthoring/Packages/ability_moba_tests_p2_showcase.Module.asset";
        public const string FlowSourceAssetPath =
            "Assets/AbilityKit/MobaSkillFlowAuthoring/Showcases/moba-p2-derived-capture-flow.json";
        public const string FlowAssetPath =
            "Assets/AbilityKit/MobaSkillFlowAuthoring/Showcases/MobaP2DerivedCaptureShowcase.asset";

        [MenuItem("AbilityKit/Moba/Skill Flow/Sync P2 Skill Programming Showcase")]
        public static void Sync()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SyncTriggerModule();
            SyncSkillFlow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MobaP2SkillProgrammingShowcaseSync] Synchronized P2 trigger module and skill flow assets.");
        }

        public static void SyncBatch()
        {
            Sync();
        }

        private static void SyncTriggerModule()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectAssetPath);
            if (project == null)
                throw new InvalidOperationException("MOBA Trigger Authoring project is missing: " + ProjectAssetPath);

            var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(TriggerModuleAssetPath);
            if (module == null)
            {
                module = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
                AssetDatabase.CreateAsset(module, TriggerModuleAssetPath);
            }

            var sourcePath = ResolveProjectPath(TriggerSourceAssetPath);
            var options = new TriggerAuthoringModuleSourceImportOptions
            {
                ExpectedModuleId = TriggerModuleId,
                Force = true,
                ValidateProject = true,
                ConfigurePackageMetadata = true,
                DomainId = "ability",
                ContentKey = "moba.tests.p2_showcase",
                Owner = "moba-demo-tests",
                Tags = new[] { "moba", "test", "p2", "showcase" },
                AssetName = "MOBA P2 Skill Programming Showcase",
            };
            if (!TriggerAuthoringModuleSourceImportApi.Import(
                    module, project, sourcePath, options, out var message))
            {
                throw new InvalidDataException(message);
            }
        }

        private static void SyncSkillFlow()
        {
            var sourcePath = ResolveProjectPath(FlowSourceAssetPath);
            var array = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
                File.ReadAllText(sourcePath), typeof(SkillFlowDTO));
            if (array == null || array.Length != 1 || !(array.GetValue(0) is SkillFlowDTO dto) || dto.Id != FlowId)
                throw new InvalidDataException("P2 showcase source must contain exactly one reserved skill flow.");

            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SkillFlowSO>();
                AssetDatabase.CreateAsset(asset, FlowAssetPath);
            }
            asset.dataList = new[] { SkillFlowDef.FromDto(dto) };
            asset.legacyDataList = Array.Empty<SkillFlowDTO>();
            asset.name = "MOBA P2 Derived Capture Showcase";
            EditorUtility.SetDirty(asset);
        }

        private static string ResolveProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
#endif
