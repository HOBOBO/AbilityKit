using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Events.Summon
{
    /// <summary>
    /// 召唤物事件负载
    /// </summary>
    public sealed class SummonEventPayload : IMobaActorContextProvider, IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider, IMobaPersistentContextSourceProvider
    {
        /// <summary>召唤物 ActorId</summary>
        public int SummonActorId;

        /// <summary>召唤物 ID</summary>
        public int SummonId;

        /// <summary>召唤者 ActorId</summary>
        public int OwnerActorId;

        /// <summary>根召唤者 ActorId</summary>
        public int RootOwnerActorId;

        /// <summary>原因</summary>
        public int Reason;

        public SummonSourceContext SourceContext;

        public int SourceActorId => OwnerActorId;
        public int TargetActorId => SummonActorId;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = OwnerActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = SummonActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (SourceContext.TryGetOrigin(out origin) && origin.IsValid)
            {
                return true;
            }

            origin = MobaGameplayOriginBuilder.Create()
                .WithActors(OwnerActorId, SummonActorId)
                .WithImmediate(MobaExecutionKind.SummonSpawn, SummonId, 0)
                .Build();
            return origin.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (SourceContext.TryGetLineageContext(out lineageContext))
            {
                return true;
            }

            lineageContext = new MobaTriggerLineageContext(
                EffectContextKind.Unknown,
                MobaExecutionKind.SummonSpawn,
                OwnerActorId,
                SummonActorId,
                0,
                0,
                0,
                SummonId);
            return OwnerActorId > 0 || SummonActorId > 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (SourceContext.TryGetContextSource(out source) && source.IsValid)
            {
                return true;
            }

            source = default;
            return false;
        }

        public bool TryGetPersistentContextSource(out MobaPersistentContextSourceSnapshot snapshot)
        {
            if (SourceContext.TryGetPersistentContextSource(out snapshot) && snapshot.IsValid)
            {
                return true;
            }

            snapshot = default;
            return false;
        }
    }
}
