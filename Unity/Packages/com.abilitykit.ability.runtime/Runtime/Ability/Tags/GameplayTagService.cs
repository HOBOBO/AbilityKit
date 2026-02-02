using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Tags
{
    public sealed class GameplayTagService : IGameplayTagService, IWorldInitializable
    {
        private sealed class OwnerState
        {
            public readonly GameplayTagContainer Tags = new GameplayTagContainer();
            public readonly Dictionary<int, Dictionary<long, int>> Refs = new Dictionary<int, Dictionary<long, int>>();
        }

        private readonly Dictionary<int, OwnerState> _owners = new Dictionary<int, OwnerState>();
        private ITagTemplateRegistry _templates;

        public event Action<int, GameplayTagDelta, TagSource> TagsChanged;

        public void OnInit(IWorldResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.TryResolve(out _templates);
        }

        public void Dispose()
        {
            _owners.Clear();
        }

        public GameplayTagContainer GetTags(int ownerId)
        {
            if (ownerId <= 0) return null;
            return GetOrCreate(ownerId).Tags;
        }

        public void ClearOwner(int ownerId)
        {
            if (ownerId <= 0) return;
            _owners.Remove(ownerId);
        }

        public bool AddTag(int ownerId, GameplayTag tag, TagSource source)
        {
            if (ownerId <= 0) return false;
            if (!tag.IsValid) return false;
            if (!source.IsValid) return false;

            var state = GetOrCreate(ownerId);
            if (!TryAddRef(state, tag.Value, source.Value, out var becamePresent))
            {
                return false;
            }

            if (!becamePresent)
            {
                return true;
            }

            state.Tags.Add(tag);

            var added = new GameplayTagContainer();
            added.Add(tag);
            Raise(ownerId, new GameplayTagDelta(added, null), source);
            return true;
        }

        public bool RemoveTag(int ownerId, GameplayTag tag, TagSource source)
        {
            if (ownerId <= 0) return false;
            if (!tag.IsValid) return false;
            if (!source.IsValid) return false;

            if (!_owners.TryGetValue(ownerId, out var state) || state == null)
            {
                return false;
            }

            if (!TryRemoveRef(state, tag.Value, source.Value, out var becameAbsent))
            {
                return false;
            }

            if (!becameAbsent)
            {
                return true;
            }

            state.Tags.Remove(tag);

            var removed = new GameplayTagContainer();
            removed.Add(tag);
            Raise(ownerId, new GameplayTagDelta(null, removed), source);
            return true;
        }

        public bool ApplyTemplate(int ownerId, int templateId, TagSource source, bool checkRequirements = false)
        {
            if (ownerId <= 0) return false;
            if (templateId <= 0) return false;
            if (!source.IsValid) return false;
            if (_templates == null) return false;

            if (!_templates.TryGet(templateId, out var template) || template == null)
            {
                return false;
            }

            var state = GetOrCreate(ownerId);

            if (checkRequirements)
            {
                if (!template.Requirements.IsSatisfiedBy(state.Tags))
                {
                    return false;
                }
            }

            GameplayTagContainer added = null;
            GameplayTagContainer removed = null;

            // Remove first.
            if (template.RemoveTags != null && template.RemoveTags.Count > 0)
            {
                foreach (var t in template.RemoveTags)
                {
                    if (!t.IsValid) continue;

                    if (TryRemoveAllRefs(state, t.Value, out var becameAbsent) && becameAbsent)
                    {
                        state.Tags.Remove(t);
                        removed ??= new GameplayTagContainer();
                        removed.Add(t);
                    }
                }
            }

            // Grant.
            if (template.GrantTags != null && template.GrantTags.Count > 0)
            {
                foreach (var t in template.GrantTags)
                {
                    if (!t.IsValid) continue;

                    if (TryAddRef(state, t.Value, source.Value, out var becamePresent) && becamePresent)
                    {
                        state.Tags.Add(t);
                        added ??= new GameplayTagContainer();
                        added.Add(t);
                    }
                }
            }

            if (added != null || removed != null)
            {
                Raise(ownerId, new GameplayTagDelta(added, removed), source);
            }

            return true;
        }

        private OwnerState GetOrCreate(int ownerId)
        {
            if (!_owners.TryGetValue(ownerId, out var state) || state == null)
            {
                state = new OwnerState();
                _owners[ownerId] = state;
            }
            return state;
        }

        private static bool TryAddRef(OwnerState state, int tagId, long source, out bool becamePresent)
        {
            becamePresent = false;
            if (state == null) return false;
            if (tagId <= 0) return false;
            if (source == 0) return false;

            if (!state.Refs.TryGetValue(tagId, out var bySource) || bySource == null)
            {
                bySource = new Dictionary<long, int>();
                state.Refs[tagId] = bySource;
            }

            var wasPresent = state.Tags.HasTagExact(GameplayTag.FromId(tagId));

            bySource.TryGetValue(source, out var count);
            count++;
            bySource[source] = count;

            if (!wasPresent)
            {
                becamePresent = true;
            }

            return true;
        }

        private static bool TryRemoveRef(OwnerState state, int tagId, long source, out bool becameAbsent)
        {
            becameAbsent = false;
            if (state == null) return false;
            if (tagId <= 0) return false;
            if (source == 0) return false;

            if (!state.Refs.TryGetValue(tagId, out var bySource) || bySource == null)
            {
                return false;
            }

            if (!bySource.TryGetValue(source, out var count) || count <= 0)
            {
                return false;
            }

            count--;
            if (count <= 0)
            {
                bySource.Remove(source);
            }
            else
            {
                bySource[source] = count;
            }

            if (bySource.Count == 0)
            {
                state.Refs.Remove(tagId);

                // No sources remain.
                becameAbsent = true;
            }

            return true;
        }

        private static bool TryRemoveAllRefs(OwnerState state, int tagId, out bool becameAbsent)
        {
            becameAbsent = false;
            if (state == null) return false;
            if (tagId <= 0) return false;

            if (!state.Refs.TryGetValue(tagId, out var bySource) || bySource == null || bySource.Count == 0)
            {
                // If we don't have refs but tag exists, remove it anyway.
                if (state.Tags.HasTagExact(GameplayTag.FromId(tagId)))
                {
                    becameAbsent = true;
                    return true;
                }
                return false;
            }

            state.Refs.Remove(tagId);
            becameAbsent = true;
            return true;
        }

        private void Raise(int ownerId, GameplayTagDelta delta, TagSource source)
        {
            if (delta.IsEmpty) return;
            TagsChanged?.Invoke(ownerId, delta, source);
        }
    }
}
