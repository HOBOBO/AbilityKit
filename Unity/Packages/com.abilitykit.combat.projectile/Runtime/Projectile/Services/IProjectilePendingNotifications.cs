namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 只读查询尚未消费的投射物和区域通知，不包含未来发射任务。
    /// 查询不会清空队列，也不用于保存或重放通知。
    /// </summary>
    public interface IProjectilePendingNotifications
    {
        /// <summary>
        /// 所有类别当前待消费项总数，包含到达通知。
        /// </summary>
        int PendingNotificationCount { get; }
    }
}
