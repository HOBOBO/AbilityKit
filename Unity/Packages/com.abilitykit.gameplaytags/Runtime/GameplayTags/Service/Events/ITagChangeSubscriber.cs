using System;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签变化订阅者接口。
    /// </summary>
    public interface ITagChangeSubscriber
    {
        /// <summary>
        /// 当标签发生变化时调用
        /// </summary>
        void OnTagsChanged(int ownerId, GameplayTagContainer currentTags, GameplayTagDelta delta, GameplayTagSource source);
    }
}
