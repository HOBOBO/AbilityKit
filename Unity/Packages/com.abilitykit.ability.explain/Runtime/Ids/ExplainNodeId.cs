using System;

namespace AbilityKit.Ability.Explain
{
    public static class ExplainNodeId
    {
        public static string FromParts(string scope, string a, string b = null, string c = null)
        {
            var s = (scope ?? string.Empty) + "|" + (a ?? string.Empty) + "|" + (b ?? string.Empty) + "|" + (c ?? string.Empty);
            return "n_" + Fnv1A64Hex(s);
        }

        public static string FromKey(string scope, in PipelineItemKey key, string path = null)
        {
            return FromParts(scope, key.Type, key.Id, string.IsNullOrEmpty(path) ? key.Variant : path);
        }

        private static string Fnv1A64Hex(string input)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037ul;
                const ulong prime = 1099511628211ul;

                var hash = offset;
                for (var i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= prime;
                }

                return hash.ToString("x16");
            }
        }
    }
}
