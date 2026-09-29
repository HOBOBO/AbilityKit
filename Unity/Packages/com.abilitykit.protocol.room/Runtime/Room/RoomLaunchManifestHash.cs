#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AbilityKit.Protocol.Room
{
    public static class RoomLaunchManifestHash
    {
        public static string Compute(IEnumerable<string> references,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var text = new StringBuilder();
            foreach (var reference in references.OrderBy(item => item, StringComparer.Ordinal))
                text.Append("ref:").Append(reference).Append('\n');
            if (metadata != null)
                foreach (var pair in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
                    text.Append("meta:").Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }
    }
}
