namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public sealed class TagTemplateRuntime
    {
        public int Id { get; }
        public string Name { get; }

        public GameplayTagRequirements Requirements { get; }
        public GameplayTagContainer GrantTags { get; }
        public GameplayTagContainer RemoveTags { get; }

        public TagTemplateRuntime(int id, string name, GameplayTagRequirements requirements, GameplayTagContainer grantTags, GameplayTagContainer removeTags)
        {
            Id = id;
            Name = name;
            Requirements = requirements;
            GrantTags = grantTags;
            RemoveTags = removeTags;
        }
    }
}
