namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 单次查询的合格命中，内部评分仍依赖查询的临时缓冲区。
    /// </summary>
    public readonly struct SearchHit
    {
        /// <summary>
        /// 候选的实体句柄，可独立复制。
        /// </summary>
        public readonly EntityId Id;

        /// <summary>
        /// 本次查询解析到的实体键，用于同分排序和显式去重。
        /// </summary>
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
