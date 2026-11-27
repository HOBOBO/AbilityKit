namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标映射器接口
    /// </summary>
    public interface ITargetMapper<T>
    {
        bool TryMap(SearchContext context, EntityId id, out T result);
    }

    /// <summary>
    /// 实体句柄集合接口。
    /// </summary>
    public interface IEntityIdSet
    {
        bool Contains(EntityId id);
        int Count { get; }
    }

    /// <summary>
    /// 可选的实体等价键提供者。未提供时使用 <see cref="EntityId.Value"/>。
    /// </summary>
    public interface IEntityKeyProvider
    {
        ulong GetKey(EntityId id);
    }

    /// <summary>
    /// 目标 ID 查询的可选统计接口；回调在发起查询的执行链中同步运行，异常向调用方传播。
    /// </summary>
    public interface ISearchStats
    {
        /// <summary>
        /// 在开始一次查询、遍历候选之前重置统计。
        /// </summary>
        void Reset();

        /// <summary>
        /// 每收到一个最终提供者推送的候选时调用，包括无效或重复的候选。
        /// </summary>
        void OnCandidate();

        /// <summary>
        /// 候选通过校验、规则、评分与去重时调用，不要求候选最终入选。
        /// </summary>
        void OnHit();

        /// <summary>
        /// 目标 ID 选择成功后报告一次输出数量，包括零结果；不统计后续映射成功数。
        /// </summary>
        /// <param name='count'>选择阶段写入的实体句柄数。</param>
        void OnResult(int count);
    }

    /// <summary>
    /// 候选评估阶段的决定，不表示最终选择结果。
    /// </summary>
    public enum SearchCandidateDecision
    {
        /// <summary>
        /// 候选的实体句柄无效。
        /// </summary>
        Invalid = 1,

        /// <summary>
        /// 显式按实体键去重时，该键已被另一合格候选占用。
        /// </summary>
        Duplicate = 2,

        /// <summary>
        /// 候选未通过查询规则。
        /// </summary>
        RuleRejected = 3,

        /// <summary>
        /// 至少一个排序项返回非数值分数，候选不可评分。
        /// </summary>
        ScoreUnavailable = 4,

        /// <summary>
        /// 候选合格，可参与选择，但不保证写入结果。
        /// </summary>
        Eligible = 5
    }

    /// <summary>
    /// 在基础统计之外，同步观察引擎对每个候选作出的评估决定。
    /// </summary>
    public interface ISearchDetailedStats : ISearchStats
    {
        /// <summary>
        /// 报告候选的评估结果；此回调抛出的异常被隔离，继承的基础统计回调不适用该隔离。
        /// </summary>
        /// <param name='id'>本次评估的候选句柄，可能无效。</param>
        /// <param name='decision'>候选的评估决定。</param>
        /// <param name='ruleIndex'>首个拒绝候选的规则索引，其他决定为负一。</param>
        /// <param name='ruleName'>拒绝规则的类型名，其他决定为空引用。</param>
        /// <param name='primaryScore'>合格候选的首项评分；没有排序项或未合格时为空。</param>
        void OnDecision(EntityId id, SearchCandidateDecision decision, int ruleIndex, string ruleName,
            float? primaryScore);
    }
}
