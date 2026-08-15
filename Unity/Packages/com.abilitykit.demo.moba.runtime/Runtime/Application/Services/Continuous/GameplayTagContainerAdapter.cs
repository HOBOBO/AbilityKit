using System;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class GameplayTagContainerAdapter : ITagContainer
    {
        public static readonly GameplayTagContainerAdapter Empty = new GameplayTagContainerAdapter(null);

        private readonly GameplayTagContainer _container;

        private GameplayTagContainerAdapter(GameplayTagContainer container)
        {
            _container = container;
        }

        public static GameplayTagContainerAdapter From(GameplayTagContainer container)
        {
            return container == null || container.Count == 0 ? Empty : new GameplayTagContainerAdapter(container);
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            if (_container == null || _container.Count == 0) return false;

            foreach (var gameplayTag in _container)
            {
                if (string.Equals(gameplayTag.TagName, tag, StringComparison.Ordinal)) return true;
                if (string.Equals(gameplayTag.Value.ToString(), tag, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public bool HasAny(ITagContainer other)
        {
            if (_container == null || _container.Count == 0) return false;
            if (other == null || other.Count == 0) return false;

            if (other is GameplayTagContainerAdapter adapter && adapter._container != null)
            {
                return _container.HasAny(adapter._container);
            }

            foreach (var gameplayTag in _container)
            {
                if (other.HasTag(gameplayTag.TagName)) return true;
                if (other.HasTag(gameplayTag.Value.ToString())) return true;
            }

            return false;
        }

        public int Count => _container?.Count ?? 0;
    }
}
