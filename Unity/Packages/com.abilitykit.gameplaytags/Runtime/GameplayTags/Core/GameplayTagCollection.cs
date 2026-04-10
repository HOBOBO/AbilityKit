using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 游戏标签集合接口，提供统一的标签集合访问方式。
    /// 对标 Unreal Engine 的 FGameplayTagContainer 资产表示形式。
    /// </summary>
    public interface IGameplayTagCollection
    {
        /// <summary>
        /// 转换为标签容器
        /// </summary>
        GameplayTagContainer ToContainer();
    }

    /// <summary>
    /// 游戏标签集合，对标 Unreal Engine 的 GameplayTagCollection。
    /// 用于从标签集合配置中创建容器。
    /// </summary>
    [Serializable]
    public class GameplayTagCollection : IGameplayTagCollection
    {
        [NonSerialized]
        private List<GameplayTag> _tags = new List<GameplayTag>();

        /// <summary>
        /// 标签列表
        /// </summary>
        public IReadOnlyList<GameplayTag> Tags => _tags;

        /// <summary>
        /// 标签数量
        /// </summary>
        public int Count => _tags?.Count ?? 0;

        /// <summary>
        /// 创建空集合
        /// </summary>
        public GameplayTagCollection()
        {
            _tags = new List<GameplayTag>();
        }

        /// <summary>
        /// 从标签列表创建集合
        /// </summary>
        public GameplayTagCollection(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                _tags = new List<GameplayTag>();
            }
            else
            {
                _tags = new List<GameplayTag>(tags);
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public void Add(GameplayTag tag)
        {
            if (tag.IsValid && !_tags.Contains(tag))
            {
                _tags.Add(tag);
            }
        }

        /// <summary>
        /// 批量添加标签
        /// </summary>
        public void AddRange(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                Add(tag);
            }
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public bool Remove(GameplayTag tag)
        {
            return _tags.Remove(tag);
        }

        /// <summary>
        /// 清空所有标签
        /// </summary>
        public void Clear()
        {
            _tags.Clear();
        }

        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        public bool Contains(GameplayTag tag)
        {
            return _tags.Contains(tag);
        }

        /// <summary>
        /// 转换为标签容器
        /// </summary>
        public GameplayTagContainer ToContainer()
        {
            return new GameplayTagContainer(_tags);
        }

        /// <summary>
        /// 隐式转换为标签容器
        /// </summary>
        public static implicit operator GameplayTagContainer(GameplayTagCollection collection)
        {
            return collection?.ToContainer() ?? new GameplayTagContainer();
        }

        /// <summary>
        /// 从容器创建集合
        /// </summary>
        public static GameplayTagCollection FromContainer(GameplayTagContainer container)
        {
            if (container == null) return new GameplayTagCollection();
            return new GameplayTagCollection(container);
        }
    }
}
