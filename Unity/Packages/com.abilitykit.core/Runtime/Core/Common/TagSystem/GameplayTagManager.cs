using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public sealed class GameplayTagManager
    {
        private sealed class Node
        {
            public string Name;
            public int ParentId;
        }

        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Node> _nodes = new List<Node>(256);
        private readonly Dictionary<int, HashSet<int>> _ancestors = new Dictionary<int, HashSet<int>>();

        public static GameplayTagManager Instance { get; } = new GameplayTagManager();

        private GameplayTagManager()
        {
            _nodes.Add(new Node { Name = string.Empty, ParentId = 0 });
            _byName[string.Empty] = 0;
        }

        public GameplayTag RequestTag(string name)
        {
            if (!TryNormalize(name, out var normalized))
            {
                throw new ArgumentException("Invalid tag name", nameof(name));
            }

            var id = GetOrCreate(normalized);
            return new GameplayTag(id);
        }

        public bool TryGetTag(string name, out GameplayTag tag)
        {
            tag = default;
            if (!TryNormalize(name, out var normalized)) return false;
            if (_byName.TryGetValue(normalized, out var id))
            {
                tag = new GameplayTag(id);
                return true;
            }
            return false;
        }

        public string GetName(GameplayTag tag)
        {
            if (!tag.IsValid) return string.Empty;
            var id = tag.Id;
            if (id <= 0 || id >= _nodes.Count) return string.Empty;
            return _nodes[id].Name ?? string.Empty;
        }

        public bool Matches(GameplayTag tag, GameplayTag matchAgainst)
        {
            if (!tag.IsValid || !matchAgainst.IsValid) return false;
            if (tag.Id == matchAgainst.Id) return true;
            return IsChildOf(tag, matchAgainst);
        }

        public bool IsChildOf(GameplayTag tag, GameplayTag parent)
        {
            if (!tag.IsValid || !parent.IsValid) return false;
            if (tag.Id == parent.Id) return false;

            if (_ancestors.TryGetValue(tag.Id, out var ancestors))
            {
                return ancestors.Contains(parent.Id);
            }

            return false;
        }

        public GameplayTag GetParent(GameplayTag tag)
        {
            if (!tag.IsValid) return default;
            var parentId = _nodes[tag.Id].ParentId;
            return parentId == 0 ? default : new GameplayTag(parentId);
        }

        private int GetOrCreate(string normalized)
        {
            if (_byName.TryGetValue(normalized, out var existing)) return existing;

            var lastDot = normalized.LastIndexOf('.');
            var parentId = 0;
            if (lastDot > 0)
            {
                var parentName = normalized.Substring(0, lastDot);
                parentId = GetOrCreate(parentName);
            }

            var id = _nodes.Count;
            _nodes.Add(new Node { Name = normalized, ParentId = parentId });
            _byName[normalized] = id;
            BuildAncestorCache(id, parentId);
            return id;
        }

        private void BuildAncestorCache(int id, int parentId)
        {
            var ancestors = new HashSet<int>();

            if (parentId != 0)
            {
                ancestors.Add(parentId);

                if (_ancestors.TryGetValue(parentId, out var parentAncestors))
                {
                    foreach (var ancestor in parentAncestors)
                    {
                        ancestors.Add(ancestor);
                    }
                }
            }

            _ancestors[id] = ancestors;
        }

        private static bool TryNormalize(string name, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var s = name.Trim();
            if (s.Length == 0) return false;
            if (s[0] == '.' || s[s.Length - 1] == '.') return false;

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.')
                {
                    if (i > 0 && s[i - 1] == '.') return false;
                    continue;
                }

                if (char.IsWhiteSpace(c)) return false;
            }

            normalized = s;
            return true;
        }
    }
}
