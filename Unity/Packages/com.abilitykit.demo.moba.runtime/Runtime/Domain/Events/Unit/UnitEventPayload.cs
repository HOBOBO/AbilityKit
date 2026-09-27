using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Events.Unit
{
    /// <summary>
    /// 单位事件负载
    /// </summary>
    public readonly struct UnitEventPayload : IMobaActorContextProvider, IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider
    {
        /// <summary>单位 ActorId</summary>
        public readonly int ActorId;

        /// <summary>队伍</summary>
        public readonly Team Team;

        /// <summary>实体主类型</summary>
        public readonly EntityMainType MainType;

        /// <summary>单位子类型</summary>
        public readonly UnitSubType UnitSubType;

        /// <summary>所属玩家</summary>
        public readonly PlayerId OwnerPlayerId;

        /// <summary>模板 ID</summary>
        public readonly int TemplateId;

        public readonly MobaExecutionKind ExecutionKind;

        public UnitEventPayload(int actorId, Team team, EntityMainType mainType, UnitSubType unitSubType, PlayerId ownerPlayerId, int templateId)
            : this(actorId, team, mainType, unitSubType, ownerPlayerId, templateId, MobaExecutionKind.UnitSpawn)
        {
        }

        public UnitEventPayload(int actorId, Team team, EntityMainType mainType, UnitSubType unitSubType, PlayerId ownerPlayerId, int templateId, MobaExecutionKind executionKind)
        {
            ActorId = actorId;
            Team = team;
            MainType = mainType;
            UnitSubType = unitSubType;
            OwnerPlayerId = ownerPlayerId;
            TemplateId = templateId;
            ExecutionKind = executionKind != MobaExecutionKind.None ? executionKind : MobaExecutionKind.UnitSpawn;
        }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = ActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = ActorId;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = MobaGameplayOriginBuilder.Create()
                .WithActors(ActorId, ActorId)
                .WithImmediate(ExecutionKind, TemplateId, 0)
                .Build();
            return origin.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = new MobaTriggerLineageContext(EffectContextKind.Unit, ExecutionKind, ActorId, ActorId, 0, 0, 0, TemplateId);
            return ActorId > 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.Unit,
                    runtimeConfigId: TemplateId);
                return source.IsValid;
            }

            source = default;
            return false;
        }
    }
}
