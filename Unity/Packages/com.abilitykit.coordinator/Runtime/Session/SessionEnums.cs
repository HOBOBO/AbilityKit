namespace AbilityKit.Coordinator.Core
{
    /// <summary>
    /// 同步模式枚举。
    /// </summary>
    public enum SyncMode
    {
        Lockstep = 0,
        SnapshotAuthority = 1,
        StateSync = 2,
        Hybrid = 3
    }

    /// <summary>
    /// 宿主模式枚举。
    /// </summary>
    public enum HostMode
    {
        Local = 0,
        Host = 1,
        Client = 2
    }
}
