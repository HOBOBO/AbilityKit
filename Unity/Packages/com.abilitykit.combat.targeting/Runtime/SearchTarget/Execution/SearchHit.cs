namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 搜索命中结果
    /// </summary>
    public readonly struct SearchHit
    {
        public readonly EntityId Id;
        public readonly ulong Key;
        private readonly SearchScoreBuffer _scoreBuffer;
        private readonly int _scoreOffset;

        internal SearchHit(
            EntityId id,
            ulong key,
            SearchScoreBuffer scoreBuffer,
            int scoreOffset)
        {
            Id = id;
            Key = key;
            _scoreBuffer = scoreBuffer;
            _scoreOffset = scoreOffset;
        }

        internal float GetScore(int index)
        {
            return _scoreBuffer.Get(_scoreOffset, index, 0f);
        }
    }
}
