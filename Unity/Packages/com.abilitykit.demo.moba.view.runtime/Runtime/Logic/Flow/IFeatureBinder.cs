namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 抽象 runtime 级别的 feature 绑定，使 flow 逻辑可以在不依赖具体组件存储实现的情况下测试。
    /// </summary>
    public interface IFeatureBinder
    {
        void AttachFeature(object feature);
        void DetachFeature(object feature);
    }
}
