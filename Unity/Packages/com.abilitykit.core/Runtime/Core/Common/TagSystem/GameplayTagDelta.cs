namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public readonly struct GameplayTagDelta
    {
        public readonly GameplayTagContainer Added;
        public readonly GameplayTagContainer Removed;

        public GameplayTagDelta(GameplayTagContainer added, GameplayTagContainer removed)
        {
            Added = added;
            Removed = removed;
        }

        public bool IsEmpty
        {
            get
            {
                var addedEmpty = Added == null || Added.Count == 0;
                var removedEmpty = Removed == null || Removed.Count == 0;
                return addedEmpty && removedEmpty;
            }
        }
    }
}
