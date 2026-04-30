namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 位置提供者接口
    /// </summary>
    public interface IPositionProvider
    {
        bool TryGetPosition(IEntityId entity, out IVec2 position);
    }
}
