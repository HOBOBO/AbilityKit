namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface ITagChangeSubscriber
    {
        void OnTagsChanged(int ownerId, GameplayTagContainer currentTags, GameplayTagDelta delta, TagSource source);
    }
}
