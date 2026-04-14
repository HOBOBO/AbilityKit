namespace AbilityKit.Triggering.Runtime.Config.Cue
{
    /// <summary>
    /// Cue 配置接口（静态配置数据）
    /// </summary>
    public interface ICueConfig
    {
        ECueKind Kind { get; }
        string VfxId { get; }
        string SfxId { get; }
        string ExtraData { get; }
        bool IsEmpty { get; }
    }
}