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
    public sealed class TagEffectRouter : ITagEffectRouter, IWorldInitializable
    {
        private readonly List<ITagChangeSubscriber> _subs = new List<ITagChangeSubscriber>(8);

        private IGameplayTagService _tags;
        private IDurableRegistry _durables;

        private readonly DefaultDurableTagControlSubscriber _defaultDurable;

        public TagEffectRouter()
        {
            _defaultDurable = new DefaultDurableTagControlSubscriber();
        }

        public void OnInit(IWorldResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _tags = services.Resolve<IGameplayTagService>();
            _durables = services.Resolve<IDurableRegistry>();

            if (_defaultDurable != null)
            {
                _defaultDurable.Bind(_durables);
                Register(_defaultDurable);
            }

            _tags.TagsChanged += OnTagsChanged;
        }

        public void Dispose()
        {
            if (_tags != null)
            {
                _tags.TagsChanged -= OnTagsChanged;
            }

            _subs.Clear();
            _tags = null;
            _durables = null;
        }

        public void Register(ITagChangeSubscriber subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            if (!_subs.Contains(subscriber)) _subs.Add(subscriber);
        }

        public bool Unregister(ITagChangeSubscriber subscriber)
        {
            if (subscriber == null) return false;
            return _subs.Remove(subscriber);
        }

        public System.Collections.Generic.IReadOnlyList<ITagChangeSubscriber> GetSubscribers()
        {
            return _subs;
        }

        private void OnTagsChanged(int ownerId, GameplayTagDelta delta, GameplayTagSource source)
        {
            if (_tags == null) return;
            var current = _tags.GetTags(ownerId);
            for (int i = 0; i < _subs.Count; i++)
            {
                try
                {
                    _subs[i]?.OnTagsChanged(ownerId, current, delta, source);
                }
                catch
                {
                    // keep router resilient
                }
            }
        }

        private sealed class DefaultDurableTagControlSubscriber : ITagChangeSubscriber
        {
            private IDurableRegistry _durables;

            public void Bind(IDurableRegistry durables)
            {
                _durables = durables;
            }

            public void OnTagsChanged(int ownerId, GameplayTagContainer currentTags, GameplayTagDelta delta, GameplayTagSource source)
            {
                if (_durables == null) return;
                if (currentTags == null) return;

                var pauseKind = false;
                var stopKind = false;
                var removeKind = false;

                string kindFilter = null;

                foreach (var tag in currentTags)
                {
                    var name = GameplayTagManager.Instance.GetName(tag);
                    if (string.IsNullOrEmpty(name)) continue;

                    if (name.StartsWith("Control.Remove", StringComparison.Ordinal))
                    {
                        removeKind = true;
                        kindFilter = TryParseKindSuffix(name);
                        break;
                    }
                    if (name.StartsWith("Control.Stop", StringComparison.Ordinal))
                    {
                        stopKind = true;
                        kindFilter = kindFilter ?? TryParseKindSuffix(name);
                        // do not break; Remove might also exist
                    }
                    if (name.StartsWith("Control.Pause", StringComparison.Ordinal))
                    {
                        pauseKind = true;
                        kindFilter = kindFilter ?? TryParseKindSuffix(name);
                    }
                }

                if (!pauseKind && !stopKind && !removeKind) return;

                var list = _durables.GetByOwner(ownerId);
                for (int i = 0; i < list.Count; i++)
                {
                    var d = list[i];
                    if (d == null) continue;

                    if (!string.IsNullOrEmpty(kindFilter) && !string.Equals(d.Kind, kindFilter, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (removeKind)
                    {
                        if (!d.IsRemoved) d.Remove();
                        continue;
                    }

                    if (stopKind)
                    {
                        if (!d.IsStopped) d.Stop();
                    }

                    if (pauseKind)
                    {
                        if (!d.IsPaused) d.Pause();
                    }
                }
            }

            private static string TryParseKindSuffix(string tagName)
            {
                if (string.IsNullOrEmpty(tagName)) return null;

                // Example: Control.Pause.Buff
                var lastDot = tagName.LastIndexOf('.');
                if (lastDot <= 0 || lastDot >= tagName.Length - 1) return null;

                return tagName.Substring(lastDot + 1);
            }
        }
    }
}
