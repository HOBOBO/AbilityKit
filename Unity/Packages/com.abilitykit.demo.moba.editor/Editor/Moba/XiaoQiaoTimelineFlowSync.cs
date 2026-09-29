using UnityEditor;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class XiaoQiaoTimelineFlowSync
    {
        [MenuItem("Tools/AbilityKit/Demos/Moba/ActionEditor/Sync XiaoQiao Skill 1 Flow")]
        public static void Sync() => MobaSkillPipelineExporter.Apply();
    }
}
