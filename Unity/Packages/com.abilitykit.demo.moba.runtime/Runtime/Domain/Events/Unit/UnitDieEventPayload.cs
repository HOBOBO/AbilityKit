using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Events.Unit
{
    /// <summary>
    /// 单位死亡事件负载
    /// </summary>
    public readonly struct UnitDieEventPayload : IMobaActorContextProvider, IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider
    {
        /// <summary>单位 ActorId</summary>
        public readonly int ActorId;

        /// <summary>击杀者 ActorId</summary>
        public readonly int KillerActorId;

        /// <summary>伤害类型</summary>
        public readonly int DamageType;

        /// <summary>死亡原因类型</summary>
        public readonly int ReasonKind;

        /// <summary>死亡原因参数</summary>
        public readonly int ReasonParam;

        /// <summary>伤害值</summary>
        public readonly float DamageValue;

        public readonly MobaGameplayOrigin Origin;

        public UnitDieEventPayload(int actorId, int killerActorId, int damageType, int reasonKind, int reasonParam, float damageValue)
            : this(actorId, killerActorId, damageType, reasonKind, reasonParam, damageValue, default)
        {
        }

        public UnitDieEventPayload(int actorId, int killerActorId, int damageType, int reasonKind, int reasonParam, float damageValue, in MobaGameplayOrigin origin)
        {
            ActorId = actorId;
            KillerActorId = killerActorId;
            DamageType = damageType;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            DamageValue = damageValue;
            Origin = origin.IsValid ? origin.WithImmediate(MobaExecutionKind.UnitDeath, reasonParam, origin.EffectiveParentContextId) : default;
        }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = KillerActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = ActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (Origin.IsValid)
            {
                origin = Origin;
                return true;
            }

            origin = MobaGameplayOriginBuilder.Create()
                .WithActors(KillerActorId, ActorId)
                .WithImmediate(MobaExecutionKind.UnitDeath, ReasonParam, 0)
                .Build();
            return origin.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (TryGetOrigin(out var origin) && origin.IsValid)
            {
                lineageContext = origin.ToLineageContext(EffectContextKind.Unit);
                return true;
            }

            lineageContext = new MobaTriggerLineageContext(EffectContextKind.Unit, MobaExecutionKind.UnitDeath, KillerActorId, ActorId, 0, 0, 0, ReasonParam);
            return KillerActorId > 0 || ActorId > 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.UnitDeath,
                    runtimeConfigId: ReasonParam);
                return source.IsValid;
            }

            source = default;
            return false;
        }
    }
}
