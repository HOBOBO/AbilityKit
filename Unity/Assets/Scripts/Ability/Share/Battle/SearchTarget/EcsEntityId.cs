namespace AbilityKit.Ability.Share.Battle.SearchTarget.Legacy
{
    [System.Obsolete("Use AbilityKit.Ability.Share.ECS.EcsEntityId instead.")]
    public readonly struct EcsEntityId
    {
        public readonly int ActorId;

        public EcsEntityId(int actorId)
        {
            ActorId = actorId;
        }

        public bool IsValid => ActorId > 0;

        public override string ToString()
        {
            return ActorId.ToString();
        }
    }
}
