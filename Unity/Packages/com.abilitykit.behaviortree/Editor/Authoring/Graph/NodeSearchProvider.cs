#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Editor
{
    internal sealed class NodeSearchQuery
    {
        public string Text { get; set; } = "";
        public string Category { get; set; } = "";
        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool FavoritesOnly { get; set; }
        public bool RecentOnly { get; set; }
    }

    internal sealed class NodeSearchOptions
    {
        public HashSet<string> FavoriteTypeIds { get; } = new(StringComparer.Ordinal);
        public List<string> RecentTypeIds { get; } = new();
        public Dictionary<string, HashSet<string>> TagsByTypeId { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class NodeSearchResult
    {
        public NodeSearchResult(
            NodeDescriptor descriptor,
            int score,
            IReadOnlyList<string> matchedTags,
            bool isFavorite,
            bool isRecent)
        {
            Descriptor = descriptor;
            Score = score;
            MatchedTags = matchedTags;
            IsFavorite = isFavorite;
            IsRecent = isRecent;
        }

        public NodeDescriptor Descriptor { get; }
        public int Score { get; }
        public IReadOnlyList<string> MatchedTags { get; }
        public bool IsFavorite { get; }
        public bool IsRecent { get; }
    }

    internal static class NodeSearchV2
    {
        public static IReadOnlyList<NodeSearchResult> Search(
            IEnumerable<NodeDescriptor> descriptors,
            NodeSearchQuery? query,
            NodeSearchOptions? options = null)
        {
            query ??= new NodeSearchQuery();
            options ??= new NodeSearchOptions();
            var text = (query.Text ?? "").Trim();
            var results = new List<NodeSearchResult>();

            foreach (var descriptor in descriptors ?? Array.Empty<NodeDescriptor>())
            {
                if (!string.IsNullOrWhiteSpace(query.Category)
                    && !string.Equals(descriptor.Category, query.Category, StringComparison.Ordinal))
                    continue;

                var favorite = options.FavoriteTypeIds.Contains(descriptor.TypeId);
                var recent = options.RecentTypeIds.Contains(descriptor.TypeId);
                if (query.FavoritesOnly && !favorite) continue;
                if (query.RecentOnly && !recent) continue;

                var tags = BuildTags(descriptor, options);
                var matchedTags = query.Tags.Count == 0
                    ? Array.Empty<string>()
                    : tags.Where(tag => query.Tags.Contains(tag))
                        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                if (query.Tags.Count > 0 && matchedTags.Length != query.Tags.Count) continue;

                var score = Score(descriptor, text, tags);
                if (text.Length > 0 && score <= 0) continue;
                if (favorite) score += 15;
                if (recent) score += Math.Max(1, 10 - options.RecentTypeIds.IndexOf(descriptor.TypeId));
                if (matchedTags.Length > 0) score += matchedTags.Length * 8;

                results.Add(new NodeSearchResult(descriptor, score, matchedTags, favorite, recent));
            }

            return results
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Descriptor.Category, StringComparer.Ordinal)
                .ThenBy(result => result.Descriptor.MenuOrder)
                .ThenBy(result => result.Descriptor.DisplayName, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<string> BuildTags(NodeDescriptor descriptor, NodeSearchOptions? options = null)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                descriptor.Category,
                descriptor.Kind.ToString(),
            };
            foreach (var part in descriptor.TypeId.Split('.'))
            {
                if (!string.IsNullOrWhiteSpace(part)) tags.Add(part);
            }
            if (options != null && options.TagsByTypeId.TryGetValue(descriptor.TypeId, out var configuredTags))
            {
                foreach (var tag in configuredTags)
                    if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag);
            }
            return tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static int Score(NodeDescriptor descriptor, string query, IReadOnlyList<string>? tags = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return 1;
            query = query.Trim();
            var best = Math.Max(
                FuzzyScore(descriptor.DisplayName, query),
                FuzzyScore(descriptor.TypeId, query));
            best = Math.Max(best, FuzzyScore(descriptor.Category, query) - 5);
            foreach (var tag in tags ?? BuildTags(descriptor))
                best = Math.Max(best, FuzzyScore(tag, query) - 3);
            return best;
        }

        private static int FuzzyScore(string candidate, string query)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(query)) return 0;
            if (string.Equals(candidate, query, StringComparison.OrdinalIgnoreCase)) return 1000;
            var index = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return 800 - index;

            var score = 0;
            var last = -1;
            foreach (var q in query)
            {
                var found = candidate.IndexOf(q.ToString(), last + 1, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return 0;
                score += last >= 0 && found == last + 1 ? 12 : 6;
                if (found == 0 || char.IsWhiteSpace(candidate[Math.Max(0, found - 1)]) || candidate[found - 1] == '.')
                    score += 4;
                last = found;
            }
            return score;
        }
    }

    /// <summary>节点创建菜单：从描述符目录拉取分组与类型。</summary>
    internal sealed class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private IAuthoringGraphHost? _host;
        private NodeSearchOptions _options = new();
        private NodeSearchQuery _query = new();

        public void Init(
            IAuthoringGraphHost host,
            NodeSearchOptions? options = null,
            NodeSearchQuery? query = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _options = options ?? new NodeSearchOptions();
            _query = query ?? new NodeSearchQuery();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node")),
            };

            var results = NodeSearchV2.Search(EditorNodeCatalog.Registry.Descriptors, _query, _options);
            foreach (var group in results
                         .Select(result => result.Descriptor.Category)
                         .Distinct()
                         .OrderBy(c => c, StringComparer.Ordinal))
            {
                entries.Add(new SearchTreeGroupEntry(new GUIContent(group), 1));
                foreach (var result in results
                             .Where(result => result.Descriptor.Category == group)
                             .OrderByDescending(result => result.Score)
                             .ThenBy(result => result.Descriptor.MenuOrder)
                             .ThenBy(result => result.Descriptor.DisplayName, StringComparer.Ordinal))
                {
                    var descriptor = result.Descriptor;
                    var label = result.IsFavorite
                        ? "* " + descriptor.DisplayName
                        : result.IsRecent
                            ? "Recent " + descriptor.DisplayName
                            : descriptor.DisplayName;
                    entries.Add(new SearchTreeEntry(new GUIContent(label))
                    {
                        level = 2,
                        userData = descriptor,
                    });
                }
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is not NodeDescriptor descriptor || _host == null) return false;
            if (_host.IsReadOnly) return false;

            var graphPosition = _host.ScreenToGraphPosition(context.screenMousePosition);
            _host.AddNode(descriptor, graphPosition);
            _options.RecentTypeIds.Remove(descriptor.TypeId);
            _options.RecentTypeIds.Insert(0, descriptor.TypeId);
            if (_options.RecentTypeIds.Count > 20)
                _options.RecentTypeIds.RemoveRange(20, _options.RecentTypeIds.Count - 20);
            return true;
        }
    }
}
