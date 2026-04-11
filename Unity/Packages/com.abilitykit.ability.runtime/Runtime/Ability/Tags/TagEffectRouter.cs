using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.GameplayTags;
using IGameplayTagService = AbilityKit.GameplayTags.IGameplayTagService;
using ITagEffectRouter = AbilityKit.GameplayTags.ITagEffectRouter;
using ITagChangeSubscriber = AbilityKit.GameplayTags.ITagChangeSubscriber;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;
using GameplayTagDelta = AbilityKit.GameplayTags.GameplayTagDelta;
using GameplayTagSource = AbilityKit.GameplayTags.GameplayTagSource;

namespace AbilityKit.Ability.Tags
{
    /// <summary>
    /// 标签变更事件路由器
    /// 负责将标签变更事件分发给所有已注册的订阅者
    /// </summary>
    public sealed class TagEffectRouter : ITagEffectRouter, IWorldInitializable
    {
        private readonly List<ITagChangeSubscriber> _subscribers = new List<ITagChangeSubscriber>(8);

        private IGameplayTagService _tagService;

        public void OnInit(IWorldResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            _tagService = services.Resolve<IGameplayTagService>();
            _tagService.TagsChanged += OnTagsChanged;
        }

        public void Dispose()
        {
            if (_tagService != null)
            {
                _tagService.TagsChanged -= OnTagsChanged;
            }

            _subscribers.Clear();
            _tagService = null;
        }

        public void Register(ITagChangeSubscriber subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        public bool Unregister(ITagChangeSubscriber subscriber)
        {
            if (subscriber == null) return false;
            return _subscribers.Remove(subscriber);
        }

        public IReadOnlyList<ITagChangeSubscriber> GetSubscribers()
        {
            return _subscribers;
        }

        private void OnTagsChanged(int ownerId, GameplayTagDelta delta, GameplayTagSource source)
        {
            if (_tagService == null) return;

            var currentTags = _tagService.GetTags(ownerId);
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i]?.OnTagsChanged(ownerId, currentTags, delta, source);
                }
                catch
                {
                    // keep router resilient
                }
            }
        }
    }
}
