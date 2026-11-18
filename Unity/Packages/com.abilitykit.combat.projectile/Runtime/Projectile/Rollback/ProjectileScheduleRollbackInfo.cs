namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 活动任务的隔离关系摘要，供同份快照的 ECS 与业务关系预检使用。
    /// 不表示所有 Launcher 都有活动任务；已完成任务的 Launcher 可以继续等待弹体结束。
    /// </summary>
    public readonly struct ProjectileScheduleRollbackInfo
    {
        /// <summary>
        /// 任务身份。
        /// </summary>
        public readonly int Id;
        /// <summary>
        /// 发射器对象身份。
        /// </summary>
        public readonly int LauncherActorId;
        /// <summary>
        /// 根角色身份。
        /// </summary>
        public readonly int RootActorId;
        /// <summary>
        /// 投射物模板身份。
        /// </summary>
        public readonly int TemplateId;
        /// <summary>
        /// 调度间隔帧数。
        /// </summary>
        public readonly int IntervalFrames;
        /// <summary>
        /// 发射策略的纯数据描述。
        /// </summary>
        public readonly ProjectileSchedulePatternSnapshot Pattern;

        internal ProjectileScheduleRollbackInfo(in ProjectileScheduleSnapshot s)
        {
            Id = s.Id; LauncherActorId = s.BaseSpawn.LauncherActorId; RootActorId = s.BaseSpawn.RootActorId;
            TemplateId = s.BaseSpawn.TemplateId; IntervalFrames = s.IntervalFrames; Pattern = s.Pattern;
        }
    }
}
