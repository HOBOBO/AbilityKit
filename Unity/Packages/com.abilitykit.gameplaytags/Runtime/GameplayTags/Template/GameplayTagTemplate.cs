using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签模板，对标 GAS 的 GameplayEffect 中的标签配置。
    /// 用于批量管理标签集合。
    /// </summary>
    public class GameplayTagTemplate
    {
        private readonly List<GameplayTag> _grantTags;
        private readonly List<GameplayTag> _removeTags;
        private readonly List<GameplayTag> _requiredTags;
        private readonly List<GameplayTag> _blockedTags;
        private string _description;

        /// <summary>
        /// 创建标签模板
        /// </summary>
        public GameplayTagTemplate()
        {
            _grantTags = new List<GameplayTag>();
            _removeTags = new List<GameplayTag>();
            _requiredTags = new List<GameplayTag>();
            _blockedTags = new List<GameplayTag>();
            _description = string.Empty;
        }

        /// <summary>
        /// 创建标签模板
        /// </summary>
        public GameplayTagTemplate(
            IEnumerable<GameplayTag> grantTags = null,
            IEnumerable<GameplayTag> removeTags = null,
            IEnumerable<GameplayTag> requiredTags = null,
            IEnumerable<GameplayTag> blockedTags = null,
            string description = null)
        {
            _grantTags = grantTags != null ? new List<GameplayTag>(grantTags) : new List<GameplayTag>();
            _removeTags = removeTags != null ? new List<GameplayTag>(removeTags) : new List<GameplayTag>();
            _requiredTags = requiredTags != null ? new List<GameplayTag>(requiredTags) : new List<GameplayTag>();
            _blockedTags = blockedTags != null ? new List<GameplayTag>(blockedTags) : new List<GameplayTag>();
            _description = description ?? string.Empty;
        }

        /// <summary>
        /// 授予的标签列表
        /// </summary>
        public IReadOnlyList<GameplayTag> GrantTags => _grantTags;

        /// <summary>
        /// 移除的标签列表
        /// </summary>
        public IReadOnlyList<GameplayTag> RemoveTags => _removeTags;

        /// <summary>
        /// 标签需求条件
        /// </summary>
        public GameplayTagRequirements Requirements
        {
            get
            {
                var required = new GameplayTagContainer();
                var blocked = new GameplayTagContainer();
                foreach (var tag in _requiredTags) required.Add(tag);
                foreach (var tag in _blockedTags) blocked.Add(tag);
                return new GameplayTagRequirements(required, blocked);
            }
        }

        /// <summary>
        /// 模板描述
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// 添加授予标签
        /// </summary>
        public void AddGrantTag(GameplayTag tag)
        {
            if (tag.IsValid && !_grantTags.Contains(tag))
            {
                _grantTags.Add(tag);
            }
        }

        /// <summary>
        /// 添加移除标签
        /// </summary>
        public void AddRemoveTag(GameplayTag tag)
        {
            if (tag.IsValid && !_removeTags.Contains(tag))
            {
                _removeTags.Add(tag);
            }
        }

        /// <summary>
        /// 添加必须标签
        /// </summary>
        public void AddRequiredTag(GameplayTag tag)
        {
            if (tag.IsValid && !_requiredTags.Contains(tag))
            {
                _requiredTags.Add(tag);
            }
        }

        /// <summary>
        /// 添加禁止标签
        /// </summary>
        public void AddBlockedTag(GameplayTag tag)
        {
            if (tag.IsValid && !_blockedTags.Contains(tag))
            {
                _blockedTags.Add(tag);
            }
        }

        /// <summary>
        /// 获取授予标签容器
        /// </summary>
        public GameplayTagContainer GetGrantContainer()
        {
            var container = new GameplayTagContainer();
            foreach (var tag in _grantTags)
            {
                container.Add(tag);
            }
            return container;
        }

        /// <summary>
        /// 获取移除标签容器
        /// </summary>
        public GameplayTagContainer GetRemoveContainer()
        {
            var container = new GameplayTagContainer();
            foreach (var tag in _removeTags)
            {
                container.Add(tag);
            }
            return container;
        }

        /// <summary>
        /// 创建运行时模板实例（用于动态模板）
        /// </summary>
        public static GameplayTagTemplate CreateRuntime(
            IEnumerable<GameplayTag> grantTags = null,
            IEnumerable<GameplayTag> removeTags = null,
            IEnumerable<GameplayTag> requiredTags = null,
            IEnumerable<GameplayTag> blockedTags = null,
            string description = null)
        {
            return new GameplayTagTemplate(grantTags, removeTags, requiredTags, blockedTags, description);
        }
    }
}