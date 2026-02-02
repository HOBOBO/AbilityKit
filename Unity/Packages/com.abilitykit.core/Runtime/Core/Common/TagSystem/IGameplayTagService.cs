using System;

namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface IGameplayTagService
    {
        event Action<int, GameplayTagDelta, TagSource> TagsChanged;

        GameplayTagContainer GetTags(int ownerId);

        bool AddTag(int ownerId, GameplayTag tag, TagSource source);
        bool RemoveTag(int ownerId, GameplayTag tag, TagSource source);

        bool ApplyTemplate(int ownerId, int templateId, TagSource source, bool checkRequirements = false);

        void ClearOwner(int ownerId);
    }
}
