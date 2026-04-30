namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 候选提供者接口
    /// </summary>
    public interface ICandidateProvider
    {
        void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer;

        bool RequiresPosition { get; }
    }

    /// <summary>
    /// 候选消费者接口
    /// </summary>
    public interface ICandidateConsumer
    {
        void Consume(IEntityId id);
    }
}
