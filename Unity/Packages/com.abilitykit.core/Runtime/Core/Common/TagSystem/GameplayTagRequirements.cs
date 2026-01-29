namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public readonly struct GameplayTagRequirements
    {
        public readonly GameplayTagContainer Required;
        public readonly GameplayTagContainer Blocked;
        public readonly bool Exact;

        public GameplayTagRequirements(GameplayTagContainer required, GameplayTagContainer blocked, bool exact = false)
        {
            Required = required;
            Blocked = blocked;
            Exact = exact;
        }

        public bool IsSatisfiedBy(GameplayTagContainer tags)
        {
            if (tags == null) return false;

            if (Blocked != null && Blocked.Count > 0)
            {
                if (tags.HasAny(Blocked, Exact)) return false;
            }

            if (Required != null && Required.Count > 0)
            {
                if (!tags.HasAll(Required, Exact)) return false;
            }

            return true;
        }
    }
}
