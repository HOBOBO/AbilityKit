#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringSearchResult
    {
        public AuthoringSearchResult(string query, IReadOnlyList<AuthoringNodeSearchHit> hits, int totalNodeCount)
        {
            Query = query ?? string.Empty;
            Hits = hits ?? Array.Empty<AuthoringNodeSearchHit>();
            TotalNodeCount = Math.Max(0, totalNodeCount);
        }

        public string Query { get; }
        public IReadOnlyList<AuthoringNodeSearchHit> Hits { get; }
        public int TotalNodeCount { get; }
    }
}
