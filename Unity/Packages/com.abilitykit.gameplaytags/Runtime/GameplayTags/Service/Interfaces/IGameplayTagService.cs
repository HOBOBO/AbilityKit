using System;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签服务接口，用于管理实体的标签状态。
    /// </summary>
    public interface IGameplayTagService
    {
        /// <summary>
        /// 标签变化事件
        /// </summary>
        event Action<int, GameplayTagDelta, GameplayTagSource> TagsChanged;

        /// <summary>
        /// 获取实体的所有标签
        /// </summary>
        GameplayTagContainer GetTags(int ownerId);

        /// <summary>
        /// 添加单个标签
        /// </summary>
        bool AddTag(int ownerId, GameplayTag tag, GameplayTagSource source);

        /// <summary>
        /// 移除单个标签
        /// </summary>
        bool RemoveTag(int ownerId, GameplayTag tag, GameplayTagSource source);

        /// <summary>
        /// 应用标签模板（通过模板对象）
        /// </summary>
        bool ApplyTemplate(int ownerId, GameplayTagTemplate template, GameplayTagSource source, bool checkRequirements = false);

        /// <summary>
        /// 移除标签模板（通过模板对象）
        /// </summary>
        bool RemoveTemplate(int ownerId, GameplayTagTemplate template, GameplayTagSource source);

        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        bool HasTag(int ownerId, GameplayTag tag, bool exact = false);

        /// <summary>
        /// 清空实体的所有标签
        /// </summary>
        void ClearOwner(int ownerId);
    }

    /// <summary>
    /// 基于模板 ID 的标签服务扩展接口。
    /// 用于需要通过模板 ID 进行操作的场景。
    /// </summary>
    public interface ITaggedEntityService : IGameplayTagService
    {
        /// <summary>
        /// 应用标签模板（通过模板 ID）
        /// </summary>
        bool ApplyTemplate(int ownerId, int templateId, GameplayTagSource source, bool checkRequirements = false);
    }
}
