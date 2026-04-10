using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签服务默认实现。
    /// </summary>
    public sealed class GameplayTagService : IGameplayTagService
    {
        public static GameplayTagService Instance { get; } = new GameplayTagService();

        public event Action<int, GameplayTagDelta, GameplayTagSource> TagsChanged;

        private readonly Dictionary<int, GameplayTagContainer> _ownerTags = new Dictionary<int, GameplayTagContainer>();
        private readonly Dictionary<int, Dictionary<int, GameplayTagTemplate>> _ownerTemplates = new Dictionary<int, Dictionary<int, GameplayTagTemplate>>();

        private int _templateIdCounter = 1;

        public GameplayTagContainer GetTags(int ownerId)
        {
            if (_ownerTags.TryGetValue(ownerId, out var tags))
            {
                return tags;
            }
            return null;
        }

        public bool AddTag(int ownerId, GameplayTag tag, GameplayTagSource source)
        {
            if (!tag.IsValid) return false;

            if (!_ownerTags.TryGetValue(ownerId, out var tags))
            {
                tags = new GameplayTagContainer();
                _ownerTags[ownerId] = tags;
            }

            if (tags.HasTagExact(tag)) return false;

            var added = new GameplayTagContainer();
            added.Add(tag);
            tags.Add(tag);

            TagsChanged?.Invoke(ownerId, new GameplayTagDelta(added, null), source);
            return true;
        }

        public bool RemoveTag(int ownerId, GameplayTag tag, GameplayTagSource source)
        {
            if (!tag.IsValid) return false;

            if (!_ownerTags.TryGetValue(ownerId, out var tags))
            {
                return false;
            }

            if (!tags.HasTagExact(tag)) return false;

            var removed = new GameplayTagContainer();
            removed.Add(tag);
            tags.Remove(tag);

            TagsChanged?.Invoke(ownerId, new GameplayTagDelta(null, removed), source);
            return true;
        }

        public bool ApplyTemplate(int ownerId, GameplayTagTemplate template, GameplayTagSource source, bool checkRequirements = false)
        {
            if (template == null) return false;

            if (!_ownerTags.TryGetValue(ownerId, out var currentTags))
            {
                currentTags = new GameplayTagContainer();
                _ownerTags[ownerId] = currentTags;
            }

            if (checkRequirements && !template.Requirements.IsSatisfiedBy(currentTags))
            {
                return false;
            }

            int instanceId = _templateIdCounter++;

            if (!_ownerTemplates.TryGetValue(ownerId, out var templates))
            {
                templates = new Dictionary<int, GameplayTagTemplate>();
                _ownerTemplates[ownerId] = templates;
            }

            templates[instanceId] = template;

            var added = new GameplayTagContainer();
            if (template.GrantTags != null)
            {
                foreach (var tag in template.GrantTags)
                {
                    if (!currentTags.HasTagExact(tag))
                    {
                        currentTags.Add(tag);
                        added.Add(tag);
                    }
                }
            }

            var removed = new GameplayTagContainer();
            if (template.RemoveTags != null)
            {
                foreach (var tag in template.RemoveTags)
                {
                    if (currentTags.HasTagExact(tag))
                    {
                        currentTags.Remove(tag);
                        removed.Add(tag);
                    }
                }
            }

            if (!added.IsEmpty || !removed.IsEmpty)
            {
                TagsChanged?.Invoke(ownerId, new GameplayTagDelta(added, removed), source);
            }

            return true;
        }

        public bool RemoveTemplate(int ownerId, GameplayTagTemplate template, GameplayTagSource source)
        {
            if (template == null) return false;

            if (!_ownerTemplates.TryGetValue(ownerId, out var templates))
            {
                return false;
            }

            int foundId = -1;
            foreach (var kvp in templates)
            {
                if (kvp.Value == template)
                {
                    foundId = kvp.Key;
                    break;
                }
            }

            if (foundId < 0) return false;

            templates.Remove(foundId);

            if (!_ownerTags.TryGetValue(ownerId, out var tags))
            {
                return true;
            }

            var removed = new GameplayTagContainer();
            if (template.GrantTags != null && template.GrantTags.Count > 0)
            {
                foreach (var tag in template.GrantTags)
                {
                    bool stillHas = false;
                    foreach (var otherKvp in templates)
                    {
                        if (otherKvp.Value == template) continue;
                        foreach (var otherTag in otherKvp.Value.GrantTags)
                        {
                            if (otherTag == tag)
                            {
                                stillHas = true;
                                break;
                            }
                        }
                        if (stillHas) break;
                    }

                    if (!stillHas && tags.HasTagExact(tag))
                    {
                        tags.Remove(tag);
                        removed.Add(tag);
                    }
                }
            }

            if (!removed.IsEmpty)
            {
                TagsChanged?.Invoke(ownerId, new GameplayTagDelta(null, removed), source);
            }

            return true;
        }

        public bool HasTag(int ownerId, GameplayTag tag, bool exact = false)
        {
            if (!_ownerTags.TryGetValue(ownerId, out var tags))
            {
                return false;
            }

            return exact ? tags.HasTagExact(tag) : tags.HasTag(tag);
        }

        public void ClearOwner(int ownerId)
        {
            if (!_ownerTags.TryGetValue(ownerId, out var currentTags))
            {
                return;
            }

            if (currentTags.IsEmpty) return;

            var removed = new GameplayTagContainer();
            removed.AppendTags(currentTags);
            currentTags.Clear();

            _ownerTemplates.Remove(ownerId);

            TagsChanged?.Invoke(ownerId, new GameplayTagDelta(null, removed), GameplayTagSource.System);
        }
    }
}
