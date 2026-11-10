#if UNITY_EDITOR
using System;
using System.Reflection;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaMechanismPublisher
    {
        public static void PublishAndCheck()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MobaStateMachinePublisher.Publish();
            MobaStateMachinePublisher.Check();
            Invoke("ImportProductionSourcesBatch");
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            Invoke("PublishRuntimeBatch");
            Invoke("CheckRuntimeBatch");
            AssetDatabase.SaveAssets();
            Debug.Log("[MobaMechanismPublisher] All authoritative publishers and checks completed.");
        }
        private static void Invoke(string name)
        {
            var type = typeof(TriggerAuthoringProjectAsset).Assembly.GetType("AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration", true);
            try { type.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException e) { throw new InvalidOperationException(name + " failed.", e.InnerException); }
        }
    }
}
#endif
