using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public readonly struct SearchHit
    {
        public readonly EcsEntityId Id;
        public readonly float Score;
        public readonly ulong Key;

        public SearchHit(EcsEntityId id, float score, ulong key)
        {
            Id = id;
            Score = score;
            Key = key;
        }
    }
}
