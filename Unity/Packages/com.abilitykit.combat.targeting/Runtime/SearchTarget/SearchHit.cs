namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 搜索命中结果
    /// </summary>
    public readonly struct SearchHit
    {
        public readonly IEntityId Id;
        public readonly float Score;
        public readonly ulong Key;

        public SearchHit(IEntityId id, float score, ulong key)
        {
            Id = id;
            Score = score;
            Key = key;
        }
    }
}
