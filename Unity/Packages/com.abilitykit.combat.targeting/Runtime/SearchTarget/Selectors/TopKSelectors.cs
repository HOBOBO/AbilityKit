using System;

namespace AbilityKit.Battle.SearchTarget.Selectors
{
    /// <summary>
    /// 前若干个评分结果选择器。
    /// </summary>
    [TargetSelector(0x1001, "TopKByScore")]
    public sealed class TopKByScoreSelector : ITargetSelector
    {
        public void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results)
        {
            SearchOrdering.Sort(hits.MutableHits, in query);

            if (query.HasMaxCount)
            {
                var count = query.MaxCount;
                if (count > hits.Count) count = hits.Count;
                for (int i = 0; i < count; i++)
                {
                    results.Add(hits[i].Id);
                }
                return;
            }

            for (int i = 0; i < hits.Count; i++)
            {
                results.Add(hits[i].Id);
            }
        }

    }

    /// <summary>
    /// 使用查询内局部缓冲区计算前若干个评分结果，选择器实例本身不保存执行状态。
    /// </summary>
    [TargetSelector(0x1002, "StreamingTopKByScore")]
    public sealed class StreamingTopKByScoreSelector : IStreamingTopKByScoreSelector
    {
        public void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results)
        {
            if (!query.HasMaxCount)
            {
                SearchOrdering.Sort(hits.MutableHits, in query);
                AddAll(hits, results);
                return;
            }

            var count = Math.Min(query.MaxCount, hits.Count);
            if (count == 0) return;

            var buffer = TargetingPool.RentHitBuffer(count);
            try
            {
                var selectedCount = 0;
                var items = buffer.Items;
                for (int i = 0; i < hits.Count; i++)
                {
                    Offer(in query, items, ref selectedCount, count, hits[i]);
                }

                for (int i = 0; i < selectedCount; i++)
                {
                    results.Add(items[i].Id);
                }
            }
            finally
            {
                TargetingPool.ReleaseHitBuffer(buffer);
            }
        }

        private static void AddAll(SearchHitView hits, SearchResultWriter results)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                results.Add(hits[i].Id);
            }
        }

        private static void Offer(
            in SearchQuery query,
            SearchHit[] items,
            ref int selectedCount,
            int capacity,
            in SearchHit hit)
        {
            var insertIndex = 0;
            while (insertIndex < selectedCount && SearchOrdering.IsBetter(in query, in items[insertIndex], in hit))
            {
                insertIndex++;
            }

            if (insertIndex >= capacity) return;

            var lastIndex = selectedCount < capacity ? selectedCount : capacity - 1;
            for (int i = lastIndex; i > insertIndex; i--)
            {
                items[i] = items[i - 1];
            }

            items[insertIndex] = hit;
            if (selectedCount < capacity) selectedCount++;
        }

    }
}
