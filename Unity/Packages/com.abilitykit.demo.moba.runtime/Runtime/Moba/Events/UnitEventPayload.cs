using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct UnitEventPayload
    {
        public readonly int ActorId;
        public readonly Team Team;
        public readonly EntityMainType MainType;
        public readonly UnitSubType UnitSubType;
        public readonly PlayerId OwnerPlayerId;
        public readonly int TemplateId;

        public UnitEventPayload(int actorId, Team team, EntityMainType mainType, UnitSubType unitSubType, PlayerId ownerPlayerId, int templateId)
        {
            ActorId = actorId;
            Team = team;
            MainType = mainType;
            UnitSubType = unitSubType;
            OwnerPlayerId = ownerPlayerId;
            TemplateId = templateId;
        }
    }
}
