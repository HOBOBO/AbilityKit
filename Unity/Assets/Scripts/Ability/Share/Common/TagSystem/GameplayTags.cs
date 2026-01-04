namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public static class GameplayTags
    {
        public static GameplayTag Tag(string name)
        {
            return GameplayTagManager.Instance.RequestTag(name);
        }

        public static bool TryGet(string name, out GameplayTag tag)
        {
            return GameplayTagManager.Instance.TryGetTag(name, out tag);
        }
    }
}
