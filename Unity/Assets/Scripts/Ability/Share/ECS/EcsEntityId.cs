namespace AbilityKit.Ability.Share.ECS
{
    public readonly struct EcsEntityId
    {
        public readonly int ActorId;

        public EcsEntityId(int actorId)
        {
            ActorId = actorId;
        }

        public bool IsValid => ActorId > 0;

        public override string ToString() => ActorId.ToString();
    }
}
