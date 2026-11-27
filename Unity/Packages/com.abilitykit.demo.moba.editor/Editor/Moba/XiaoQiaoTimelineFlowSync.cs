namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 保留历史同步方法名以兼容批处理；不再暴露为单技能菜单。
    /// </summary>
    public static class XiaoQiaoTimelineFlowSync
    {
        /// <summary>
        /// 转交正式发布器处理全部技能流程，并非小乔一技能的定向同步。
        /// </summary>
        public static void Sync() => MobaSkillPipelineExporter.Apply();
    }
}
