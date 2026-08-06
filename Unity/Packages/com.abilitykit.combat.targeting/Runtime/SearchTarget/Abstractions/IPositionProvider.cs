namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 位置提供者接口
    /// </summary>
    public interface IPositionProvider
    {
        bool TryGetPosition(EntityId entity, out Vec2 position);
    }
}
